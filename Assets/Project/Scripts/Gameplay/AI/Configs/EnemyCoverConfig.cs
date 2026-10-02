using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    [CreateAssetMenu(menuName = "Breachpoint/Enemies/Cover")]
    public sealed class EnemyCoverConfig : ScriptableObject
    {
        [field: SerializeField, Min(1f)] public float SearchRadius { get; private set; } = 20f;
        [field: SerializeField, Min(0.5f)] public float ReservationLease { get; private set; } = 3f;
        [field: SerializeField, Min(1f)] public float ReachTimeout { get; private set; } = 8f;
        [field: SerializeField, Range(0f, 1f)] public float MinimumProtectionDot { get; private set; } = 0.2f;
        [field: SerializeField, Min(0.5f)] public float StandingMuzzleHeight { get; private set; } = 1.4f;
        [field: SerializeField, Min(0f)] public float ProtectionWeight { get; private set; } = 30f;
        [field: SerializeField, Min(0f)] public float FiringWeight { get; private set; } = 20f;
        [field: SerializeField, Min(0f)] public float PathCostWeight { get; private set; } = 0.8f;
        [field: SerializeField, Min(0f)] public float RangeErrorWeight { get; private set; } = 0.8f;
        [field: SerializeField, Min(0f)] public float TravelExposurePenalty { get; private set; } = 10f;
        [field: SerializeField, Min(0f)] public float CrowdingPenalty { get; private set; } = 20f;
    }
}
