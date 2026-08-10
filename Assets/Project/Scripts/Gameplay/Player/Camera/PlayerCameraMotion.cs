using Breachpoint.Gameplay.Player.Movement;
using UnityEngine;

namespace Breachpoint.Gameplay.Player.Camera
{
    public sealed class PlayerCameraMotion : MonoBehaviour
    {
        private const float SpeedThreshold = 0.01f;

        [Header("References")]
        [SerializeField]
        private Transform _cameraTiltRoot;

        [SerializeField]
        private UnityEngine.Camera _playerCamera;

        [SerializeField]
        private Transform _orientation;

        [SerializeField]
        private PlayerMovement _movement;

        [SerializeField]
        private PlayerMovementConfig _movementConfig;

        [SerializeField]
        private PlayerCameraMotionConfig _config;

        private float _currentTilt;
        private float _tiltVelocity;
        private float _fieldOfViewVelocity;

        private bool _preserveSprintFieldOfViewInAir;

        private void Awake()
        {
            ValidateReferences();
            Initialize();
        }

        private void LateUpdate()
        {
            if (!CanUpdate())
            {
                return;
            }

            UpdateTilt();
            UpdateSprintFieldOfViewState();
            UpdateFieldOfView();
        }

        private void UpdateTilt()
        {
            float targetTilt =
                CalculateTargetTilt();

            _currentTilt =
                Mathf.SmoothDampAngle(
                    _currentTilt,
                    targetTilt,
                    ref _tiltVelocity,
                    _config.TiltSmoothTime);

            _cameraTiltRoot.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    _currentTilt);
        }

        private float CalculateTargetTilt()
        {
            if (!_movement.IsGrounded)
            {
                return 0f;
            }

            Vector3 horizontalVelocity =
                GetHorizontalVelocity();

            float horizontalSpeed =
                horizontalVelocity.magnitude;

            if (horizontalSpeed <
                _config.MinimumTiltSpeed)
            {
                return 0f;
            }

            float lateralSpeed =
                Vector3.Dot(
                    horizontalVelocity,
                    _orientation.right);

            float referenceSpeed =
                GetReferenceSpeed();

            if (referenceSpeed <=
                SpeedThreshold)
            {
                return 0f;
            }

            float lateralRatio =
                Mathf.Clamp(
                    lateralSpeed /
                    referenceSpeed,
                    -1f,
                    1f);

            return
                -lateralRatio *
                GetMaximumTiltAngle();
        }

        private void UpdateSprintFieldOfViewState()
        {
            if (_movement.IsGrounded)
            {
                _preserveSprintFieldOfViewInAir =
                    _movement.CurrentState ==
                    PlayerMovementState.Sprinting ||
                    _movement.CurrentState ==
                    PlayerMovementState.Sliding;

                return;
            }

            if (!_movement.IsSprintHeld)
            {
                _preserveSprintFieldOfViewInAir =
                    false;

                return;
            }

            float horizontalSpeed =
                GetHorizontalVelocity()
                    .magnitude;

            if (horizontalSpeed <
                GetMinimumSprintFieldOfViewSpeed())
            {
                _preserveSprintFieldOfViewInAir =
                    false;
            }
        }

        private void UpdateFieldOfView()
        {
            bool useSprintFieldOfView =
                _movement.CurrentState ==
                PlayerMovementState.Sprinting ||
                _movement.CurrentState ==
                PlayerMovementState.Sliding ||
                _preserveSprintFieldOfViewInAir;

            float targetFieldOfView =
                useSprintFieldOfView
                    ? _config.SprintFieldOfView
                    : _config.DefaultFieldOfView;

            _playerCamera.fieldOfView =
                Mathf.SmoothDamp(
                    _playerCamera.fieldOfView,
                    targetFieldOfView,
                    ref _fieldOfViewVelocity,
                    _config.FieldOfViewSmoothTime);
        }

        private float GetMinimumSprintFieldOfViewSpeed()
        {
            return
                Mathf.Lerp(
                    _movementConfig.WalkSpeed,
                    _movementConfig.SprintSpeed,
                    0.5f);
        }

        private Vector3 GetHorizontalVelocity()
        {
            Vector3 velocity =
                _movement.Velocity;

            velocity.y = 0f;

            return velocity;
        }

        private float GetMaximumTiltAngle()
        {
            return _movement.CurrentState switch
            {
                PlayerMovementState.Sprinting =>
                    _config.SprintTiltAngle,

                PlayerMovementState.Crouching or
                PlayerMovementState.Sliding =>
                    _config.CrouchTiltAngle,

                _ =>
                    _config.WalkTiltAngle
            };
        }

        private float GetReferenceSpeed()
        {
            return _movement.CurrentState switch
            {
                PlayerMovementState.Sprinting =>
                    _movementConfig.SprintSpeed,

                PlayerMovementState.Crouching =>
                    _movementConfig.CrouchSpeed,

                PlayerMovementState.Sliding =>
                    _movementConfig.MaximumSlideSpeed,

                _ =>
                    _movementConfig.WalkSpeed
            };
        }

        private bool CanUpdate()
        {
            return
                _cameraTiltRoot != null &&
                _playerCamera != null &&
                _orientation != null &&
                _movement != null &&
                _movementConfig != null &&
                _config != null;
        }

        private void Initialize()
        {
            if (_cameraTiltRoot != null)
            {
                _cameraTiltRoot.localRotation =
                    Quaternion.identity;
            }

            if (_playerCamera != null &&
                _config != null)
            {
                _playerCamera.fieldOfView =
                    _config.DefaultFieldOfView;
            }

            _currentTilt = 0f;
            _tiltVelocity = 0f;
            _fieldOfViewVelocity = 0f;
            _preserveSprintFieldOfViewInAir = false;
        }

        private void ValidateReferences()
        {
            if (_cameraTiltRoot == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerCameraMotion)} requires a CameraTiltRoot reference.",
                    this);
            }

            if (_playerCamera == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerCameraMotion)} requires a Camera reference.",
                    this);
            }

            if (_orientation == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerCameraMotion)} requires an Orientation reference.",
                    this);
            }

            if (_movement == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerCameraMotion)} requires a PlayerMovement reference.",
                    this);
            }

            if (_movementConfig == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerCameraMotion)} requires a PlayerMovementConfig reference.",
                    this);
            }

            if (_config == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerCameraMotion)} requires a PlayerCameraMotionConfig reference.",
                    this);
            }
        }
    }
}