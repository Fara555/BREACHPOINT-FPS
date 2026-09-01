using Breachpoint.Gameplay.Player.Movement;
using UnityEngine;

namespace Breachpoint.Gameplay.Weapons
{
    [CreateAssetMenu(
        fileName = "WeaponMotionConfig",
        menuName = "Breachpoint/Weapons/Weapon Motion Config")]
    public sealed class WeaponMotionConfig : ScriptableObject
    {
        [Header("Mouse Sway")]
        [SerializeField, Min(0f)]
        private float _maximumLookDelta = 30f;

        [SerializeField]
        private Vector2 _swayPosition = new(0.0008f, 0.0006f);

        [SerializeField]
        private Vector3 _swayRotation = new(0.08f, 0.1f, 0.04f);

        [SerializeField, Min(0.01f)]
        private float _swaySnappiness = 16f;

        [Header("Movement Bob")]
        [SerializeField]
        private WeaponMotionProfile _walkProfile;

        [SerializeField]
        private WeaponMotionProfile _sprintProfile;

        [SerializeField]
        private WeaponMotionProfile _crouchProfile;

        [SerializeField, Min(0f)]
        private float _minimumBobSpeed = 0.3f;

        [SerializeField, Min(0.01f)]
        private float _bobBlendInSpeed = 8f;

        [SerializeField, Min(0.01f)]
        private float _bobBlendOutSpeed = 10f;

        [SerializeField, Min(0.01f)]
        private float _bobSnappiness = 10f;

        [Header("Movement Inertia")]
        [SerializeField, Min(0f)]
        private float _maximumAcceleration = 30f;

        [SerializeField]
        private Vector3 _inertiaPosition = new(0.0008f, 0f, 0.0012f);

        [SerializeField]
        private Vector3 _inertiaRotation = new(0.08f, 0.025f, 0.08f);

        [SerializeField, Min(0.01f)]
        private float _velocityTrackingSpeed = 10f;

        [SerializeField, Min(0.01f)]
        private float _inertiaSnappiness = 8f;

        [Header("Jump And Landing")]
        [SerializeField, Min(0f)]
        private float _jumpPositionImpulse = 0.14f;

        [SerializeField, Min(0f)]
        private float _jumpPitchImpulse = 3f;

        [SerializeField, Min(0f)]
        private float _minimumLandingSpeed = 3f;

        [SerializeField, Min(0.01f)]
        private float _maximumLandingSpeed = 14f;

        [SerializeField, Min(0f)]
        private float _minimumLandingPositionImpulse = 0.12f;

        [SerializeField, Min(0f)]
        private float _maximumLandingPositionImpulse = 0.32f;

        [SerializeField, Min(0f)]
        private float _minimumLandingPitchImpulse = 2.5f;

        [SerializeField, Min(0f)]
        private float _maximumLandingPitchImpulse = 7f;

        [SerializeField, Min(0.01f)]
        private float _impulseSpringStrength = 110f;

        [SerializeField, Min(0.01f)]
        private float _impulseDamping = 17f;

        [Header("Sprint Pose")]
        [SerializeField]
        private Vector3 _sprintPosition = new(-0.11f, -0.01f, -0.1f);

        [SerializeField]
        private Vector3 _sprintRotation = new(2f, -42f, -2f);

        [SerializeField, Min(0.01f)]
        private float _sprintPoseEnterSpeed = 10f;

        [SerializeField, Min(0.01f)]
        private float _sprintPoseExitSpeed = 14f;

        [SerializeField, Min(0f)]
        private float _minimumSprintPoseEnterSpeed = 3.5f;

        [SerializeField, Min(0f)]
        private float _minimumSprintPoseExitSpeed = 2f;

        [SerializeField, Min(0f)]
        private float _sprintPoseMovementGraceTime = 0.18f;

        [Header("Idle Breathing")]
        [SerializeField, Min(0f)]
        private float _breathingCyclesPerSecond = 0.22f;

        [SerializeField]
        private Vector3 _breathingPositionAmplitude =
            new(0.001f, 0.002f, 0.0005f);

        [SerializeField]
        private Vector3 _breathingRotationAmplitude =
            new(0.08f, 0.04f, 0.08f);

        [SerializeField, Min(0.01f)]
        private float _breathingBlendInSpeed = 2.5f;

        [SerializeField, Min(0.01f)]
        private float _breathingBlendOutSpeed = 6f;

