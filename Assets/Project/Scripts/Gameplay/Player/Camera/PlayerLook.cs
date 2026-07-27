using Breachpoint.Gameplay.Player.Input;
using UnityEngine;
using VContainer;

namespace Breachpoint.Gameplay.Player.Look
{
    public sealed class PlayerLook : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _orientation;
        [SerializeField] private Transform _cameraRoot;
        [SerializeField] private PlayerLookConfig _config;

        private IPlayerInput _input;

        private float _yaw;
        private float _pitch;

        [Inject]
        public void Construct(IPlayerInput input)
        {
            _input = input;
        }

        private void Awake()
        {
            ValidateReferences();
            InitializeRotation();
            ApplyCursorState();
        }

        private void Update()
        {
            ReadLookInput();
        }

        private void LateUpdate()
        {
            ApplyRotation();
        }

        private void ReadLookInput()
        {
            if (!CanProcessLook())
            {
                return;
            }

            Vector2 lookInput = _input.Look;

            _yaw += lookInput.x * _config.MouseSensitivity;
            _pitch -= lookInput.y * _config.MouseSensitivity;

            _pitch = Mathf.Clamp(
                _pitch,
                _config.MinimumPitch,
                _config.MaximumPitch);
        }

        private void ApplyRotation()
        {
            if (_orientation == null || _cameraRoot == null)
            {
                return;
            }

            _orientation.localRotation = Quaternion.Euler(
                0f,
                _yaw,
                0f);

            _cameraRoot.localRotation = Quaternion.Euler(
                _pitch,
                _yaw,
                0f);
        }

        private bool CanProcessLook()
        {
            return
                _input != null &&
                _orientation != null &&
                _cameraRoot != null &&
                _config != null;
        }

        private void InitializeRotation()
        {
            if (_orientation != null)
            {
                _yaw = NormalizeAngle(
                    _orientation.localEulerAngles.y);
            }

            if (_cameraRoot != null)
            {
                _pitch = NormalizeAngle(
                    _cameraRoot.localEulerAngles.x);
            }
        }

        private void ApplyCursorState()
        {
            if (_config == null)
            {
                return;
            }

            Cursor.lockState = _config.LockCursorOnStart
                ? CursorLockMode.Locked
                : CursorLockMode.None;

            Cursor.visible = !_config.HideCursorOnStart;
        }

        private void ValidateReferences()
        {
            if (_orientation == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerLook)} requires an Orientation reference.",
                    this);
            }

            if (_cameraRoot == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerLook)} requires a CameraRoot reference.",
                    this);
            }

            if (_config == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerLook)} requires a PlayerLookConfig reference.",
                    this);
            }
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f
                ? angle - 360f
                : angle;
        }
    }
}