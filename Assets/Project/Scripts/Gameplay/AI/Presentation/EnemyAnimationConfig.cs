using System;
using UnityEngine;

namespace Breachpoint.Gameplay.AI
{
    [Serializable]
    public sealed class EnemyAnimationConfig
    {
        [SerializeField] private EnemyTurnProfile[] _turnProfiles = Array.Empty<EnemyTurnProfile>();
        public EnemyTurnProfile GetTurnProfile(string state)
        {
            foreach (var profile in _turnProfiles) if (profile != null && profile.State == state) return profile;
            return null;
        }
        [field: SerializeField, Min(0f)] public float FloatDamping { get; private set; } = 0.18f;
        [field: SerializeField, Min(0.01f)] public float MoveSpeedAccelerationDamp { get; private set; } = 0.12f;
        [field: SerializeField, Min(0.01f)] public float MoveSpeedDecelerationDamp { get; private set; } = 0.1f;
        [field: SerializeField, Min(0.01f)] public float MoveDirectionDamp { get; private set; } = 0.035f;
        [field: SerializeField, Range(0f, 1f)] public float MovementStartAnticipation { get; private set; } = 0.2f;
        [field: SerializeField, Min(.01f), Tooltip("Speed-limit ramp after the existing combat readiness pose permits movement, in seconds.")] public float CombatStartDuration { get; private set; } = .18f;
        [field: SerializeField, Range(0f, 1f), Tooltip("Fraction of Walk blend sent immediately during a Combat Walk/Run start.")] public float CombatStartBlend { get; private set; } = .6f;
        [field: SerializeField, Range(0f, 30f)] public float CoarseAimYawLimit { get; private set; } = 18f;
        [field: SerializeField, Range(0f, 1f)] public float CoarseAimWeight { get; private set; } = 0.8f;
        [field: SerializeField, Range(0f, 25f)] public float CoarseAimPitchLimit { get; private set; } = 18f;
        [field: SerializeField, Range(0f, 1f)] public float CoarseAimPitchWeight { get; private set; } = .95f;
        [field: SerializeField, Min(.01f)] public float CoarseAimBlend { get; private set; } = .2f;
        [SerializeField] private AnimationCurve _raiseAim = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(.12f, .05f), new Keyframe(.30f, .55f), new Keyframe(.50f, 1f), new Keyframe(1f, 1f));
        [SerializeField] private AnimationCurve _leftTurnAim = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.12f, .2f), new Keyframe(.25f, 0f), new Keyframe(.55f, 0f), new Keyframe(.78f, .4f), new Keyframe(.93f, 1f), new Keyframe(1.2f, 1f));
        [field: SerializeField, Range(0f, 1f)] public float SprintAimRigWeight { get; private set; } = 0f;
        public float RaiseAimAt(float normalizedTime) => Mathf.Clamp01(_raiseAim.Evaluate(Mathf.Clamp01(normalizedTime)));
        public float LeftTurnAimAt(float normalizedTime) => Mathf.Clamp01(_leftTurnAim.Evaluate(Mathf.Clamp(normalizedTime, 0f, 1.2f)));
        [field: SerializeField, Min(0.01f)] public float SteadyNaturalSpeed { get; private set; } = 1.12f;
        [field: SerializeField] public Vector3 CombatNaturalSpeeds { get; private set; } = new Vector3(1.74f, 4.26f, 6.06f);
        // Cardinal values are left, backward, right; diagonals are FL, BL, BR, FR.
        [field: SerializeField, Tooltip("Authored m/s: X left, Y backward, Z right. Forward uses Combat Natural Speeds X.")] public Vector3 WalkCardinalNaturalSpeeds { get; private set; } = new Vector3(1.8118f, 1.7635f, 1.8085f);
        [field: SerializeField, Tooltip("Authored m/s: X left, Y backward, Z right. Forward uses Combat Natural Speeds Y.")] public Vector3 RunCardinalNaturalSpeeds { get; private set; } = new Vector3(4.4867f, 4.5343f, 4.5109f);
        [field: SerializeField, Tooltip("Authored m/s: X forward-left, Y backward-left, Z backward-right, W forward-right.")] public Vector4 WalkDiagonalNaturalSpeeds { get; private set; } = new Vector4(1.8081f, 1.7703f, 1.8046f, 1.7827f);
        [field: SerializeField, Tooltip("Authored m/s: X forward-left, Y backward-left, Z backward-right, W forward-right.")] public Vector4 RunDiagonalNaturalSpeeds { get; private set; } = new Vector4(4.5260f, 4.5213f, 4.5274f, 4.3621f);
        [field: SerializeField, Min(0.01f)] public float CrouchNaturalSpeed { get; private set; } = 1.62f;
        [field: SerializeField] public Vector2 StridePlaybackLimits { get; private set; } = new Vector2(0.75f, 1.15f);
        [field: SerializeField, Min(0.01f)] public float SteadyStartSpeed { get; private set; } = 1f;
        [field: SerializeField, Min(0.01f)] public float SteadyStopSpeed { get; private set; } = 1f;
        [field: SerializeField, Min(0.01f)] public float StancePlaybackSpeed { get; private set; } = 1f;
        [field: SerializeField, Min(0f)] public float LocomotionBlend { get; private set; } = 0.2f;
        [field: SerializeField, Min(0f)] public float StanceBlend { get; private set; } = 0.14f;
        [field: SerializeField, Min(0f)] public float ReadinessBlend { get; private set; } = 0.2f;
        [field: SerializeField, Min(0f)] public float TurnBlendIn { get; private set; } = 0.12f;
        [field: SerializeField, Min(0f)] public float TurnBlendOut { get; private set; } = 0.18f;
        [field: SerializeField, Min(0f)] public float ActionBlendIn { get; private set; } = 0.1f;
        [field: SerializeField, Min(0f)] public float ActionBlendOut { get; private set; } = 0.18f;
        [field: SerializeField, Min(0f)] public float HitBlendIn { get; private set; } = 0.06f;
        [field: SerializeField, Min(0f)] public float ActionLayerBlend { get; private set; } = 0.16f;
        [field: SerializeField, Min(0f)] public float IkBlend { get; private set; } = 0.22f;
        [field: SerializeField, Min(0f)] public float AimBlend { get; private set; } = 0.2f;
        [field: SerializeField] public Vector3 ElbowFromChest { get; private set; } = new Vector3(-0.3f, -0.27f, -0.16f);
        [field: SerializeField] public Vector3 CrouchElbowFromChest { get; private set; } = new Vector3(-0.32f, -0.24f, -0.18f);
        [field: SerializeField, Min(0f)] public float HintBlend { get; private set; } = 0.12f;
        [field: SerializeField, Min(0f)] public float TurnStableTime { get; private set; } = 0.08f;
        [field: SerializeField, Min(0f)] public float TurnHysteresis { get; private set; } = 8f;
        [field: SerializeField, Min(1f)] public float StationaryTurnSpeed { get; private set; } = 90f;
        [field: SerializeField, Min(0.01f)] public float StationarySpeed { get; private set; } = 0.08f;
        [field: SerializeField, Min(0f), Tooltip("Maximum actual planar speed in m/s for an animated death. Faster deaths immediately preserve the locomotion pose as ragdoll.")] public float DeathAnimationMaxSpeed { get; private set; } = 0.08f;
        [field: SerializeField, Range(0f, 1f)] public float WalkThreshold { get; private set; } = 0.33f;
        [field: SerializeField, Range(0f, 1f)] public float RunThreshold { get; private set; } = 0.66f;
        [field: SerializeField, Range(0f, 1f)] public float SprintThreshold { get; private set; } = 1f;
        [field: SerializeField, Min(1f)] public float SprintSpeedRatio { get; private set; } = 1.5f;
        [field: SerializeField, Range(1f, 180f)] public float Turn90Angle { get; private set; } = 55f;
        [field: SerializeField, Range(1f, 180f)] public float Turn180Angle { get; private set; } = 135f;
        [field: SerializeField, Min(0f)] public float TurnCooldown { get; private set; } = 0.35f;
        [field: SerializeField] public Vector4 SteadyTurnDurations { get; private set; } = new Vector4(1.466667f, 1.7f, 1.866667f, 1.966667f);
        [field: SerializeField] public Vector4 CombatTurnDurations { get; private set; } = new Vector4(1f, 1f, 1.733333f, 1.8f);
        [field: SerializeField] public Vector4 CrouchTurnDurations { get; private set; } = new Vector4(1.266667f, 1.233333f, 1.433333f, 1.233333f);
        [field: SerializeField, Min(0f)] public float AimSmoothing { get; private set; } = 12f;
        [field: SerializeField, Min(0.1f)] public float RestAimDistance { get; private set; } = 8f;
        [field: SerializeField, Min(0f)] public float FireHoldDuration { get; private set; } = 0.18f;
    }

    [Serializable]
    public sealed class EnemyTurnProfile
    {
        [SerializeField] private string _state;
        [SerializeField] private AnimationCurve _progress;
        [SerializeField] private float _duration;
        public string State => _state;
        public float Duration => _duration;
        public float Evaluate(float normalizedTime) => _progress.Evaluate(Mathf.Clamp01(normalizedTime));
    }
}
