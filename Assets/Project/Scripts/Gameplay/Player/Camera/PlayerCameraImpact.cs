using Breachpoint.Gameplay.Player.Movement;
using UnityEngine;

namespace Breachpoint.Gameplay.Player.Camera
{
    public sealed class PlayerCameraImpact : MonoBehaviour
    {
        private const float LandingReboundStart = 0.7f;

        [Header("References")]
        [SerializeField]
        private Transform _cameraImpactRoot;

        [SerializeField]
        private PlayerMovement _movement;

        [SerializeField]
        private PlayerCameraImpactConfig _config;

        private Vector3 _baseLocalPosition;

        private float _jumpElapsedTime;
        private float _landingElapsedTime;
        private float _landingIntensity;

        private bool _isJumpImpactActive;
        private bool _isLandingImpactActive;

        private void Awake()
        {
            ValidateReferences();
            Initialize();
        }

        private void OnEnable()
        {
            if (_movement == null)
            {
                return;
            }

            _movement.Jumped +=
                HandleJumped;

            _movement.Landed +=
                HandleLanded;
        }

        private void OnDisable()
        {
            if (_movement == null)
            {
                return;
            }

            _movement.Jumped -=
                HandleJumped;

            _movement.Landed -=
                HandleLanded;
        }

        private void LateUpdate()
        {
            if (!CanUpdate())
            {
                return;
            }

            float jumpWeight =
                UpdateJumpImpact();

            float landingWeight =
                UpdateLandingImpact();

            ApplyImpact(
                jumpWeight,
                landingWeight);
        }

        private void HandleJumped()
        {
            _jumpElapsedTime = 0f;
            _isJumpImpactActive = true;

            _landingElapsedTime = 0f;
            _landingIntensity = 0f;
            _isLandingImpactActive = false;
        }

        private void HandleLanded(
            float downwardSpeed)
        {
            float intensity =
                Mathf.InverseLerp(
                    _config.MinimumLandingSpeed,
                    _config.MaximumLandingSpeed,
                    downwardSpeed);

            if (intensity <= 0f)
            {
                return;
            }

            _landingElapsedTime = 0f;
            _landingIntensity = intensity;
            _isLandingImpactActive = true;

            _jumpElapsedTime = 0f;
            _isJumpImpactActive = false;
        }

        private float UpdateJumpImpact()
        {
            if (!_isJumpImpactActive)
            {
                return 0f;
            }

            _jumpElapsedTime +=
                Time.deltaTime;

            float attackDuration =
                _config.JumpAttackDuration;

            float recoveryDuration =
                _config.JumpRecoveryDuration;

            float totalDuration =
                attackDuration +
                recoveryDuration;

            if (_jumpElapsedTime >= totalDuration)
            {
                _isJumpImpactActive = false;
                return 0f;
            }

            if (_jumpElapsedTime < attackDuration)
            {
                float attackProgress =
                    _jumpElapsedTime /
                    attackDuration;

                return SmoothStep(
                    attackProgress);
            }

            float recoveryProgress =
                (_jumpElapsedTime -
                 attackDuration) /
                recoveryDuration;

            return 1f -
                   SmoothStep(
                       recoveryProgress);
        }

        private float UpdateLandingImpact()
        {
            if (!_isLandingImpactActive)
            {
                return 0f;
            }

            _landingElapsedTime +=
                Time.deltaTime;

            float attackDuration =
                _config.LandingAttackDuration;

            float recoveryDuration =
                _config.LandingRecoveryDuration;

            float totalDuration =
                attackDuration +
                recoveryDuration;

            if (_landingElapsedTime >= totalDuration)
            {
                _isLandingImpactActive = false;
                _landingIntensity = 0f;

                return 0f;
            }

            float envelope;

            if (_landingElapsedTime < attackDuration)
            {
                float attackProgress =
                    _landingElapsedTime /
                    attackDuration;

                envelope =
                    SmoothStep(
                        attackProgress);
            }
            else
            {
                float recoveryProgress =
                    (_landingElapsedTime -
                     attackDuration) /
                    recoveryDuration;

                envelope =
                    EvaluateLandingRecovery(
                        recoveryProgress);
            }

            return envelope *
                   _landingIntensity;
        }

        private float EvaluateLandingRecovery(
            float recoveryProgress)
        {
            if (recoveryProgress <
                LandingReboundStart)
            {
                float compressionProgress =
                    recoveryProgress /
                    LandingReboundStart;

                return Mathf.Lerp(
                    1f,
                    -_config.LandingRebound,
                    SmoothStep(
                        compressionProgress));
            }

            float reboundProgress =
                (recoveryProgress -
                 LandingReboundStart) /
                (1f -
                 LandingReboundStart);

            return Mathf.Lerp(
                -_config.LandingRebound,
                0f,
                SmoothStep(
                    reboundProgress));
        }

        private void ApplyImpact(
            float jumpWeight,
            float landingWeight)
        {
            float verticalOffset =
                _config.JumpVerticalOffset *
                jumpWeight +
                _config.LandingVerticalOffset *
                landingWeight;

            float pitchOffset =
                _config.JumpPitchOffset *
                jumpWeight +
                _config.LandingPitchOffset *
                landingWeight;

            verticalOffset = Mathf.Clamp(
                verticalOffset,
                -_config.MaximumVerticalOffset,
                _config.MaximumVerticalOffset);

            pitchOffset = Mathf.Clamp(
                pitchOffset,
                -_config.MaximumPitch,
                _config.MaximumPitch);

            _cameraImpactRoot.localPosition =
                _baseLocalPosition +
                Vector3.up *
                verticalOffset;

            _cameraImpactRoot.localRotation =
                Quaternion.Euler(
                    pitchOffset,
                    0f,
                    0f);
        }

        private static float SmoothStep(
            float value)
        {
            value = Mathf.Clamp01(
                value);

            return
                value *
                value *
                (3f -
                 2f *
                 value);
        }

        private bool CanUpdate()
        {
            return
                _cameraImpactRoot != null &&
                _movement != null &&
                _config != null;
        }

        private void Initialize()
        {
            if (_cameraImpactRoot == null)
            {
                return;
            }

            _baseLocalPosition =
                _cameraImpactRoot.localPosition;

            _jumpElapsedTime = 0f;
            _landingElapsedTime = 0f;
            _landingIntensity = 0f;

            _isJumpImpactActive = false;
            _isLandingImpactActive = false;

            _cameraImpactRoot.localPosition =
                _baseLocalPosition;

            _cameraImpactRoot.localRotation =
                Quaternion.identity;
        }

        private void ValidateReferences()
        {
            if (_cameraImpactRoot == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerCameraImpact)} requires a CameraImpactRoot reference.",
                    this);
            }

            if (_movement == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerCameraImpact)} requires a PlayerMovement reference.",
                    this);
            }

            if (_config == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerCameraImpact)} requires a PlayerCameraImpactConfig reference.",
                    this);
            }
        }
    }
}