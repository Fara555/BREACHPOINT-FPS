using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    [CreateAssetMenu(menuName = "Breachpoint/Enemies/Decision")]
    public sealed class EnemyDecisionConfig : ScriptableObject
    {
        [field: SerializeField, Min(0.05f)] public float TickInterval { get; private set; } = 0.1f;
        [field: SerializeField, Min(0.1f)] public float SearchDuration { get; private set; } = 5f;
        [field: SerializeField, Min(0.1f)] public float InvestigateDuration { get; private set; } = 4f;
        [field: SerializeField, Range(0.01f, 1f)] public float AlertThreshold { get; private set; } = 0.6f;
        private void OnValidate()
        {
            TickInterval = Mathf.Max(0.05f, TickInterval); SearchDuration = Mathf.Max(0.1f, SearchDuration);
            InvestigateDuration = Mathf.Max(0.1f, InvestigateDuration); AlertThreshold = Mathf.Clamp(AlertThreshold, 0.01f, 1f);
        }
    }
}
