using UnityEngine;

namespace Breachpoint.Gameplay.Player.Camera
{
    [CreateAssetMenu(
        fileName = "PlayerCameraMotionConfig",
        menuName = "Breachpoint/Player/Camera Motion Config")]
    public sealed class PlayerCameraMotionConfig : ScriptableObject
    {
        [Header("Strafe Tilt")]
        [SerializeField, Min(0f)]
        private float _walkTiltAngle = 1.25f;

        [SerializeField, Min(0f)]
        private float _sprintTiltAngle = 1.75f;

        [SerializeField, Min(0f)]
        private float _crouchTiltAngle = 0.8f;

        [SerializeField, Min(0.01f)]
        private float _tiltSmoothTime = 0.1f;

        [SerializeField, Min(0f)]
        private float _minimumTiltSpeed = 0.2f;

        [Header("Field Of View")]
        [SerializeField, Range(1f, 179f)]
        private float _defaultFieldOfView = 75f;

        [SerializeField, Range(1f, 179f)]
        private float _sprintFieldOfView = 82f;

        [SerializeField, Min(0.01f)]
        private float _fieldOfViewSmoothTime = 0.16f;

        public float WalkTiltAngle =>
            _walkTiltAngle;

        public float SprintTiltAngle =>
            _sprintTiltAngle;

        public float CrouchTiltAngle =>
            _crouchTiltAngle;

        public float TiltSmoothTime =>
            _tiltSmoothTime;

        public float MinimumTiltSpeed =>
            _minimumTiltSpeed;

        public float DefaultFieldOfView =>
            _defaultFieldOfView;

        public float SprintFieldOfView =>
            _sprintFieldOfView;

        public float FieldOfViewSmoothTime =>
            _fieldOfViewSmoothTime;

        private void OnValidate()
        {
            _sprintFieldOfView = Mathf.Max(
                _sprintFieldOfView,
                _defaultFieldOfView);
        }
    }
}