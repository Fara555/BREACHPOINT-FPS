using UnityEngine;
using UnityEngine.AI;

namespace Breachpoint.Gameplay.AI
{
    public enum EnemyMovePace { Walk, Run, Sprint, Crouch }
    public enum EnemyMovementTier { Steady, CombatWalk, Run, Sprint, Crouch }
    public enum EnemyMovementPhase { Idle, Starting, Cruising, Stopping }
    public enum NavigationResult { None, Moving, Arrived, Unavailable, Partial, Invalid, Stuck }
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyNavigation : MonoBehaviour
    {
        private NavMeshAgent _agent;
        private EnemyMovementConfig _config;
        private NavMeshPath _path;
        private float _nextRepath;
        private float _lastProgressTime;
        private Vector3 _lastProgressPosition;
        private bool _requested;
        private Vector3 _desiredFacing;
        private float _facingUntil;
        private Quaternion _turnStart;
        private Quaternion _turnEnd;
        private float _turnBegan;
        private float _turnDuration;
        private bool _animationDrivenTurn;
        private float _stationaryTurnSpeed;
        private float _deferFacingUntil;
        private EnemyBrain _brain;
        private float _phaseBegan;
        private float _stopSpeed;
        private float _stopDuration;
        private bool _stoppingForArrival;
        private bool _hasSteadyAnimationPhase;
        private float _steadyStartFraction;
        private float _steadyStopFraction;
        private bool _steadyCruisePose;
        private bool _steadyRestPose;
        private float _combatStartDuration;
        private bool _combatStarting;
        private bool _hasCombatPresentation;
        private bool _combatMovementReady;
        public void ConfigureCombatStart(float duration)
        { _combatStartDuration = duration; _hasCombatPresentation = duration > 0f; _combatMovementReady = false; }
        public void SetCombatMovementReady(bool ready) => _combatMovementReady = ready;
        // Animation publishes its evaluated phase; NavMesh still owns all translation.
        public void SetSteadyAnimationPhase(float startFraction, float stopFraction, bool cruise, bool rest)
        {
            _hasSteadyAnimationPhase = true;
            _steadyStartFraction = startFraction; _steadyStopFraction = stopFraction;
            _steadyCruisePose = cruise; _steadyRestPose = rest;
        }
        public void ClearSteadyAnimationPhase() => _hasSteadyAnimationPhase = false;
        public EnemyMovementPhase MovementPhase { get; private set; }
        public float AllowedSpeed => Ready ? _agent.speed : 0f;
        public NavigationResult Result { get; private set; }
        public Vector3 Velocity => Ready ? _agent.velocity : Vector3.zero;
        public Vector3 DesiredVelocity => Ready ? _agent.desiredVelocity : Vector3.zero;
        public float DesiredSpeed => Ready ? _agent.speed : 0f;
        public EnemyMovementTier DesiredMovementTier { get; private set; }
        public float RequestedWorldSpeed { get; private set; }
        public bool HasMovementRequest => _requested && MovementPhase != EnemyMovementPhase.Stopping && Result == NavigationResult.Moving;
        public Vector3 PresentationDesiredVelocity
        {
            get
            {
                if (!HasMovementRequest || !Ready) return Vector3.zero;
                Vector3 velocity = _agent.desiredVelocity;
                if (velocity.sqrMagnitude > 0.0001f) return velocity;
                Vector3 direction = _agent.steeringTarget - transform.position; direction.y = 0f;
                return direction.normalized * RequestedWorldSpeed;
            }
        }
        public Vector3 DesiredFacing => _desiredFacing;
        public bool IsTurning { get; private set; }
        public event System.Action<Vector3> FacingRequested;
        public Vector3 Destination { get; private set; }
        public bool Ready => _agent != null && _agent.enabled && _agent.isOnNavMesh;
        public bool Failed => Result == NavigationResult.Unavailable || Result == NavigationResult.Invalid || Result == NavigationResult.Partial || Result == NavigationResult.Stuck;
        public bool Arrived => Result == NavigationResult.Arrived || Ready && _requested && MovementPhase != EnemyMovementPhase.Stopping && !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.15f;
        public int RepathCount { get; private set; }
        public int CopyPathCorners(Vector3[] buffer) => _requested && _path != null ? _path.GetCornersNonAlloc(buffer) : 0;

        public void Initialize(EnemyMovementConfig config)
        {
            _config = config; _agent = GetComponent<NavMeshAgent>(); _path = new NavMeshPath();
            _brain = GetComponent<EnemyBrain>();
            _agent.acceleration = config.Acceleration; _agent.angularSpeed = config.TurnSpeed;
            _agent.stoppingDistance = config.StoppingDistance;
            // Gameplay rotation is updated here; turn clips never apply root motion.
            _agent.updateRotation = false;
        }
        public bool ResetAt(Vector3 position)
        {
            _nextRepath = 0f; _requested = false; Result = NavigationResult.None; RepathCount = 0;
            RequestedWorldSpeed = 0f; DesiredMovementTier = EnemyMovementTier.Steady;
            MovementPhase = EnemyMovementPhase.Idle; _stoppingForArrival = false;
            _hasSteadyAnimationPhase = false; _combatStarting = false; _combatMovementReady = false;
            CancelTurn(); _desiredFacing = Vector3.zero; _deferFacingUntil = _facingUntil = 0f;
            if (_agent == null || !NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, _agent.areaMask))
            { Result = NavigationResult.Unavailable; return false; }
            bool success = _agent.Warp(hit.position);
            if (Ready) { _agent.ResetPath(); _agent.velocity = Vector3.zero; _agent.isStopped = true; }
            return success;
        }
        public void MoveTo(Vector3 point, bool run, float now) => MoveTo(point, run ? EnemyMovePace.Run : EnemyMovePace.Walk, now);
        public void MoveTo(Vector3 point, EnemyMovePace pace, float now)
        {
            if (!Ready) { Result = NavigationResult.Unavailable; return; }
            if (Result == NavigationResult.Arrived && (point - Destination).sqrMagnitude < .01f) return;
            // Repeated patrol ticks must not restart a destination's braking phase.
            if (_stoppingForArrival && MovementPhase == EnemyMovementPhase.Stopping && (point - Destination).sqrMagnitude < .01f) return;
            DesiredMovementTier = pace == EnemyMovePace.Crouch ? EnemyMovementTier.Crouch : pace == EnemyMovePace.Sprint ? EnemyMovementTier.Sprint : pace == EnemyMovePace.Run ? EnemyMovementTier.Run : _brain.States.Group == EnemyStateGroup.Passive ? EnemyMovementTier.Steady : EnemyMovementTier.CombatWalk;
            RequestedWorldSpeed = pace == EnemyMovePace.Sprint ? _config.SprintSpeed : pace == EnemyMovePace.Crouch ? _config.CrouchSpeed : pace == EnemyMovePace.Run ? _config.RunSpeed : _config.WalkSpeed;
            if (DesiredMovementTier == EnemyMovementTier.Steady && _config.SteadyWalkSpeed > 0f) RequestedWorldSpeed = _config.SteadyWalkSpeed;
            _agent.acceleration = DesiredMovementTier == EnemyMovementTier.Steady ? _config.SteadyAcceleration : _config.Acceleration;
            if (DesiredMovementTier != EnemyMovementTier.CombatWalk && DesiredMovementTier != EnemyMovementTier.Run) _combatStarting = false;
            if (DesiredMovementTier != EnemyMovementTier.Steady && MovementPhase == EnemyMovementPhase.Starting && !_combatStarting) MovementPhase = EnemyMovementPhase.Cruising;
            if (!_requested || MovementPhase == EnemyMovementPhase.Stopping)
            {
                _combatStarting = _combatStartDuration > 0f && _brain.States.Group != EnemyStateGroup.Passive && (DesiredMovementTier == EnemyMovementTier.CombatWalk || DesiredMovementTier == EnemyMovementTier.Run) && Velocity.sqrMagnitude < .01f;
                bool starting = (DesiredMovementTier == EnemyMovementTier.Steady || _combatStarting) && Velocity.sqrMagnitude < .01f;
                MovementPhase = starting ? EnemyMovementPhase.Starting : EnemyMovementPhase.Cruising;
                _phaseBegan = Time.time; _stoppingForArrival = false;
                _agent.speed = starting ? 0f : RequestedWorldSpeed;
            }
            if (now < _nextRepath) return;
            _nextRepath = now + _config.RepathInterval; RepathCount++;
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 1.5f, _agent.areaMask) || !_agent.CalculatePath(hit.position, _path) || _path.status != NavMeshPathStatus.PathComplete)
            {
                Stop(); _nextRepath = now + _config.RepathInterval;
                Result = _path.status == NavMeshPathStatus.PathPartial ? NavigationResult.Partial : NavigationResult.Invalid; return;
            }
            if (!_requested || (Destination - hit.position).sqrMagnitude > 1f)
            { _lastProgressTime = now; _lastProgressPosition = transform.position; }
            Destination = hit.position; _requested = true;
            _agent.isStopped = false; _agent.SetPath(_path); Result = NavigationResult.Moving;
            if ((transform.position - _lastProgressPosition).sqrMagnitude > 0.04f)
            { _lastProgressTime = now; _lastProgressPosition = transform.position; }
            else if (!Arrived && now - _lastProgressTime > _config.StuckTimeout)
            { Stop(); _nextRepath = now + _config.RepathInterval; Result = NavigationResult.Stuck; }
            if (Arrived) Result = NavigationResult.Arrived;
        }
        public void Stop()
        {
            if (Ready && (_requested || !_agent.isStopped || _agent.velocity.sqrMagnitude > 0.0001f))
            { _agent.ResetPath(); _agent.velocity = Vector3.zero; _agent.isStopped = true; }
            _requested = false; _nextRepath = 0f;
            RequestedWorldSpeed = 0f;
            MovementPhase = EnemyMovementPhase.Idle; _stoppingForArrival = false;
            if (Ready)
            {
                _agent.speed = 0f;
                if (_config != null) _agent.acceleration = DesiredMovementTier == EnemyMovementTier.Steady ? _config.SteadyAcceleration : _config.Acceleration;
            }
        }
        // Ordinary locomotion keeps a valid path during its short braking envelope.
        // Stop remains the explicit hard cancellation used by combat/death callers.
        public void RequestStop(bool arrival = false)
        {
            if (MovementPhase == EnemyMovementPhase.Stopping) return;
            if (!Ready || !_requested || Velocity.sqrMagnitude < .0001f) { Stop(); if (arrival) Result = NavigationResult.Arrived; return; }
            _stopSpeed = Velocity.magnitude;
            _stopDuration = OrdinaryStopDuration(_stopSpeed);
            _steadyRestPose = false; _steadyStopFraction = 1f;
            // The agent must be able to follow the falling speed limit before the phase ends.
            _agent.acceleration = Mathf.Max(_agent.acceleration, _stopSpeed / _stopDuration);
            _phaseBegan = Time.time; _stoppingForArrival = arrival;
            MovementPhase = EnemyMovementPhase.Stopping; RequestedWorldSpeed = 0f;
        }
        public void AcknowledgeFailure() { if (Failed) Result = NavigationResult.None; }
        private float OrdinaryStopDuration(float speed) => DesiredMovementTier == EnemyMovementTier.Steady ? Mathf.Max(.05f, _config.SteadyStopDuration) : Mathf.Clamp(speed / Mathf.Max(.1f, _config.StopDeceleration), .2f, .5f);
        public void StopAndClearFacing()
        { Stop(); CancelTurn(); _desiredFacing = Vector3.zero; _facingUntil = 0f; }
        public void Face(Vector3 point, float deltaTime)
        {
            Vector3 direction = point - transform.position; direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return;
            _desiredFacing = direction.normalized; _facingUntil = Time.time + 0.25f;
            FacingRequested?.Invoke(_desiredFacing);
        }
        public bool BeginTurn(Vector3 direction, float duration, bool animationDriven = false)
        {
            if (IsTurning || Velocity.sqrMagnitude > 0.01f || duration <= 0f) return false;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return false;
            _turnStart = transform.rotation; _turnEnd = Quaternion.LookRotation(direction);
            _turnBegan = Time.time; _turnDuration = duration; _animationDrivenTurn = animationDriven; IsTurning = true; return true;
        }
        public void ApplyAnimatedTurn(float progress, bool complete)
        {
            if (!IsTurning || !_animationDrivenTurn) return;
            transform.rotation = complete ? _turnEnd : Quaternion.SlerpUnclamped(_turnStart, _turnEnd, progress);
            if (complete) IsTurning = false;
        }
        public void CancelTurn() => IsTurning = false;
        public void ConfigurePresentationTurning(float stationarySpeed) => _stationaryTurnSpeed = stationarySpeed;
        public void DeferStationaryFacing(float duration) => _deferFacingUntil = Time.time + duration;
        private void Update()
        {
            if (_config == null || !Ready) return;
            if (MovementPhase == EnemyMovementPhase.Stopping)
            {
                bool synchronized = DesiredMovementTier == EnemyMovementTier.Steady && _hasSteadyAnimationPhase;
                float progress = Mathf.Clamp01((Time.time - _phaseBegan) / _stopDuration);
                _agent.speed = _stopSpeed * (synchronized ? _steadyStopFraction : 1f - progress);
                if (synchronized ? _steadyRestPose && Velocity.magnitude < .035f : progress >= 1f) { bool arrived = _stoppingForArrival; Stop(); Result = arrived ? NavigationResult.Arrived : NavigationResult.None; }
            }
            else if (_requested)
            {
                if (MovementPhase == EnemyMovementPhase.Starting)
                {
                    if (_combatStarting)
                    {
                        // Keep authored readiness intact: intent leads, but stationary Raise
                        // cannot carry lateral translation. Ramp only once legs can contribute.
                        if (_hasCombatPresentation && !_combatMovementReady) { _phaseBegan = Time.time; _agent.speed = 0f; }
                        else
                        {
                            float startProgress = Mathf.Clamp01((Time.time - _phaseBegan) / _combatStartDuration);
                            _agent.speed = RequestedWorldSpeed * Mathf.SmoothStep(0f, 1f, startProgress);
                            if (startProgress >= 1f) { MovementPhase = EnemyMovementPhase.Cruising; _combatStarting = false; }
                        }
                    }
                    else
                    {
                        float progress = Mathf.Clamp01((Time.time - _phaseBegan - _config.SteadyStartDelay) / Mathf.Max(.05f, _config.SteadyStartDuration));
                        _agent.speed = RequestedWorldSpeed * (_hasSteadyAnimationPhase ? _steadyStartFraction : Mathf.SmoothStep(0f, 1f, progress));
                        if (_hasSteadyAnimationPhase ? _steadyCruisePose : progress >= 1f) MovementPhase = EnemyMovementPhase.Cruising;
                    }
                }
                else _agent.speed = Mathf.MoveTowards(_agent.speed, RequestedWorldSpeed, _config.Acceleration * Time.deltaTime);
                if (!_agent.pathPending && _agent.hasPath && _agent.remainingDistance <= _agent.stoppingDistance + Velocity.magnitude * OrdinaryStopDuration(Velocity.magnitude) * .5f)
                    RequestStop(true);
            }
            if (IsTurning)
            {
                if (Velocity.sqrMagnitude > 0.01f) CancelTurn();
                else
                {
                    if (_animationDrivenTurn) return;
                    float progress = Mathf.Clamp01((Time.time - _turnBegan) / _turnDuration);
                    transform.rotation = Quaternion.Slerp(_turnStart, _turnEnd, Mathf.SmoothStep(0f, 1f, progress));
                    if (progress >= 1f) IsTurning = false;
                    return;
                }
            }
            Vector3 facing = Time.time < _facingUntil ? _desiredFacing : Velocity.sqrMagnitude > 0.01f ? DesiredVelocity : _desiredFacing;
            if (Velocity.sqrMagnitude < 0.01f && Time.time < _deferFacingUntil) return;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), (_stationaryTurnSpeed > 0f && Velocity.sqrMagnitude < 0.01f ? _stationaryTurnSpeed : _config.TurnSpeed) * Time.deltaTime);
        }
        private void OnDisable() { Stop(); CancelTurn(); _desiredFacing = Vector3.zero; }
    }
}
