using Breachpoint.Gameplay.Player.Movement;
using UnityEngine;
using VContainer;

namespace Breachpoint.Gameplay.Player.Camera
{
    public sealed class PlayerCameraHeight : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private Transform _cameraRoot;

        [SerializeField]
        private PlayerMovement _movement;

        private PlayerMovementConfig _config;

        private float _heightVelocity;
        private float _curbCameraOffset;
        private float _curbCameraOffsetVelocity;

        private bool _wasSliding;

        [Inject]
        public void Construct(
            PlayerMovementConfig config)
        {
            _config = config;
        }

        private void OnEnable()
        {
            if (_movement == null)
            {
                return;
            }

            _movement.CurbResolved +=
                HandleCurbResolved;
        }

        private void OnDisable()
        {
            if (_movement == null)
            {
                return;
            }

            _movement.CurbResolved -=
                HandleCurbResolved;
        }

        private void Start()
        {
            ValidateReferences();

            if (_cameraRoot == null ||
                _config == null)
            {
                return;
            }

            Vector3 position =
                _cameraRoot.localPosition;

            position.y =
                _config.StandingCameraLocalY;

            _cameraRoot.localPosition =
                position;

            _heightVelocity = 0f;
            _curbCameraOffset = 0f;
            _curbCameraOffsetVelocity = 0f;

            _wasSliding =
                _movement != null &&
                _movement.IsSliding;
        }

        private void Update()
        {
            if (!CanUpdate())
            {
                return;
            }

            UpdateCurbOffset();
            UpdateHeight();

            _wasSliding =
                _movement.IsSliding;
        }

        private void HandleCurbResolved(
            float curbHeight)
        {
            _curbCameraOffset +=
                curbHeight;
        }

        private void UpdateCurbOffset()
        {
            _curbCameraOffset =
                Mathf.SmoothDamp(
                    _curbCameraOffset,
                    0f,
                    ref _curbCameraOffsetVelocity,
                    _config.CurbCameraSmoothTime);
        }

        private void UpdateHeight()
        {
            bool useCrouchingHeight =
                _movement
                    .ShouldUseCrouchingCameraHeight;

            float stanceCameraY =
                useCrouchingHeight
                    ? _config.CrouchingCameraLocalY
                    : _config.StandingCameraLocalY;

            float targetCameraY =
                stanceCameraY -
                _curbCameraOffset;

            float smoothTime =
                GetCurrentSmoothTime(
                    useCrouchingHeight);

            Vector3 position =
                _cameraRoot.localPosition;

            position.y =
                Mathf.SmoothDamp(
                    position.y,
                    targetCameraY,
                    ref _heightVelocity,
                    smoothTime,
                    _config.CameraTransitionSpeed,
                    Time.deltaTime);

            _cameraRoot.localPosition =
                position;
        }

        private float GetCurrentSmoothTime(
            bool useCrouchingHeight)
        {
            if (_movement.IsSliding)
            {
                return
                    _config
                        .SlideCameraEnterSmoothTime;
            }

            if (_wasSliding &&
                !useCrouchingHeight)
            {
                return
                    _config
                        .SlideCameraExitSmoothTime;
            }

            return
                _config.CrouchCameraSmoothTime;
        }

        private bool CanUpdate()
        {
            return
                _cameraRoot != null &&
                _movement != null &&
                _config != null;
        }

        private void ValidateReferences()
        {
            if (_cameraRoot == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerCameraHeight)} requires CameraRoot.",
                    this);
            }

            if (_movement == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerCameraHeight)} requires PlayerMovement.",
                    this);
            }

            if (_config == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerCameraHeight)} requires PlayerMovementConfig.",
                    this);
            }
        }
    }
}