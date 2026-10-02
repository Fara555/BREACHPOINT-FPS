using Breachpoint.Gameplay.Combat;
using UnityEngine;

namespace Breachpoint.Gameplay.AI
{
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyBrain))]
    public sealed class EnemyAnimationBridge : MonoBehaviour
    {
        private enum Parameter { MoveSpeed, MoveX, MoveY, IsCombat, IsCrouching, IsFiring, IsReloading, IsHit, IsDead, HitDirection, ReloadSpeed, Turn90Left, Turn90Right, Turn180Left, Turn180Right, Hit, Speed, Dead, Reloading, Attack }
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
        private int _lastStateHash;
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
            _animator.applyRootMotion = false;
            // Hitscan and shot VFX use the bone-attached muzzle even outside the camera.
            if (_modern) _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _brain.ResetCompleted += ResetView; _navigation.FacingRequested += PresentTurn;
            _brain.Combat.Fired += Fire; _brain.Combat.ReloadStarted += Reload;
            _actor.Health.DamageReceived += ReceiveDamage; _actor.Health.Died += Die;
            _bound = true; ResetView();
        }
        private void Update()
        {
            if (!_bound) Bind();
            if (!_bound || !_animator.enabled || _actor.Health.IsDead) return;
            Vector3 velocity = _navigation.Velocity; velocity.y = 0f;
            EnemyMovementConfig movement = _brain.Config.Movement;
            float speed = velocity.magnitude;
            Float(Parameter.MoveSpeed, EnemyAnimationMath.MoveSpeed(speed, movement.WalkSpeed, movement.RunSpeed, _config), speed > _config.StationarySpeed ? _config.FloatDamping : 0.04f);
            Float(Parameter.Speed, speed, _config.FloatDamping);
            Vector2 local = EnemyAnimationMath.LocalMovement(transform.rotation, velocity, IsCrouching ? movement.CrouchSpeed : Mathf.Max(movement.WalkSpeed, _navigation.DesiredSpeed));
            Float(Parameter.MoveX, local.x, _config.FloatDamping); Float(Parameter.MoveY, local.y, _config.FloatDamping);
            Bool(Parameter.IsCombat, _brain.States.Group != EnemyStateGroup.Passive || IsCrouching);
            Bool(Parameter.IsCrouching, IsCrouching);
            Bool(Parameter.IsFiring, !_brain.Combat.IsReloading && Time.time < _fireUntil);
            Bool(Parameter.IsReloading, _brain.Combat.IsReloading); Bool(Parameter.Reloading, _brain.Combat.IsReloading);
            Bool(Parameter.IsHit, IsHit);
            Float(Parameter.ReloadSpeed, 1f / Mathf.Max(0.01f, _brain.Config.Combat.ReloadDuration), 0f);
            if (speed > _config.StationarySpeed || IsHit || _brain.Combat.IsReloading)
            { ClearTurnTriggers(); _navigation.CancelTurn(); CurrentTurn = null; }
            else if (!_navigation.IsTurning) CurrentTurn = null;
            if (_modern)
            {
                AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(1);
                AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(1);
                bool active = !current.IsTag("None") || (_animator.IsInTransition(1) && !next.IsTag("None"));
                if (_animator.IsInTransition(1) && next.IsTag("None")) active = false;
                // An empty Humanoid override layer still contributes default muscles at weight 1.
                // Fade it out whenever no action owns the upper body.
                _animator.SetLayerWeight(1, Mathf.MoveTowards(_animator.GetLayerWeight(1), active ? 1f : 0f, Time.deltaTime / 0.06f));
            }
            UpdateAim();
            if (_logTransitions)
            {
                int hash = _animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
                if (hash != _lastStateHash)
                { Debug.Log($"[Enemy:{name}][Animation] {_lastStateHash} -> {hash}; mode={_brain.States.Group}, velocity={speed:0.00}", this); _lastStateHash = hash; }
            }
        }
        private void PresentTurn(Vector3 direction)
        {
            if (!_bound || !_modern || !_animator.enabled || _actor.Health.IsDead || _navigation.IsTurning ||
                Time.time < _nextTurn || _navigation.Velocity.sqrMagnitude > _config.StationarySpeed * _config.StationarySpeed ||
                IsHit || _brain.Combat.IsReloading || Time.time < _fireUntil || _animator.IsInTransition(0) ||
                !_animator.GetCurrentAnimatorStateInfo(0).IsTag("Locomotion")) return;
            float angle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);
            if (Mathf.Abs(angle) < _config.Turn90Angle) return;
            bool large = Mathf.Abs(angle) >= _config.Turn180Angle;
            bool left = angle < 0f;
            Parameter turn = large ? (left ? Parameter.Turn180Left : Parameter.Turn180Right) : (left ? Parameter.Turn90Left : Parameter.Turn90Right);
            bool combat = _brain.States.Group != EnemyStateGroup.Passive;
            Vector4 durations = IsCrouching ? _config.CrouchTurnDurations : combat ? _config.CombatTurnDurations : _config.SteadyTurnDurations;
            int index = large ? (left ? 2 : 3) : (left ? 0 : 1);
            if (!_navigation.BeginTurn(direction, durations[index])) return;
            ClearTurnTriggers(); Trigger(turn); CurrentTurn = turn.ToString();
            _nextTurn = Time.time + durations[index] + _config.TurnCooldown;
            if (_logTransitions) Debug.Log($"[Enemy:{name}][Animation] {CurrentTurn}; reason=facing error {angle:0.0} degrees", this);
        }
        private void UpdateAim()
        {
            if (_aimTarget == null) return;
            EnemyBlackboard memory = _brain.Memory;
            Vector3 desired = _actor.Eyes.position + transform.forward * _config.RestAimDistance;
            if (memory.Visible && memory.Target != null && memory.Target.IsAlive) desired = memory.Target.AimPosition;
            else if (memory.HasContact) desired = memory.LastKnownPosition + Vector3.up * (_actor.Eyes.position.y - transform.position.y);
            _aimTarget.position = Vector3.Lerp(_aimTarget.position, desired, 1f - Mathf.Exp(-_config.AimSmoothing * Time.deltaTime));
        }
        public void SetCrouching(bool crouching) => _stance?.SetCrouching(crouching);
        public void SetAnimationLogging(bool enabled) => _logTransitions = enabled;
        private void Fire()
        {
            if (_actor.Health.IsDead || !_animator.enabled) return;
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
            if (lethal) { _lethalDirection = value; _deathVelocity = _navigation.Velocity; return; }
            if (_actor.Health.IsDead || !_animator.enabled) return;
            if (IsHit) return;
            Trigger(Parameter.Hit); Float(Parameter.HitDirection, value, 0f); _hitUntil = Time.time + 0.48f; Bool(Parameter.IsHit, true);
            ClearTurnTriggers(); _navigation.CancelTurn();
        }
        private void Die()
        {
            _navigation.CancelTurn(); CurrentTurn = null;
            _ragdoll?.BeginDeath(_deathVelocity, _animator.enabled && _modern);
            _fireUntil = _hitUntil = 0f; ClearTurnTriggers();
            if (Has(Parameter.Hit)) _animator.ResetTrigger(Hashes[(int)Parameter.Hit]);
            if (!_animator.enabled) return;
            Bool(Parameter.IsFiring, false); Bool(Parameter.IsReloading, false); Bool(Parameter.IsHit, false); Bool(Parameter.Reloading, false);
            Bool(Parameter.IsCrouching, IsCrouching);
            Float(Parameter.HitDirection, _lethalDirection, 0f); Bool(Parameter.IsDead, true); Bool(Parameter.Dead, true);
            if (_modern) _animator.SetLayerWeight(1, 0f);
        }
        private void ResetView()
        {
            _stance?.SetCrouching(false); _navigation.CancelTurn(); ClearTurnTriggers(); CurrentTurn = null;
            _nextTurn = _fireUntil = _hitUntil = _lethalDirection = 0f; _deathVelocity = Vector3.zero; _lastStateHash = 0;
            _ragdoll?.ResetPresentation(); _animator.Rebind();
            foreach (var parameter in _animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger) _animator.ResetTrigger(parameter.nameHash);
                else if (parameter.type == AnimatorControllerParameterType.Bool) _animator.SetBool(parameter.nameHash, parameter.defaultBool);
                else if (parameter.type == AnimatorControllerParameterType.Float) _animator.SetFloat(parameter.nameHash, parameter.defaultFloat);
                else if (parameter.type == AnimatorControllerParameterType.Int) _animator.SetInteger(parameter.nameHash, parameter.defaultInt);
            }
            if (_modern)
            {
                _animator.SetLayerWeight(1, 0f);
                Bool(Parameter.IsCombat, _brain.States.Group != EnemyStateGroup.Passive);
                _animator.Play("Base Layer.SteadyIdle", 0, 0f); _animator.Play("Actions.None", 1, 0f);
            }
            _animator.Update(0f);
            if (_aimTarget != null) _aimTarget.position = _actor.Eyes.position + transform.forward * _config.RestAimDistance;
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
            if (!_bound) return;
            _brain.ResetCompleted -= ResetView; _navigation.FacingRequested -= PresentTurn;
            _brain.Combat.Fired -= Fire; _brain.Combat.ReloadStarted -= Reload;
            _actor.Health.DamageReceived -= ReceiveDamage; _actor.Health.Died -= Die; _bound = false;
        }
    }
}
