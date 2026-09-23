using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    [CreateAssetMenu(menuName = "Breachpoint/Enemies/Movement")]
    public sealed class EnemyMovementConfig : ScriptableObject
    {
        [field: SerializeField, Min(0.1f)] public float WalkSpeed { get; private set; } = 2f;
        [field: SerializeField, Min(0.1f)] public float RunSpeed { get; private set; } = 4.5f;
        [field: SerializeField, Min(0.1f)] public float Acceleration { get; private set; } = 14f;
        [field: SerializeField, Min(1f)] public float TurnSpeed { get; private set; } = 360f;
        [field: SerializeField, Min(0.05f)] public float StoppingDistance { get; private set; } = 0.35f;
        [field: SerializeField, Min(0.1f)] public float RepathInterval { get; private set; } = 0.35f;
        [field: SerializeField, Min(0f)] public float PatrolWait { get; private set; } = 1f;
        [field: SerializeField, Min(0.5f)] public float StuckTimeout { get; private set; } = 3f;
        private void OnValidate()
        {
            WalkSpeed = Mathf.Max(0.1f, WalkSpeed); RunSpeed = Mathf.Max(WalkSpeed, RunSpeed);
            Acceleration = Mathf.Max(0.1f, Acceleration); TurnSpeed = Mathf.Max(1f, TurnSpeed);
            StoppingDistance = Mathf.Max(0.05f, StoppingDistance); RepathInterval = Mathf.Max(0.1f, RepathInterval);
            PatrolWait = Mathf.Max(0f, PatrolWait); StuckTimeout = Mathf.Max(0.5f, StuckTimeout);
        }
    }
}
