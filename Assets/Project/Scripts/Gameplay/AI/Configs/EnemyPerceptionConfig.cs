using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    [CreateAssetMenu(menuName = "Breachpoint/Enemies/Perception")]
    public sealed class EnemyPerceptionConfig : ScriptableObject
    {
        [field: SerializeField, Min(1f)] public float ViewRange { get; private set; } = 25f;
        [field: SerializeField, Range(1f, 360f)] public float ViewAngle { get; private set; } = 110f;
        [field: SerializeField, Min(0.05f)] public float Interval { get; private set; } = 0.2f;
        [field: SerializeField, Min(0f)] public float HearingRange { get; private set; } = 30f;
        [field: SerializeField, Min(0f)] public float MemoryDuration { get; private set; } = 5f;
        [field: SerializeField, Min(0.01f)] public float AlertRise { get; private set; } = 3f;
        [field: SerializeField, Min(0.01f)] public float AlertFall { get; private set; } = 0.4f;
        [field: SerializeField] public LayerMask TargetMask { get; private set; } = (1 << 6) | (1 << 8);
        [field: SerializeField] public LayerMask ObstructionMask { get; private set; } = ~(1 << 7);
        private void OnValidate()
        {
            ViewRange = Mathf.Max(1f, ViewRange); ViewAngle = Mathf.Clamp(ViewAngle, 1f, 360f);
            Interval = Mathf.Max(0.05f, Interval); HearingRange = Mathf.Max(0f, HearingRange);
            MemoryDuration = Mathf.Max(0f, MemoryDuration); AlertRise = Mathf.Max(0.01f, AlertRise); AlertFall = Mathf.Max(0.01f, AlertFall);
        }
    }
}
