using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    [CreateAssetMenu(menuName = "Breachpoint/Enemies/Squad")]
    public sealed class EnemySquadConfig : ScriptableObject
    {
        [field: SerializeField, Min(1f)] public float CommunicationRadius { get; private set; } = 25f;
        [field: SerializeField, Min(0f)] public float CommunicationLatency { get; private set; } = 0.25f;
        [field: SerializeField, Min(0.1f)] public float MemoryDuration { get; private set; } = 6f;
        [field: SerializeField, Min(1)] public int MaximumShooters { get; private set; } = 2;
        [field: SerializeField, Min(1)] public int MaximumMovers { get; private set; } = 2;
        [field: SerializeField, Min(0.1f)] public float SlotLease { get; private set; } = 2f;
        [field: SerializeField, Min(0.1f)] public float RoleCooldown { get; private set; } = 3f;
    }
}
