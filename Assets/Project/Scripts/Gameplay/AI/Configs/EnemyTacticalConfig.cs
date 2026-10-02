using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    [CreateAssetMenu(menuName = "Breachpoint/Enemies/Tactics")]
    public sealed class EnemyTacticalConfig : ScriptableObject
    {
        [field: SerializeField, Min(0f)] public float CommittedCoverBonus { get; private set; } = 85f;
        [field: SerializeField, Min(0f)] public float CoverPreferenceBonus { get; private set; } = 50f;
        [field: SerializeField, Min(0f)] public float AdvanceScore { get; private set; } = 120f;
        [field: SerializeField, Min(0f)] public float BlockedCoverRepositionScore { get; private set; } = 110f;
        [field: SerializeField, Min(0.1f)] public float DecisionInterval { get; private set; } = 0.6f;
        [field: SerializeField, Min(0f)] public float MinimumIntentDuration { get; private set; } = 2f;
        [field: SerializeField, Min(0f)] public float ImprovementThreshold { get; private set; } = 8f;
        [field: SerializeField, Min(1f)] public float PreferredSpacing { get; private set; } = 2f;
        [field: SerializeField, Min(1f)] public float FlankDistance { get; private set; } = 8f;
        [field: SerializeField, Min(1f)] public float MaximumTravel { get; private set; } = 18f;
        [field: SerializeField, Min(1f)] public float RepositionAfter { get; private set; } = 10f;
        [field: SerializeField, Min(0.1f)] public float SuppressionDuration { get; private set; } = 2f;
        [field: SerializeField, Range(0.1f, 2f)] public float RecentSuppressionMemory { get; private set; } = 1f;
        [field: SerializeField, Min(1f)] public float PositionTimeout { get; private set; } = 8f;
        [field: SerializeField, Range(1, 8)] public int CandidateBudget { get; private set; } = 4;
        [field: SerializeField, Range(0f, 1f)] public float ReloadAmmoFraction { get; private set; } = 0.25f;
        [field: SerializeField] public EnemyCoverConfig Cover { get; private set; }
        [field: SerializeField] public EnemySquadConfig Squad { get; private set; }
    }
}
