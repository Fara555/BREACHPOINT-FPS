using Breachpoint.Gameplay.Combat;
using UnityEngine;

namespace Breachpoint.Gameplay.AI
{
    [DisallowMultipleComponent, RequireComponent(typeof(Health))]
    public sealed class PerceptionTarget : MonoBehaviour
    {
        [SerializeField] private Faction _faction = Faction.Player;
        [SerializeField] private Transform _aimPoint;
        private EnemyWorld _world;
        private Health _health;
        private Collider _body;
        public Faction Faction => _faction;
        public Vector3 AimPosition => _aimPoint != null ? _aimPoint.position :
            _body != null ? _body.bounds.center : transform.position + Vector3.up;
        public Vector3 NavigationPosition => _body != null
            ? new Vector3(transform.position.x, _body.bounds.min.y, transform.position.z) : transform.position;
        public bool IsAlive => this != null && isActiveAndEnabled && _health != null && !_health.IsDead;
        public Health Health => _health;
        public const int AimSampleCount = 3;
        public Vector3 AimSample(int index)
        {
            if (index == 0 || _body == null) return AimPosition;
            Bounds bounds = _body.bounds;
            return bounds.center + Vector3.up * bounds.extents.y * (index == 1 ? .8f : .4f);
        }
        public void Initialize(EnemyWorld world, Faction faction)
        {
            _world?.Unregister(this);
            _health = GetComponent<Health>();
            _body = GetComponent<Collider>();
            _world = world;
            _faction = faction;
            if (isActiveAndEnabled) _world.Register(this);
        }
        private void OnEnable() => _world?.Register(this);
        private void OnDisable() => _world?.Unregister(this);
    }
}
