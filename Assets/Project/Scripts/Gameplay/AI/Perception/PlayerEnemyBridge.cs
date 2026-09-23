using Breachpoint.Gameplay.Player.Movement;
using Breachpoint.Gameplay.Weapons;
using UnityEngine;
using VContainer;
namespace Breachpoint.Gameplay.AI
{
    [RequireComponent(typeof(PerceptionTarget))]
    public sealed class PlayerEnemyBridge : MonoBehaviour
    {
        [SerializeField] private float _shotRadius = 35f;
        [SerializeField] private float _walkRadius = 8f;
        [SerializeField] private float _sprintRadius = 16f;
        private EnemyWorld _world;
        private PlayerMovement _movement;
        private HitscanWeapon _weapon;
        private bool _subscribed;
        private float _nextStep;
        [Inject]
        public void Construct(EnemyWorld world, PlayerMovement movement, HitscanWeapon weapon)
        {
            _world = world; _movement = movement; _weapon = weapon;
            GetComponent<PerceptionTarget>().Initialize(world, Faction.Player);
            Subscribe();
        }
        private void OnEnable() { _nextStep = 0; Subscribe(); }
        private void Subscribe()
        {
            if (_world == null || _subscribed || !isActiveAndEnabled) return;
            if (_weapon != null) _weapon.ShotResolved += Shot;
            if (_movement != null) _movement.Landed += Land;
            _subscribed = true;
        }
        private void OnDisable()
        {
            if (!_subscribed) return;
            if (_weapon != null) _weapon.ShotResolved -= Shot;
            if (_movement != null) _movement.Landed -= Land;
            _subscribed = false;
        }
        private void Update()
        {
            if (_world == null || _movement == null || !_movement.IsGrounded || _movement.IsCrouching ||
                _movement.Velocity.sqrMagnitude < 1f || Time.time < _nextStep) return;
            _nextStep = Time.time + (_movement.IsSprintHeld ? 0.3f : 0.5f);
            Emit(_movement.IsSprintHeld ? _sprintRadius : _walkRadius);
        }
        private void Shot(WeaponShotResult shot) => Emit(_shotRadius);
        private void Land(float speed) => Emit(Mathf.Clamp(Mathf.Abs(speed) * 2f, _walkRadius, _sprintRadius));
        private void Emit(float radius) => _world?.Emit(new NoiseStimulus(transform.position, radius, Faction.Player, Time.time));
    }
}
