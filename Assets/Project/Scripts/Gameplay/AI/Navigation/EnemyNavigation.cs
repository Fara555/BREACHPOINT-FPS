using UnityEngine;
using UnityEngine.AI;

namespace Breachpoint.Gameplay.AI
{
    public enum EnemyMovePace { Walk, Run, Sprint, Crouch }
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
        public NavigationResult Result { get; private set; }
        public Vector3 Velocity => Ready ? _agent.velocity : Vector3.zero;
        public Vector3 DesiredVelocity => Ready ? _agent.desiredVelocity : Vector3.zero;
        public float DesiredSpeed => Ready ? _agent.speed : 0f;
        public Vector3 DesiredFacing => _desiredFacing;
        public bool IsTurning { get; private set; }
        public event System.Action<Vector3> FacingRequested;
        public Vector3 Destination { get; private set; }
        public bool Ready => _agent != null && _agent.enabled && _agent.isOnNavMesh;
        public bool Failed => Result == NavigationResult.Unavailable || Result == NavigationResult.Invalid || Result == NavigationResult.Partial || Result == NavigationResult.Stuck;
        public bool Arrived => Ready && _requested && !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.15f;
        public int RepathCount { get; private set; }
        public int CopyPathCorners(Vector3[] buffer) => _requested && _path != null ? _path.GetCornersNonAlloc(buffer) : 0;

        public void Initialize(EnemyMovementConfig config)
        {
            _config = config; _agent = GetComponent<NavMeshAgent>(); _path = new NavMeshPath();
            _agent.acceleration = config.Acceleration; _agent.angularSpeed = config.TurnSpeed;
            _agent.stoppingDistance = config.StoppingDistance;
            // Gameplay rotation is updated here; turn clips never apply root motion.
            _agent.updateRotation = false;
        }
        public bool ResetAt(Vector3 position)
        {
            _nextRepath = 0f; _requested = false; Result = NavigationResult.None; RepathCount = 0;
            CancelTurn(); _desiredFacing = Vector3.zero;
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
            _agent.speed = pace == EnemyMovePace.Sprint ? _config.SprintSpeed : pace == EnemyMovePace.Crouch ? _config.CrouchSpeed : pace == EnemyMovePace.Run ? _config.RunSpeed : _config.WalkSpeed;
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
        }
        public void AcknowledgeFailure() { if (Failed) Result = NavigationResult.None; }
        public void StopAndClearFacing()
        { Stop(); CancelTurn(); _desiredFacing = Vector3.zero; _facingUntil = 0f; }
        public void Face(Vector3 point, float deltaTime)
        {
            Vector3 direction = point - transform.position; direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return;
            _desiredFacing = direction.normalized; _facingUntil = Time.time + 0.25f;
            FacingRequested?.Invoke(_desiredFacing);
        }
        public bool BeginTurn(Vector3 direction, float duration)
        {
            if (IsTurning || Velocity.sqrMagnitude > 0.01f || duration <= 0f) return false;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return false;
            _turnStart = transform.rotation; _turnEnd = Quaternion.LookRotation(direction);
            _turnBegan = Time.time; _turnDuration = duration; IsTurning = true; return true;
        }
        public void CancelTurn() => IsTurning = false;
        private void Update()
        {
            if (_config == null || !Ready) return;
            if (IsTurning)
            {
                if (Velocity.sqrMagnitude > 0.01f) CancelTurn();
                else
                {
                    float progress = Mathf.Clamp01((Time.time - _turnBegan) / _turnDuration);
                    transform.rotation = Quaternion.Slerp(_turnStart, _turnEnd, Mathf.SmoothStep(0f, 1f, progress));
                    if (progress >= 1f) IsTurning = false;
                    return;
                }
            }
            Vector3 facing = Time.time < _facingUntil ? _desiredFacing : Velocity.sqrMagnitude > 0.01f ? DesiredVelocity : _desiredFacing;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), _config.TurnSpeed * Time.deltaTime);
        }
        private void OnDisable() { Stop(); CancelTurn(); _desiredFacing = Vector3.zero; }
    }
}
