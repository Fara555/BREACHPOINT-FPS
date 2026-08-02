using UnityEngine;

namespace Breachpoint.Gameplay.Player.Camera
{
    [CreateAssetMenu(
        fileName = "PlayerCameraImpactConfig",
        menuName = "Breachpoint/Player/Camera Impact Config")]
    public sealed class PlayerCameraImpactConfig : ScriptableObject
    {
        [Header("Jump")]
        [SerializeField]
        private float _jumpVerticalOffset = -0.045f;

        [SerializeField]
        private float _jumpPitchOffset = -1.2f;

        [SerializeField, Min(0.01f)]
        private float _jumpAttackDuration = 0.08f;

        [SerializeField, Min(0.01f)]
        private float _jumpRecoveryDuration = 0.2f;

        [Header("Landing")]
        [SerializeField, Min(0f)]
        private float _minimumLandingSpeed = 2f;

        [SerializeField, Min(0f)]
        private float _maximumLandingSpeed = 22f;

        [SerializeField]
        private float _landingVerticalOffset = -0.11f;

        [SerializeField]
        private float _landingPitchOffset = 3f;

        [SerializeField, Min(0.01f)]
        private float _landingAttackDuration = 0.065f;

        [SerializeField, Min(0.01f)]
        private float _landingRecoveryDuration = 0.28f;

        [SerializeField, Range(0f, 0.5f)]
        private float _landingRebound = 0.12f;

        [Header("Limits")]
        [SerializeField, Min(0f)]
        private float _maximumVerticalOffset = 0.16f;

        [SerializeField, Min(0f)]
        private float _maximumPitch = 4f;

        public float JumpVerticalOffset =>
            _jumpVerticalOffset;

        public float JumpPitchOffset =>
            _jumpPitchOffset;

        public float JumpAttackDuration =>
            _jumpAttackDuration;

        public float JumpRecoveryDuration =>
            _jumpRecoveryDuration;

        public float MinimumLandingSpeed =>
            _minimumLandingSpeed;

        public float MaximumLandingSpeed =>
            _maximumLandingSpeed;

        public float LandingVerticalOffset =>
            _landingVerticalOffset;

        public float LandingPitchOffset =>
            _landingPitchOffset;

        public float LandingAttackDuration =>
            _landingAttackDuration;

        public float LandingRecoveryDuration =>
            _landingRecoveryDuration;

        public float LandingRebound =>
            _landingRebound;

        public float MaximumVerticalOffset =>
            _maximumVerticalOffset;

        public float MaximumPitch =>
            _maximumPitch;

        private void OnValidate()
        {
            _maximumLandingSpeed = Mathf.Max(
                _maximumLandingSpeed,
                _minimumLandingSpeed);
        }
    }
}