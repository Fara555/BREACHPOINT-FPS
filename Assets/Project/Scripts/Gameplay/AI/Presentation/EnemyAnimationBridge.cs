using Breachpoint.Gameplay.Combat;
using UnityEngine;

namespace Breachpoint.Gameplay.AI
{
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyBrain))]
    public sealed class EnemyAnimationBridge : MonoBehaviour
    {
        private const float CombatLocomotionReadyBlend = .25f;
        private enum Parameter { MoveSpeed, MoveX, MoveY, IsCombat, IsCrouching, IsFiring, IsReloading, IsHit, IsDead, HitDirection, ReloadSpeed, Turn90Left, Turn90Right, Turn180Left, Turn180Right, Hit, Speed, Dead, Reloading, Attack, SteadyStride, CombatStride, CrouchStride }
        private static readonly int[] Hashes = CreateHashes();
        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _aimTarget;
        [SerializeField] private EnemyAnimationConfig _config = new EnemyAnimationConfig();
        [SerializeField] private bool _logTransitions;
        private readonly bool[] _available = new bool[Hashes.Length];
        private EnemyBrain _brain;
        private EnemyActor _actor;
        private EnemyNavigation _navigation;
        private EnemyStance _stance;
        private EnemyRagdollPresenter _ragdoll;
        private bool _bound;
        private bool _modern;
        private float _nextTurn;
        private float _fireUntil;
        private float _hitUntil;
        private float _lethalDirection;
        private Vector3 _deathVelocity;
        public float DeathPlanarSpeed { get; private set; }
        private int _lastStateHash;
        private int _lastActionHash;
        private int _lastRecoilHash;
        private EnemyStateGroup _lastLoggedMode;
        private Vector3 _pendingTurn;
        private float _turnCandidateAt;
        private bool _largeTurnCandidate;
        private EnemyTurnProfile _activeTurnProfile;
        private bool _turnAnimationStarted;
        private Vector3 _smoothedAimPosition;
        private Transform _leftFoot;
        private Transform _rightFoot;
        private Vector3 _lastLeftFoot;
        private Vector3 _lastRightFoot;
        private float _nextFootSample;
        public bool FootMotionDetected { get; private set; }
        public float MoveSpeedRaw { get; private set; }
        public float MoveSpeedSmoothed { get; private set; }
        public Vector2 MoveDirectionRaw { get; private set; }
        public Vector2 MoveDirectionSmoothed { get; private set; }
        public Transform AimTarget => _aimTarget;
        public float BodyAimErrorDegrees => _aimTarget == null ? 0f : Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(_aimTarget.position - transform.position, Vector3.up));
        public float MuzzleAimErrorDegrees => _aimTarget == null || _actor == null ? 0f : Vector3.Angle(_actor.Muzzle.forward, _aimTarget.position - _actor.Muzzle.position);
        public float HorizontalMuzzleErrorDegrees => _aimTarget == null || _actor == null ? 0f : Vector3.SignedAngle(Vector3.ProjectOnPlane(_actor.Muzzle.forward, Vector3.up), Vector3.ProjectOnPlane(_aimTarget.position - _actor.Muzzle.position, Vector3.up), Vector3.up);
        public float VerticalMuzzleErrorDegrees => _aimTarget == null || _actor == null ? 0f : Mathf.Asin(Mathf.Clamp(_actor.Muzzle.forward.y, -1f, 1f)) * Mathf.Rad2Deg - Mathf.Asin(Mathf.Clamp((_aimTarget.position - _actor.Muzzle.position).normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        public bool IsRaising => _animator != null && (_animator.GetCurrentAnimatorStateInfo(0).IsName("RaiseWeapon") || _animator.IsInTransition(0) && _animator.GetNextAnimatorStateInfo(0).IsName("RaiseWeapon"));
        public float AimPresentationWeight
        {
            get
            {
                if (_animator == null || _brain.States.Group == EnemyStateGroup.Passive || _brain.Combat.IsReloading || IsHit) return 0f;
                var current = _animator.GetCurrentAnimatorStateInfo(0);
                bool transition = _animator.IsInTransition(0);
                var next = _animator.GetNextAnimatorStateInfo(0);
                float blend = transition ? Mathf.Clamp01(_animator.GetAnimatorTransitionInfo(0).normalizedTime) : 0f;
                float weight = AimStateWeight(current);
                if (transition) weight = Mathf.Lerp(weight, AimStateWeight(next), blend);
                return weight * (1f - SprintPoseWeight);
            }
        }
        public float SprintPoseWeight => !IsCrouching && _animator != null && _animator.GetCurrentAnimatorStateInfo(0).IsName("StandingLocomotion") ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_config.RunThreshold, _config.SprintThreshold, MoveSpeedSmoothed)) : 0f;
        private float AimStateWeight(AnimatorStateInfo state)
        {
            if (state.IsName("RaiseWeapon")) return _config.RaiseAimAt(state.normalizedTime);
            if (state.IsName("CombatTurn180Left")) return _config.LeftTurnAimAt(state.normalizedTime);
            return state.IsName("StandingLocomotion") || state.IsName("CrouchLocomotion") || state.IsTag("Turn") ? 1f : 0f;
        }
        public bool IsAimPoseReady => _animator != null && (_animator.GetCurrentAnimatorStateInfo(0).IsName("StandingLocomotion") || _animator.GetCurrentAnimatorStateInfo(0).IsName("CrouchLocomotion") || _animator.GetCurrentAnimatorStateInfo(0).IsTag("Turn"));
        public EnemyAnimationConfig Config => _config;
        public Animator Animator => _animator;
        public string CurrentTurn { get; private set; }
        public bool LogsAnimationTransitions => _logTransitions;
        public bool IsCrouching => _stance != null && _stance.IsCrouching;
        public bool IsHit => Time.time < _hitUntil;

        private static int[] CreateHashes()
        {
            string[] names = System.Enum.GetNames(typeof(Parameter));
            var hashes = new int[names.Length];
            for (int i = 0; i < names.Length; i++) hashes[i] = UnityEngine.Animator.StringToHash(names[i]);
            return hashes;
        }
        private void Awake()
        {
            _brain = GetComponent<EnemyBrain>(); _actor = GetComponent<EnemyActor>();
            _navigation = GetComponent<EnemyNavigation>(); _stance = GetComponent<EnemyStance>();
            _ragdoll = GetComponent<EnemyRagdollPresenter>();
        }
        private void Start() => Bind();
        private void OnEnable() { if (_brain != null) Bind(); }
        private void Bind()
        {
            if (_bound || _brain.Combat == null || _animator == null || _animator.runtimeAnimatorController == null) return;
            System.Array.Clear(_available, 0, _available.Length);
            foreach (var parameter in _animator.parameters)
                for (int i = 0; i < Hashes.Length; i++) if (parameter.nameHash == Hashes[i]) _available[i] = true;
            _modern = Has(Parameter.IsCombat);
            if (_animator.isHuman) { _leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot); _rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot); }
            if (_modern) { _navigation.ConfigurePresentationTurning(_config.StationaryTurnSpeed); _navigation.ConfigureCombatStart(_config.CombatStartDuration); }
            _animator.applyRootMotion = false;
            // Hitscan and shot VFX use the bone-attached muzzle even outside the camera.
            if (_modern) _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _brain.ResetCompleted += ResetView; _navigation.FacingRequested += PresentTurn;
            _brain.Combat.Fired += Fire; _brain.Combat.ReloadStarted += Reload;
            _actor.Health.DamageReceived += ReceiveDamage; _brain.DeathCompleted += Die;
            _bound = true; ResetView();
        }
        private void Update()
        {
            if (!_bound) Bind();
            if (!_bound || !_animator.enabled || _actor.Health.IsDead) return;
            Vector3 velocity = _navigation.Velocity; velocity.y = 0f;
            EnemyMovementConfig movement = _brain.Config.Movement;
            float speed = velocity.magnitude;
            Vector3 desired = _navigation.PresentationDesiredVelocity;
            float requestedSpeed = _navigation.RequestedWorldSpeed;
            MoveSpeedRaw = EnemyAnimationMath.MovementBlend(speed, requestedSpeed, _navigation.DesiredMovementTier, _navigation.HasMovementRequest, movement, _config);
            if (_navigation.MovementPhase == EnemyMovementPhase.Starting && (_navigation.DesiredMovementTier == EnemyMovementTier.CombatWalk || _navigation.DesiredMovementTier == EnemyMovementTier.Run))
                MoveSpeedRaw = Mathf.Max(MoveSpeedRaw, _config.WalkThreshold * _config.CombatStartBlend);
            bool steadyStop = _navigation.DesiredMovementTier == EnemyMovementTier.Steady && _navigation.MovementPhase == EnemyMovementPhase.Stopping;
            if (steadyStop) MoveSpeedRaw = MoveSpeedSmoothed = 0f;
            float damping = MoveSpeedRaw > MoveSpeedSmoothed ? _config.MoveSpeedAccelerationDamp : _config.MoveSpeedDecelerationDamp;
            MoveSpeedSmoothed = Mathf.Lerp(MoveSpeedSmoothed, MoveSpeedRaw, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(.01f, damping)));
            if (!_navigation.HasMovementRequest && MoveSpeedSmoothed < .002f) MoveSpeedSmoothed = 0f;
            Float(Parameter.MoveSpeed, MoveSpeedSmoothed, 0f);
            Float(Parameter.Speed, speed, _config.FloatDamping);
            Float(Parameter.SteadyStride, EnemyAnimationMath.StrideSpeed(speed, _config.SteadyNaturalSpeed, _config), _config.FloatDamping);
            Float(Parameter.CrouchStride, EnemyAnimationMath.StrideSpeed(speed, _config.CrouchNaturalSpeed, _config), _config.FloatDamping);
            MoveDirectionRaw = EnemyAnimationMath.MovementDirection(transform.rotation, desired, velocity, requestedSpeed);
            if (MoveDirectionRaw.sqrMagnitude > .0001f)
            {
                if (MoveDirectionSmoothed.sqrMagnitude < .0001f) MoveDirectionSmoothed = MoveDirectionRaw;
                else MoveDirectionSmoothed = Vector2.Lerp(MoveDirectionSmoothed, MoveDirectionRaw, 1f - Mathf.Exp(-Time.deltaTime / _config.MoveDirectionDamp));
            }
            float combatNaturalSpeed = _navigation.DesiredMovementTier == EnemyMovementTier.Sprint
                ? EnemyAnimationMath.NaturalCombatSpeed(MoveSpeedSmoothed, _config)
                : EnemyAnimationMath.NaturalCombatSpeed(MoveSpeedSmoothed, MoveDirectionSmoothed, _config);
            Float(Parameter.CombatStride, EnemyAnimationMath.StrideSpeed(speed, combatNaturalSpeed, _config), _config.FloatDamping);
            // Stance intent can change before the evaluated crouch pose has blended out.
            // Keep that tree centered at rest throughout entry and exit, even if AI
            // requests exposure while StandingToCrouch is still finishing.
            var baseState = _animator.GetCurrentAnimatorStateInfo(0);
            var nextBaseState = _animator.GetNextAnimatorStateInfo(0);
            bool crouchPose = IsCrouching || UsesCrouchDirection(baseState) ||
                _animator.IsInTransition(0) && UsesCrouchDirection(nextBaseState);
            float directionWeight = crouchPose ? Mathf.Clamp01(MoveSpeedSmoothed / _config.WalkThreshold) : 1f;
            Float(Parameter.MoveX, MoveDirectionSmoothed.x * directionWeight, 0f);
            Float(Parameter.MoveY, MoveDirectionSmoothed.y * directionWeight, 0f);
            Bool(Parameter.IsCombat, _brain.States.Group != EnemyStateGroup.Passive || IsCrouching);
            Bool(Parameter.IsCrouching, IsCrouching);
            Bool(Parameter.IsFiring, !_brain.Combat.IsReloading && Time.time < _fireUntil);
            Bool(Parameter.IsReloading, _brain.Combat.IsReloading); Bool(Parameter.Reloading, _brain.Combat.IsReloading);
            Bool(Parameter.IsHit, IsHit);
            Float(Parameter.ReloadSpeed, 1f / Mathf.Max(0.01f, _brain.Config.Combat.ReloadDuration), 0f);
            if (speed > _config.StationarySpeed || IsHit || _brain.Combat.IsReloading)
            { ClearTurnTriggers(); _navigation.CancelTurn(); CurrentTurn = null; _pendingTurn = Vector3.zero; }
            else if (!_navigation.IsTurning) CurrentTurn = null;
            if (_pendingTurn.sqrMagnitude > 0f)
            {
                // Preserve the requested heading while an authored stance/readiness blend finishes.
                // Otherwise navigation consumes part of the turn before its footwork can begin.
                _navigation.DeferStationaryFacing(_config.TurnStableTime + _config.TurnBlendIn);
                TryPresentTurn(_pendingTurn);
            }
            if (_modern)
            {
                for (int layer = 1; layer < _animator.layerCount; layer++)
                {
                    AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(layer);
                    AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(layer);
                    bool active = !current.IsTag("None") || (_animator.IsInTransition(layer) && !next.IsTag("None"));
                    if (_animator.IsInTransition(layer) && next.IsTag("None")) active = false;
                    // Empty action layers fade out instead of contributing default Humanoid muscles.
                    _animator.SetLayerWeight(layer, Mathf.MoveTowards(_animator.GetLayerWeight(layer), active ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, _config.ActionLayerBlend)));
                }
            }
            if (_logTransitions)
            {
                int hash = _animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
                if (hash != _lastStateHash)
                {
                    Debug.Log($"[Enemy:{name}][Animation] {EnemyAnimationStateNames.Get(_lastStateHash)} -> {EnemyAnimationStateNames.Get(hash)} | Mode: {_lastLoggedMode} -> {_brain.States.Group} | Velocity: {speed:0.00} m/s | Reason: {_brain.States.LastReason}", this);
                    _lastStateHash = hash; _lastLoggedMode = _brain.States.Group;
                }
                if (_modern)
                {
                    for (int layer = 1; layer < _animator.layerCount; layer++)
                    {
                        int action = _animator.GetCurrentAnimatorStateInfo(layer).fullPathHash;
                        int previous = layer == 1 ? _lastActionHash : _lastRecoilHash;
                        if (action != previous)
                        {
                            Debug.Log($"[Enemy:{name}][Animation] {EnemyAnimationStateNames.Get(previous)} -> {EnemyAnimationStateNames.Get(action)} | Velocity: {speed:0.00} m/s | IsCrouching: {IsCrouching} | Reason: Action priority changed", this);
                            if (layer == 1) _lastActionHash = action; else _lastRecoilHash = action;
                        }
                    }
                }
            }
        }
        private void PresentTurn(Vector3 direction)
        {
            if (!_bound || !_modern || _navigation.IsTurning || _navigation.Velocity.sqrMagnitude > _config.StationarySpeed * _config.StationarySpeed) return;
            float angle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);
            if (Mathf.Abs(angle) < _config.Turn90Angle)
            {
                _pendingTurn = Vector3.zero;
                _navigation.DeferStationaryFacing(0f);
                return;
            }
            if (_pendingTurn.sqrMagnitude == 0f || Vector3.Angle(_pendingTurn, direction) > _config.TurnHysteresis)
            {
                _turnCandidateAt = Time.time;
                _largeTurnCandidate = Mathf.Abs(angle) >= _config.Turn180Angle;
            }
            _pendingTurn = direction;
            _navigation.DeferStationaryFacing(_config.TurnStableTime + _config.TurnBlendIn);
            TryPresentTurn(direction);
        }
        private static bool UsesCrouchDirection(AnimatorStateInfo state) =>
            state.IsName("CrouchLocomotion") || state.IsName("StandingToCrouch") || state.IsName("CrouchToStanding");
        private void TryPresentTurn(Vector3 direction)
        {
            if (!_bound || !_modern || !_animator.enabled || _actor.Health.IsDead || _navigation.IsTurning ||
                Time.time < _nextTurn || _navigation.Velocity.sqrMagnitude > _config.StationarySpeed * _config.StationarySpeed ||
                IsHit || _brain.Combat.IsReloading || Time.time < _fireUntil || _animator.IsInTransition(0) ||
                !_animator.GetCurrentAnimatorStateInfo(0).IsTag("Locomotion")) return;
            if (Time.time - _turnCandidateAt < _config.TurnStableTime) return;
            float angle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);
            if (Mathf.Abs(angle) < _config.Turn90Angle) return;
            bool large = _largeTurnCandidate;
            bool left = angle < 0f;
            Parameter turn = large ? (left ? Parameter.Turn180Left : Parameter.Turn180Right) : (left ? Parameter.Turn90Left : Parameter.Turn90Right);
            bool combat = _brain.States.Group != EnemyStateGroup.Passive;
            Vector4 durations = IsCrouching ? _config.CrouchTurnDurations : combat ? _config.CombatTurnDurations : _config.SteadyTurnDurations;
            int index = large ? (left ? 2 : 3) : (left ? 0 : 1);
            var profile = _config.GetTurnProfile((IsCrouching ? "Crouch" : combat ? "Combat" : "Steady") + turn);
            float duration = profile != null ? profile.Duration : durations[index];
            if (!_navigation.BeginTurn(direction, duration, profile != null)) return;
            _activeTurnProfile = profile; _turnAnimationStarted = false;
            _pendingTurn = Vector3.zero;
            ClearTurnTriggers(); Trigger(turn); CurrentTurn = turn.ToString();
            _nextTurn = Time.time + duration + _config.TurnCooldown;
            if (_logTransitions) Debug.Log($"[Enemy:{name}][Animation] {EnemyAnimationStateNames.Get(_animator.GetCurrentAnimatorStateInfo(0).fullPathHash)} -> {(IsCrouching ? "Crouch" : combat ? "Combat" : "Steady")}/{CurrentTurn} | DesiredYawDelta: {angle:0.0} deg | Duration: {duration:0.00} s | Reason: Facing requested", this);
        }
        private void UpdateAim()
        {
            if (_aimTarget == null) return;
            EnemyBlackboard memory = _brain.Memory;
            Vector3 desired = _actor.Eyes.position + transform.forward * _config.RestAimDistance;
            if (memory.Visible && memory.Target != null && memory.Target.IsAlive) desired = memory.Target.AimPosition;
            else if (memory.HasContact) desired = memory.LastKnownPosition + Vector3.up * (_actor.Eyes.position.y - transform.position.y);
            _smoothedAimPosition = Vector3.Lerp(_smoothedAimPosition, desired, 1f - Mathf.Exp(-_config.AimSmoothing * Time.deltaTime));
            _aimTarget.position = _smoothedAimPosition;
        }
        private void LateUpdate()
        {
            ApplyPresentationTurn();
            PublishSteadyMovementPhase();
            PublishCombatMovementReadiness();
            // The Animator can write the rig target's bind pose after Update. Publish the target
            // after evaluation so the next rig synchronization reads the gameplay target.
            if (_bound && _animator.enabled && !_actor.Health.IsDead) UpdateAim();
            if (_leftFoot != null && _rightFoot != null && Time.time >= _nextFootSample)
            {
                _nextFootSample = Time.time + .05f;
                Vector3 left = transform.InverseTransformPoint(_leftFoot.position), right = transform.InverseTransformPoint(_rightFoot.position);
                FootMotionDetected = Vector3.Distance(left, _lastLeftFoot) + Vector3.Distance(right, _lastRightFoot) > .003f;
                _lastLeftFoot = left; _lastRightFoot = right;
            }
        }
        private void PublishCombatMovementReadiness()
        {
            if (!_bound || !_modern || !_animator.enabled) return;
            var current = _animator.GetCurrentAnimatorStateInfo(0);
            bool ready = current.IsName("StandingLocomotion") || current.IsName("CrouchLocomotion") || current.IsTag("Turn") && _brain.States.Group != EnemyStateGroup.Passive;
            if (_animator.IsInTransition(0) && _animator.GetNextAnimatorStateInfo(0).IsName("StandingLocomotion"))
                ready |= _animator.GetAnimatorTransitionInfo(0).normalizedTime >= CombatLocomotionReadyBlend;
            _navigation.SetCombatMovementReady(ready);
        }
        private void PublishSteadyMovementPhase()
        {
            if (!_bound || !_animator.enabled || !_modern) { _navigation.ClearSteadyAnimationPhase(); return; }
            var current = _animator.GetCurrentAnimatorStateInfo(0);
            bool transition = _animator.IsInTransition(0);
            var next = _animator.GetNextAnimatorStateInfo(0);
            float blend = transition ? Mathf.Clamp01(_animator.GetAnimatorTransitionInfo(0).normalizedTime) : 0f;
            float StartFraction(AnimatorStateInfo state) => state.IsName("SteadyStartWalk") ? _brain.Config.Movement.SteadyStartSpeedAt(state.normalizedTime) : state.IsName("SteadyWalk") ? 1f : 0f;
            float StopFraction(AnimatorStateInfo state) => state.IsName("SteadyStopWalk") ? _brain.Config.Movement.SteadyStopSpeedAt(state.normalizedTime) : state.IsName("SteadyWalk") || state.IsName("SteadyStartWalk") ? 1f : 0f;
            float start = StartFraction(current), stop = StopFraction(current);
            if (transition) { start = Mathf.Lerp(start, StartFraction(next), blend); stop = Mathf.Lerp(stop, StopFraction(next), blend); }
            bool rest = current.IsName("SteadyIdle") && !transition || current.IsName("SteadyStopWalk") && stop <= .001f;
            _navigation.SetSteadyAnimationPhase(start, stop, current.IsName("SteadyWalk"), rest);
        }
        private void ApplyPresentationTurn()
        {
            if (!_bound || !_animator.enabled || !_navigation.IsTurning || _activeTurnProfile == null) return;
            var current = _animator.GetCurrentAnimatorStateInfo(0);
            var next = _animator.GetNextAnimatorStateInfo(0);
            if (current.IsTag("Turn") || _animator.IsInTransition(0) && next.IsTag("Turn"))
            {
                float time = current.IsTag("Turn") ? current.normalizedTime : next.normalizedTime;
                _turnAnimationStarted = true;
                _navigation.ApplyAnimatedTurn(_activeTurnProfile.Evaluate(time), time >= 1f);
            }
            else if (_turnAnimationStarted) _navigation.ApplyAnimatedTurn(1f, true);
        }
        public void SetCrouching(bool crouching) => _stance?.SetCrouching(crouching);
        public void SetAnimationLogging(bool enabled) => _logTransitions = enabled;
        private void Fire()
        {
            if (_actor.Health.IsDead || !_animator.enabled) return;
            if (_logTransitions) Debug.Log($"[Enemy:{name}][Animation] {EnemyAnimationStateNames.Get(_animator.GetCurrentAnimatorStateInfo(0).fullPathHash)} -> Recoil/{(IsCrouching ? "CrouchFire" : "Fire")} | Velocity: {_navigation.Velocity.magnitude:0.00} m/s | IsCrouching: {IsCrouching} | Reason: Weapon fired", this);
            _fireUntil = Time.time + _config.FireHoldDuration; Bool(Parameter.IsFiring, true); Trigger(Parameter.Attack);
        }
        private void Reload()
        {
            if (_actor.Health.IsDead || !_animator.enabled) return;
            _fireUntil = 0f; Bool(Parameter.IsFiring, false); Bool(Parameter.IsReloading, true); Bool(Parameter.Reloading, true);
            ClearTurnTriggers(); _navigation.CancelTurn();
        }
        private void ReceiveDamage(DamageInfo damage)
        {
            Vector3 direction = damage.Direction;
            if (direction.sqrMagnitude < 0.0001f && damage.Source != null) direction = transform.position - damage.Source.transform.position;
            bool lethal = _actor.Health.CurrentHealth <= 0f;
            float value = EnemyAnimationMath.HitDirection(transform.rotation, direction, lethal);
            if (lethal)
            {
                // DamageReceived precedes gameplay death and its navigation stop.
                // Capture real motion here, independently of requested tier or stance.
                _lethalDirection = value;
                _deathVelocity = Vector3.ProjectOnPlane(_navigation.Velocity, Vector3.up);
                DeathPlanarSpeed = _deathVelocity.magnitude;
                return;
            }
            if (_actor.Health.IsDead || !_animator.enabled) return;
            if (IsHit) return;
            Trigger(Parameter.Hit); Float(Parameter.HitDirection, value, 0f); _hitUntil = Time.time + 0.48f; Bool(Parameter.IsHit, true);
            ClearTurnTriggers(); _navigation.CancelTurn();
        }
        private void Die()
        {
            _navigation.CancelTurn(); CurrentTurn = null;
            bool animateDeath = _animator.enabled && _modern && DeathPlanarSpeed <= _config.DeathAnimationMaxSpeed;
            _ragdoll?.BeginDeath(_deathVelocity, animateDeath);
            _fireUntil = _hitUntil = 0f; ClearTurnTriggers();
            if (Has(Parameter.Hit)) _animator.ResetTrigger(Hashes[(int)Parameter.Hit]);
            // Moving deaths never set a terminal Animator parameter or evaluate Death.
            if (!_animator.enabled || _modern && !animateDeath) return;
            Bool(Parameter.IsFiring, false); Bool(Parameter.IsReloading, false); Bool(Parameter.IsHit, false); Bool(Parameter.Reloading, false);
            Bool(Parameter.IsCrouching, IsCrouching);
            Float(Parameter.HitDirection, _lethalDirection, 0f); Bool(Parameter.IsDead, true); Bool(Parameter.Dead, true);
            if (_modern) for (int layer = 1; layer < _animator.layerCount; layer++) _animator.SetLayerWeight(layer, 0f);
        }
        private void ResetView()
        {
            _stance?.SetCrouching(false); _navigation.CancelTurn(); ClearTurnTriggers(); CurrentTurn = null;
            MoveSpeedRaw = MoveSpeedSmoothed = 0f; MoveDirectionRaw = MoveDirectionSmoothed = Vector2.zero;
            _pendingTurn = Vector3.zero;
            _activeTurnProfile = null; _turnAnimationStarted = false;
            _nextFootSample = 0f; FootMotionDetected = false;
            _nextTurn = _fireUntil = _hitUntil = _lethalDirection = DeathPlanarSpeed = 0f; _deathVelocity = Vector3.zero; _lastStateHash = _lastActionHash = _lastRecoilHash = 0; _lastLoggedMode = _brain.States.Group;
            _ragdoll?.ResetPresentation(); _animator.speed = 1f; _animator.Rebind();
            foreach (var parameter in _animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger) _animator.ResetTrigger(parameter.nameHash);
                else if (parameter.type == AnimatorControllerParameterType.Bool) _animator.SetBool(parameter.nameHash, parameter.defaultBool);
                else if (parameter.type == AnimatorControllerParameterType.Float) _animator.SetFloat(parameter.nameHash, parameter.defaultFloat);
                else if (parameter.type == AnimatorControllerParameterType.Int) _animator.SetInteger(parameter.nameHash, parameter.defaultInt);
            }
            if (_modern)
            {
                for (int layer = 1; layer < _animator.layerCount; layer++) _animator.SetLayerWeight(layer, 0f);
                Bool(Parameter.IsCombat, _brain.States.Group != EnemyStateGroup.Passive);
                _animator.Play("Base Layer.SteadyIdle", 0, 0f); _animator.Play("Actions.None", 1, 0f); if (_animator.layerCount > 2) _animator.Play("Recoil.None", 2, 0f);
            }
            _animator.Update(0f);
            if (_aimTarget != null) _aimTarget.position = _smoothedAimPosition = _actor.Eyes.position + transform.forward * _config.RestAimDistance;
        }
        private bool Has(Parameter parameter) => _available[(int)parameter];
        private void Float(Parameter parameter, float value, float damping)
        {
            if (!Has(parameter)) return;
            if (damping <= 0f) _animator.SetFloat(Hashes[(int)parameter], value);
            else _animator.SetFloat(Hashes[(int)parameter], value, damping, Time.deltaTime);
        }
        private void Bool(Parameter parameter, bool value) { if (Has(parameter)) _animator.SetBool(Hashes[(int)parameter], value); }
        private void Trigger(Parameter parameter) { if (Has(parameter)) _animator.SetTrigger(Hashes[(int)parameter]); }
        private void ClearTurnTriggers()
        { for (int i = (int)Parameter.Turn90Left; i <= (int)Parameter.Turn180Right; i++) if (_available[i]) _animator.ResetTrigger(Hashes[i]); }
        private void OnDisable()
        {
            _navigation?.ClearSteadyAnimationPhase();
            if (!_bound) return;
            _brain.ResetCompleted -= ResetView; _navigation.FacingRequested -= PresentTurn;
            _brain.Combat.Fired -= Fire; _brain.Combat.ReloadStarted -= Reload;
            _actor.Health.DamageReceived -= ReceiveDamage; _brain.DeathCompleted -= Die; _bound = false;
        }
    }
}
