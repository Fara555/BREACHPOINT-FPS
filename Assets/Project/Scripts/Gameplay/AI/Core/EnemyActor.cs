using Breachpoint.Gameplay.Combat;
using UnityEngine;

namespace Breachpoint.Gameplay.AI
{
    [RequireComponent(typeof(Health), typeof(PerceptionTarget), typeof(EnemyNavigation))]
    public sealed class EnemyActor : MonoBehaviour
    {
        [field: SerializeField] public Transform Eyes { get; private set; }
        [field: SerializeField] public Transform Muzzle { get; private set; }
        [field: SerializeField] public PatrolRoute Route { get; set; }
        public Health Health { get; private set; }
        public PerceptionTarget Target { get; private set; }
        public EnemyNavigation Navigation { get; private set; }
        public void Initialize(EnemyArchetypeConfig config, EnemyWorld world)
        {
            Health = GetComponent<Health>(); Target = GetComponent<PerceptionTarget>(); Navigation = GetComponent<EnemyNavigation>();
            if (Eyes == null || Muzzle == null) throw new System.InvalidOperationException($"{name}: enemy requires Eyes and Muzzle.");
            Health.Configure(config.MaximumHealth, false);
            Target.Initialize(world, config.Faction); Navigation.Initialize(config.Movement);
        }
    }
}
