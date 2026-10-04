using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    [CreateAssetMenu(menuName = "Breachpoint/Enemies/Movement")]
    public sealed class EnemyMovementConfig : ScriptableObject
    {
        [field: SerializeField, Min(0.1f)] public float WalkSpeed { get; private set; } = 2f;
        [field: SerializeField, Min(0f)] public float SteadyWalkSpeed { get; private set; }
        [field: SerializeField, Min(0.1f)] public float RunSpeed { get; private set; } = 4.5f;
        [field: SerializeField, Min(0.1f)] public float SprintSpeed { get; private set; } = 6.75f;
        [field: SerializeField, Min(0.1f)] public float CrouchSpeed { get; private set; } = 1.2f;
        [field: SerializeField, Min(0.1f)] public float Acceleration { get; private set; } = 14f;
        [field: SerializeField, Min(0.1f)] public float SteadyAcceleration { get; private set; } = 2f;
        [field: SerializeField, Min(0f)] public float SteadyStartDelay { get; private set; } = .28f;
        [field: SerializeField, Min(0.05f)] public float SteadyStartDuration { get; private set; } = .65f;
        [field: SerializeField, Min(0.05f)] public float SteadyStopDuration { get; private set; } = 1.1f;
        [field: SerializeField, Min(0.1f)] public float StopDeceleration { get; private set; } = 12f;
        [field: SerializeField, Min(1f)] public float TurnSpeed { get; private set; } = 360f;
        [field: SerializeField, Min(0.05f)] public float StoppingDistance { get; private set; } = 0.35f;
        [field: SerializeField, Min(0.1f)] public float RepathInterval { get; private set; } = 0.35f;
        [field: SerializeField, Min(0f)] public float PatrolWait { get; private set; } = 1f;
        [field: SerializeField, Min(0.5f)] public float StuckTimeout { get; private set; } = 3f;
        [SerializeField] private AnimationCurve _steadyStartSpeed = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(.08f, 0f), new Keyframe(.12f, .12f),
            new Keyframe(.20f, .40f), new Keyframe(.28f, .65f), new Keyframe(.36f, 1f));
        [SerializeField] private AnimationCurve _steadyStopSpeed = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(.10f, .82f), new Keyframe(.20f, .62f),
            new Keyframe(.40f, .42f), new Keyframe(.49f, .18f), new Keyframe(.57f, 0f), new Keyframe(1f, 0f));
        public float SteadyStartSpeedAt(float normalizedTime) => Mathf.Clamp01(_steadyStartSpeed.Evaluate(Mathf.Clamp01(normalizedTime)));
        public float SteadyStopSpeedAt(float normalizedTime) => Mathf.Clamp01(_steadyStopSpeed.Evaluate(Mathf.Clamp01(normalizedTime)));
        private void OnValidate()
        {
            WalkSpeed = Mathf.Max(0.1f, WalkSpeed); RunSpeed = Mathf.Max(WalkSpeed, RunSpeed);
            SprintSpeed = Mathf.Max(RunSpeed, SprintSpeed); CrouchSpeed = Mathf.Clamp(CrouchSpeed, 0.1f, WalkSpeed);
            Acceleration = Mathf.Max(0.1f, Acceleration); TurnSpeed = Mathf.Max(1f, TurnSpeed);
            StoppingDistance = Mathf.Max(0.05f, StoppingDistance); RepathInterval = Mathf.Max(0.1f, RepathInterval);
            PatrolWait = Mathf.Max(0f, PatrolWait); StuckTimeout = Mathf.Max(0.5f, StuckTimeout);
        }
    }
}
