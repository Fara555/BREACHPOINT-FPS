using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    [RequireComponent(typeof(EnemyBrain))]
    public sealed class EnemyAnimationBridge : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private string _speedParameter = "Speed";
        [SerializeField] private string _deadParameter = "Dead";
        [SerializeField] private string _reloadParameter = "Reloading";
        [SerializeField] private string _attackParameter = "Attack";
        private EnemyBrain _brain;
        private EnemyNavigation _navigation;
        private int _speed, _dead, _reload, _attack;
        private bool _hasSpeed, _hasDead, _hasReload, _hasAttack;
        private bool _bound;
        private void Awake() { _brain = GetComponent<EnemyBrain>(); _navigation = GetComponent<EnemyNavigation>(); }
        private void Start() => Bind();
        private void OnEnable() { if (_brain != null) Bind(); }
        private void Bind()
        {
            if (_bound || _brain.Combat == null || _animator == null || _animator.runtimeAnimatorController == null) return;
            _speed = Animator.StringToHash(_speedParameter); _dead = Animator.StringToHash(_deadParameter);
            _reload = Animator.StringToHash(_reloadParameter); _attack = Animator.StringToHash(_attackParameter);
            foreach (var p in _animator.parameters)
            {
                _hasSpeed |= p.nameHash == _speed && p.type == AnimatorControllerParameterType.Float;
                _hasDead |= p.nameHash == _dead && p.type == AnimatorControllerParameterType.Bool;
                _hasReload |= p.nameHash == _reload && p.type == AnimatorControllerParameterType.Bool;
                _hasAttack |= p.nameHash == _attack && p.type == AnimatorControllerParameterType.Trigger;
            }
            _animator.applyRootMotion = false;
            _brain.Combat.Fired += Fire; _brain.ResetCompleted += ResetView; _bound = true;
        }
        private void Update()
        {
            if (!_bound) return;
            if (_hasSpeed) _animator.SetFloat(_speed, _navigation.Velocity.magnitude, 0.1f, Time.deltaTime);
            if (_hasDead) _animator.SetBool(_dead, _brain.States.Current == EnemyStateId.Dead);
            if (_hasReload) _animator.SetBool(_reload, _brain.Combat.IsReloading);
        }
        private void Fire() { if (_hasAttack) _animator.SetTrigger(_attack); }
        private void ResetView() { _animator.Rebind(); _animator.Update(0f); }
        private void OnDisable()
        {
            if (!_bound) return;
            _brain.Combat.Fired -= Fire; _brain.ResetCompleted -= ResetView;
            _bound = false;
        }
    }
}
