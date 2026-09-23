using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    [CreateAssetMenu(menuName = "Breachpoint/Enemies/Combat")]
    public sealed class EnemyCombatConfig : ScriptableObject
    {
        [field: SerializeField, Min(0.1f)] public float Range { get; private set; } = 18f;
        [field: SerializeField, Min(0f)] public float PreferredRange { get; private set; } = 12f;
        [field: SerializeField, Min(0f)] public float MinimumRange { get; private set; } = 4f;
        [field: SerializeField, Min(0f)] public float ReactionTime { get; private set; } = 0.45f;
        [field: SerializeField, Min(0.01f)] public float FireInterval { get; private set; } = 0.15f;
        [field: SerializeField, Min(1)] public int BurstLength { get; private set; } = 3;
        [field: SerializeField, Min(0f)] public float BurstPause { get; private set; } = 0.7f;
        [field: SerializeField, Min(1)] public int MagazineSize { get; private set; } = 18;
        [field: SerializeField, Min(0.01f)] public float ReloadDuration { get; private set; } = 2f;
        [field: SerializeField, Min(0f)] public float Damage { get; private set; } = 8f;
        [field: SerializeField, Range(0f, 30f)] public float SpreadAngle { get; private set; } = 2f;
        [field: SerializeField, Min(1f)] public float MovingSpreadMultiplier { get; private set; } = 2f;
        [field: SerializeField] public LayerMask HitMask { get; private set; } = ~(1 << 7);
        private void OnValidate()
        {
            Range = Mathf.Max(0.1f, Range); PreferredRange = Mathf.Clamp(PreferredRange, 0f, Range);
            MinimumRange = Mathf.Clamp(MinimumRange, 0f, PreferredRange); ReactionTime = Mathf.Max(0f, ReactionTime);
            FireInterval = Mathf.Max(0.01f, FireInterval); BurstLength = Mathf.Max(1, BurstLength); BurstPause = Mathf.Max(0f, BurstPause);
            MagazineSize = Mathf.Max(1, MagazineSize); ReloadDuration = Mathf.Max(0.01f, ReloadDuration);
            Damage = Mathf.Max(0f, Damage); SpreadAngle = Mathf.Clamp(SpreadAngle, 0f, 30f); MovingSpreadMultiplier = Mathf.Max(1f, MovingSpreadMultiplier);
        }
    }
}
