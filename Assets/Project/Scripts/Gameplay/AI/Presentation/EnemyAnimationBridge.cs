using UnityEngine;
using Breachpoint.Gameplay.Combat;

namespace Breachpoint.Gameplay.AI
{
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyBrain))]
    public sealed class EnemyAnimationBridge : MonoBehaviour
    {
        private enum Parameter { MoveSpeed, MoveX, MoveY, IsCrouching, StartRun, StopRun, Turn90Left, Turn90Right, Turn180Left, Turn180Right, IsFiring, Reload, HitDirection, Hit, IsDead, IsHeadshot, Speed, Dead, Reloading, Attack }
        private static readonly int[] Hashes = CreateHashes();
        private static readonly int SteadyEntry = Animator.StringToHash("Base Layer.Steady.Idle Steady");
        private static readonly int CombatEntry = Animator.StringToHash("Base Layer.Combat.StandingLocomotion");
        private static readonly int CrouchEntry = Animator.StringToHash("Base Layer.Combat.CrouchLocomotion");
        private static readonly int DeathEntry = Animator.StringToHash("Base Layer.Combat.Death.DeathTree");
        private static readonly int CrouchDeathEntry = Animator.StringToHash("Base Layer.Combat.Death.DeathCrouching");
        private static readonly int HeadshotDeathEntry = Animator.StringToHash("Base Layer.Combat.Death.DeathHeadshotTree");

        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _aimTarget;
        [SerializeField] private EnemyAnimationConfig _config = new EnemyAnimationConfig();
        private readonly bool[] _available = new bool[Hashes.Length];
        private EnemyBrain _brain;
        private EnemyActor _actor;
        private EnemyNavigation _navigation;
        private EnemyRagdollPresenter _ragdoll;
        private bool _bound;
        private bool _adam;
        private bool _combatMode;
        private bool _crouching;
        private bool _wasMoving;
        private bool _runStarted;
        private float _nextTurn;
        private bool _hasFacingRequest;
        private Vector3 _facingRequest;
        private float _fireUntil;
        private float _lethalDirection;
        private Vector3 _deathVelocity;
        private bool _deathRequested;

        private static int[] CreateHashes()
        {
            string[] names =
            {
                nameof(Parameter.MoveSpeed), nameof(Parameter.MoveX), nameof(Parameter.MoveY), nameof(Parameter.IsCrouching),
                nameof(Parameter.StartRun), nameof(Parameter.StopRun), nameof(Parameter.Turn90Left), nameof(Parameter.Turn90Right),
                nameof(Parameter.Turn180Left), nameof(Parameter.Turn180Right), nameof(Parameter.IsFiring), nameof(Parameter.Reload),
                nameof(Parameter.HitDirection), nameof(Parameter.Hit), nameof(Parameter.IsDead), nameof(Parameter.IsHeadshot),
                nameof(Parameter.Speed), nameof(Parameter.Dead), nameof(Parameter.Reloading), nameof(Parameter.Attack)
            };
            int[] hashes = new int[names.Length];
            for (int i = 0; i < names.Length; i++) hashes[i] = Animator.StringToHash(names[i]);
            return hashes;
        }

        private void Awake()
        {
            _brain = GetComponent<EnemyBrain>();
            _actor = GetComponent<EnemyActor>();
            _navigation = GetComponent<EnemyNavigation>();
            _ragdoll = GetComponent<EnemyRagdollPresenter>();
        }

        private void Start() => Bind();
        private void OnEnable() { if (_brain != null) Bind(); }

        private void Bind()
        {
            if (_bound || _brain.Combat == null || _animator == null || _animator.runtimeAnimatorController == null) return;
            System.Array.Clear(_available, 0, _available.Length);
            foreach (var parameter in _animator.parameters)
                for (int i = 0; i < Hashes.Length; i++)
                    if (parameter.nameHash == Hashes[i] && parameter.type == ExpectedType((Parameter)i)) _available[i] = true;
            _adam = Has(Parameter.MoveSpeed) && _animator.HasState(0, SteadyEntry) && _animator.HasState(0, CombatEntry);
            _animator.applyRootMotion = false;
            _brain.ResetCompleted += ResetView;
            _navigation.FacingRequested += RecordFacing;
            _brain.Combat.Fired += Fire;
            _brain.Combat.ReloadStarted += Reload;
            _actor.Health.DamageReceived += ReceiveDamage;
            _actor.Health.Died += Die;
            _bound = true;
            ResetView();
        }

        private static AnimatorControllerParameterType ExpectedType(Parameter parameter)
        {
            switch (parameter)
            {
                case Parameter.MoveSpeed: case Parameter.MoveX: case Parameter.MoveY: case Parameter.HitDirection: case Parameter.Speed:
                    return AnimatorControllerParameterType.Float;
                case Parameter.IsCrouching: case Parameter.IsFiring: case Parameter.IsDead: case Parameter.IsHeadshot:
                case Parameter.Dead: case Parameter.Reloading:
                    return AnimatorControllerParameterType.Bool;
                default: return AnimatorControllerParameterType.Trigger;
            }
        }

        private void Update()
        {
            if (!_bound) Bind();
            if (!_bound || !_animator.enabled) return;
            if (_actor.Health.IsDead) { ConsumeDeathRequest(); return; }
            UpdateMode();
            UpdateLocomotion();
            UpdateAim();
            Bool(Parameter.IsFiring, _brain.Combat.IsAiming && !_brain.Combat.IsReloading && Time.time < _fireUntil);
            Bool(Parameter.Reloading, _brain.Combat.IsReloading);
            _hasFacingRequest = false;
        }

        private void UpdateMode()
        {
            bool combat = _brain.States.Group != EnemyStateGroup.Passive || _crouching;
            if (!_adam || combat == _combatMode) return;
            // Authored hit/reload/fire transitions finish in Combat; preserve their playback.
            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            if (_animator.IsInTransition(0) || !(state.IsName("Idle Steady") || state.IsName("SteadyWalk") || state.IsName("Start Walk") || state.IsName("Stop Walk") || state.IsName("StandingLocomotion") || state.IsName("CrouchLocomotion"))) return;
            _combatMode = combat;
            ResetMovementTriggers();
            _runStarted = false;
            _animator.CrossFadeInFixedTime(combat ? (_crouching ? CrouchEntry : CombatEntry) : SteadyEntry, _config.ModeBlendDuration, 0);
        }

        private void UpdateLocomotion()
        {
            Vector3 velocity = _navigation.Velocity;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            EnemyMovementConfig movement = _brain.Config.Movement;
            Float(Parameter.MoveSpeed, EnemyAnimationMath.MoveSpeed(speed, movement.WalkSpeed, movement.RunSpeed, _config));
            Float(Parameter.Speed, speed);
            Vector2 local = EnemyAnimationMath.LocalMovement(transform.rotation, velocity, _crouching ? movement.WalkSpeed : Mathf.Max(movement.WalkSpeed, _navigation.DesiredSpeed));
            Float(Parameter.MoveX, local.x);
            Float(Parameter.MoveY, local.y);
            Bool(Parameter.IsCrouching, _crouching);
            bool moving = speed > _config.StationarySpeed;
            bool run = moving && !_crouching && _combatMode && _navigation.DesiredSpeed > movement.WalkSpeed && speed >= movement.RunSpeed * _config.RunEnterRatio;
            if (run && !_runStarted) { Trigger(Parameter.StartRun); _runStarted = true; }
            if (_runStarted && !moving) { Trigger(Parameter.StopRun); _runStarted = false; }
            if (_crouching || !_combatMode) _runStarted = false;
            if (moving) ClearTurnTriggers();
            else if (_hasFacingRequest) PresentTurn(_facingRequest, true);
            else if (!_wasMoving && _navigation.DesiredVelocity.sqrMagnitude > _config.StationarySpeed * _config.StationarySpeed)
                PresentTurn(_navigation.DesiredVelocity);
            _wasMoving = moving;
        }

        private void RecordFacing(Vector3 direction)
        {
            // Capture the correction before navigation applies gameplay rotation.
            _facingRequest = Quaternion.Inverse(transform.rotation) * direction;
            _hasFacingRequest = true;
        }

        private void PresentTurn(Vector3 direction, bool local = false)
        {
            if (Has(Parameter.MoveSpeed) && _animator.GetFloat(Hashes[(int)Parameter.MoveSpeed]) >= 0.05f) return;
            if (Time.time < _nextTurn || _animator.IsInTransition(0) || (_adam && !_animator.GetCurrentAnimatorStateInfo(0).IsName(_combatMode ? (_crouching ? "CrouchLocomotion" : "StandingLocomotion") : "Idle Steady"))) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            float angle = Vector3.SignedAngle(local ? Vector3.forward : transform.forward, direction, Vector3.up);
            if (Mathf.Abs(angle) < _config.Turn90Angle) return;
            ClearTurnTriggers();
            bool largeTurn = Mathf.Abs(angle) >= _config.Turn180Angle;
            Trigger(angle < 0f ? (largeTurn ? Parameter.Turn180Left : Parameter.Turn90Left) : (largeTurn ? Parameter.Turn180Right : Parameter.Turn90Right));
            _nextTurn = Time.time + _config.TurnCooldown;
        }

        private void UpdateAim()
        {
            if (_aimTarget == null) return;
            EnemyBlackboard memory = _brain.Memory;
            Vector3 desired = _actor.Eyes.position + transform.forward * _config.RestAimDistance;
            if (memory.Visible && memory.Target != null && memory.Target.IsAlive) desired = memory.Target.AimPosition;
            else if (memory.HasContact) desired = memory.LastKnownPosition + Vector3.up * (_actor.Eyes.position.y - transform.position.y);
            float blend = 1f - Mathf.Exp(-_config.AimSmoothing * Time.deltaTime);
            _aimTarget.position = Vector3.Lerp(_aimTarget.position, desired, blend);
        }

        // A presentation input for a gameplay stance owner; the current AI always stands.
        public void SetCrouching(bool crouching) => _crouching = crouching;

        private void Fire()
        {
            if (_actor.Health.IsDead || !_animator.enabled) return;
            _fireUntil = Time.time + _config.FireHoldDuration;
            Bool(Parameter.IsFiring, true);
            Trigger(Parameter.Attack);
        }

        private void Reload()
        {
            if (_actor.Health.IsDead || !_animator.enabled) return;
            _fireUntil = 0f;
            Bool(Parameter.IsFiring, false);
            Trigger(Parameter.Reload);
        }

        private void ReceiveDamage(DamageInfo damage)
        {
            Vector3 direction = damage.Direction;
            if (direction.sqrMagnitude < 0.0001f && damage.Source != null)
                direction = transform.position - damage.Source.transform.position;
            bool lethal = _actor.Health.CurrentHealth <= 0f;
            float value = EnemyAnimationMath.HitDirection(transform.rotation, direction, lethal);
            if (lethal) { _lethalDirection = value; _deathVelocity = _navigation.Velocity; return; }
            if (_actor.Health.IsDead || !_animator.enabled) return;
            if (Has(Parameter.HitDirection)) _animator.SetFloat(Hashes[(int)Parameter.HitDirection], value);
            Trigger(Parameter.Hit);
        }

        private void Die()
        {
            bool hasDeathAnimation = _animator.enabled && _adam && _animator.HasState(0, _crouching ? CrouchDeathEntry : DeathEntry);
            _ragdoll?.BeginDeath(_deathVelocity, hasDeathAnimation);
            if (!_animator.enabled) return;
            _fireUntil = 0f;
            ResetMovementTriggers();
            ClearTrigger(Parameter.Hit); ClearTrigger(Parameter.Reload); ClearTrigger(Parameter.Attack);
            Bool(Parameter.IsFiring, false); Bool(Parameter.Reloading, false);
            Bool(Parameter.IsHeadshot, false);
            Bool(Parameter.IsCrouching, _crouching);
            if (Has(Parameter.HitDirection)) _animator.SetFloat(Hashes[(int)Parameter.HitDirection], _lethalDirection);
            Bool(Parameter.IsDead, true); Bool(Parameter.Dead, true);
            _deathRequested = true;
        }

        private void ConsumeDeathRequest()
        {
            if (!_deathRequested || !_adam) return;
            AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(0);
            if (!IsDeathState(current.fullPathHash) && !IsDeathState(next.fullPathHash)) return;
            // The authored Any State transition permits self re-entry. Consume its request
            // once entered so the death clip advances; gameplay remains terminally Dead.
            Bool(Parameter.IsDead, false);
            _deathRequested = false;
        }

        private static bool IsDeathState(int hash) => hash == DeathEntry || hash == CrouchDeathEntry || hash == HeadshotDeathEntry;

        private void ResetView()
        {
            _crouching = _wasMoving = _runStarted = _hasFacingRequest = false;
            _nextTurn = 0f;
            _fireUntil = _lethalDirection = 0f;
            _deathVelocity = Vector3.zero;
            _deathRequested = false;
            _combatMode = _brain.States.Group != EnemyStateGroup.Passive;
            _ragdoll?.ResetPresentation();
            _animator.Rebind();
            foreach (var parameter in _animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger) _animator.ResetTrigger(parameter.nameHash);
                else if (parameter.type == AnimatorControllerParameterType.Bool) _animator.SetBool(parameter.nameHash, parameter.defaultBool);
                else if (parameter.type == AnimatorControllerParameterType.Float) _animator.SetFloat(parameter.nameHash, parameter.defaultFloat);
                else if (parameter.type == AnimatorControllerParameterType.Int) _animator.SetInteger(parameter.nameHash, parameter.defaultInt);
            }
            Bool(Parameter.IsDead, false); Bool(Parameter.Dead, false); Bool(Parameter.IsHeadshot, false);
            if (_adam) _animator.Play(_combatMode ? CombatEntry : SteadyEntry, 0, 0f);
            _animator.Update(0f);
            if (_aimTarget != null) _aimTarget.position = _actor.Eyes.position + transform.forward * _config.RestAimDistance;
        }

        private bool Has(Parameter parameter) => _available[(int)parameter];
        private void Float(Parameter parameter, float value) { if (Has(parameter)) _animator.SetFloat(Hashes[(int)parameter], value, _config.FloatDamping, Time.deltaTime); }
        private void Bool(Parameter parameter, bool value) { if (Has(parameter)) _animator.SetBool(Hashes[(int)parameter], value); }
        private void Trigger(Parameter parameter) { if (Has(parameter)) _animator.SetTrigger(Hashes[(int)parameter]); }
        private void ClearTrigger(Parameter parameter) { if (Has(parameter)) _animator.ResetTrigger(Hashes[(int)parameter]); }
        private void ClearTurnTriggers()
        {
            ClearTrigger(Parameter.Turn90Left); ClearTrigger(Parameter.Turn90Right);
            ClearTrigger(Parameter.Turn180Left); ClearTrigger(Parameter.Turn180Right);
        }
        private void ResetMovementTriggers() { ClearTurnTriggers(); ClearTrigger(Parameter.StartRun); ClearTrigger(Parameter.StopRun); }

        private void OnDisable()
        {
            if (!_bound) return;
            _brain.ResetCompleted -= ResetView;
            _navigation.FacingRequested -= RecordFacing;
            _brain.Combat.Fired -= Fire;
            _brain.Combat.ReloadStarted -= Reload;
            _actor.Health.DamageReceived -= ReceiveDamage;
            _actor.Health.Died -= Die;
            _bound = false;
        }
    }
}