        [Header("Aiming")]
        [SerializeField, Range(0f, 1f)]
        private float _aimSwayMultiplier = 0.25f;

        [SerializeField, Range(0f, 1f)]
        private float _aimBobMultiplier = 0.2f;

        [SerializeField, Range(0f, 1f)]
        private float _aimInertiaMultiplier = 0.25f;

        [SerializeField, Range(0f, 1f)]
        private float _aimImpulseMultiplier = 0.35f;

        [SerializeField, Range(0f, 1f)]
        private float _aimBreathingMultiplier = 0.25f;

        [SerializeField, Min(0.01f)]
        private float _aimBlendSpeed = 14f;

        public float MaximumLookDelta => _maximumLookDelta;
        public Vector2 SwayPosition => _swayPosition;
        public Vector3 SwayRotation => _swayRotation;
        public float SwaySnappiness => _swaySnappiness;
        public float MinimumBobSpeed => _minimumBobSpeed;
        public float BobBlendInSpeed => _bobBlendInSpeed;
        public float BobBlendOutSpeed => _bobBlendOutSpeed;
        public float BobSnappiness => _bobSnappiness;
        public float MaximumAcceleration => _maximumAcceleration;
        public Vector3 InertiaPosition => _inertiaPosition;
        public Vector3 InertiaRotation => _inertiaRotation;
        public float VelocityTrackingSpeed => _velocityTrackingSpeed;
        public float InertiaSnappiness => _inertiaSnappiness;
        public float JumpPositionImpulse => _jumpPositionImpulse;
        public float JumpPitchImpulse => _jumpPitchImpulse;
        public float MinimumLandingSpeed => _minimumLandingSpeed;
        public float MaximumLandingSpeed => _maximumLandingSpeed;
        public float MinimumLandingPositionImpulse =>
            _minimumLandingPositionImpulse;
        public float MaximumLandingPositionImpulse =>
            _maximumLandingPositionImpulse;
        public float MinimumLandingPitchImpulse =>
            _minimumLandingPitchImpulse;
        public float MaximumLandingPitchImpulse =>
            _maximumLandingPitchImpulse;
        public float ImpulseSpringStrength => _impulseSpringStrength;
        public float ImpulseDamping => _impulseDamping;
        public Vector3 SprintPosition => _sprintPosition;
        public Vector3 SprintRotation => _sprintRotation;
        public float SprintPoseEnterSpeed => _sprintPoseEnterSpeed;
        public float SprintPoseExitSpeed => _sprintPoseExitSpeed;
        public float MinimumSprintPoseEnterSpeed =>
            _minimumSprintPoseEnterSpeed;
        public float MinimumSprintPoseExitSpeed =>
            _minimumSprintPoseExitSpeed;
        public float SprintPoseMovementGraceTime =>
            _sprintPoseMovementGraceTime;
        public float BreathingCyclesPerSecond =>
            _breathingCyclesPerSecond;
        public Vector3 BreathingPositionAmplitude =>
            _breathingPositionAmplitude;
        public Vector3 BreathingRotationAmplitude =>
            _breathingRotationAmplitude;
        public float BreathingBlendInSpeed =>
            _breathingBlendInSpeed;
        public float BreathingBlendOutSpeed =>
            _breathingBlendOutSpeed;
        public float AimSwayMultiplier => _aimSwayMultiplier;
        public float AimBobMultiplier => _aimBobMultiplier;
        public float AimInertiaMultiplier => _aimInertiaMultiplier;
        public float AimImpulseMultiplier => _aimImpulseMultiplier;
        public float AimBreathingMultiplier => _aimBreathingMultiplier;
        public float AimBlendSpeed => _aimBlendSpeed;

        public WeaponMotionProfile GetProfile(PlayerMovementState state)
        {
            return state switch
            {
                PlayerMovementState.Sprinting => _sprintProfile,
                PlayerMovementState.Crouching => _crouchProfile,
                _ => _walkProfile
            };
        }

        private void OnValidate()
        {
            _maximumLandingSpeed = Mathf.Max(
                _maximumLandingSpeed,
                _minimumLandingSpeed + 0.01f);

            _minimumSprintPoseExitSpeed = Mathf.Min(
                _minimumSprintPoseExitSpeed,
                _minimumSprintPoseEnterSpeed);
        }
    }
}
