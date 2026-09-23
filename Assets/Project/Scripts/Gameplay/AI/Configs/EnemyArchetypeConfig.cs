using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    [CreateAssetMenu(menuName = "Breachpoint/Enemies/Archetype")]
    public sealed class EnemyArchetypeConfig : ScriptableObject
    {
        [field: SerializeField] public string Id { get; private set; } = "rifleman";
        [field: SerializeField, Min(1f)] public float MaximumHealth { get; private set; } = 100f;
        [field: SerializeField] public Faction Faction { get; private set; } = Faction.Hostile;
        [field: SerializeField] public EnemyMovementConfig Movement { get; private set; }
        [field: SerializeField] public EnemyPerceptionConfig Perception { get; private set; }
        [field: SerializeField] public EnemyCombatConfig Combat { get; private set; }
        [field: SerializeField] public EnemyDecisionConfig Decision { get; private set; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Id) && Movement != null && Perception != null && Combat != null && Decision != null;
        private void OnValidate() => MaximumHealth = Mathf.Max(1f, MaximumHealth);
    }
}
