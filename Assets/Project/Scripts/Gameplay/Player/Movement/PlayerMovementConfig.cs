using UnityEngine;

namespace Breachpoint.Gameplay.Player.Movement
{
    [CreateAssetMenu(
        fileName = "PlayerMovementConfig",
        menuName = "Breachpoint/Player/Movement Config")]
    public sealed class PlayerMovementConfig : ScriptableObject
    {
        [Header("Speed")]
        [SerializeField, Min(0f)] private float _walkSpeed = 5f;
        [SerializeField, Min(0f)] private float _sprintSpeed = 8f;
        [SerializeField, Min(0f)] private float _crouchSpeed = 2.5f;

        [Header("Acceleration")]
        [SerializeField, Min(0f)] private float _groundAcceleration = 35f;
        [SerializeField, Min(0f)] private float _groundDeceleration = 45f;
        [SerializeField, Min(0f)] private float _airAcceleration = 12f;
        [SerializeField, Range(0f, 1f)] private float _airControl = 0.5f;

        [Header("Jump")]
        [SerializeField, Min(0f)] private float _jumpHeight = 1.25f;

        [Header("Gravity")]
        [SerializeField, Min(0f)] private float _gravity = 25f;
        [SerializeField, Min(0f)] private float _maxFallSpeed = 35f;
        [SerializeField, Min(0f)] private float _groundedVerticalSpeed = 2f;

        [Header("Crouch Collider")]
        [SerializeField, Min(0.1f)] private float _standingHeight = 2f;
        [SerializeField] private float _standingCenterY;
        [SerializeField, Min(0.1f)] private float _crouchingHeight = 1.2f;
        [SerializeField, Min(0.01f)] private float _stanceTransitionSpeed = 8f;

        [Header("Crouch Camera")]
        [SerializeField] private float _standingCameraLocalY = 0.75f;
        [SerializeField] private float _crouchingCameraLocalY = 0.1f;
        [SerializeField, Min(0.01f)] private float _cameraTransitionSpeed = 10f;

        [Header("Standing Clearance")]
        [SerializeField] private LayerMask _clearanceMask = ~0;
        [SerializeField, Min(0f)] private float _clearancePadding = 0.02f;

        [Header("Ground Detection")]
        [SerializeField] private LayerMask _groundMask = ~0;
        [SerializeField, Min(0.01f)] private float _groundProbeRadius = 0.45f;
        [SerializeField, Min(0.01f)] private float _groundProbeDistance = 0.15f;
        [SerializeField, Min(0.001f)] private float _groundedDistance = 0.08f;
        [SerializeField, Range(0f, 89f)] private float _maximumGroundAngle = 50f;

        public float WalkSpeed => _walkSpeed;
        public float SprintSpeed => _sprintSpeed;
        public float CrouchSpeed => _crouchSpeed;

        public float GroundAcceleration => _groundAcceleration;
        public float GroundDeceleration => _groundDeceleration;
        public float AirAcceleration => _airAcceleration;
        public float AirControl => _airControl;

        public float JumpHeight => _jumpHeight;

        public float Gravity => _gravity;
        public float MaxFallSpeed => _maxFallSpeed;
        public float GroundedVerticalSpeed => _groundedVerticalSpeed;

        public float StandingHeight => _standingHeight;
        public float StandingCenterY => _standingCenterY;
        public float CrouchingHeight => _crouchingHeight;
        public float StanceTransitionSpeed => _stanceTransitionSpeed;

        public float StandingCameraLocalY => _standingCameraLocalY;
        public float CrouchingCameraLocalY => _crouchingCameraLocalY;
        public float CameraTransitionSpeed => _cameraTransitionSpeed;

        public LayerMask ClearanceMask => _clearanceMask;
        public float ClearancePadding => _clearancePadding;

        public LayerMask GroundMask => _groundMask;
        public float GroundProbeRadius => _groundProbeRadius;
        public float GroundProbeDistance => _groundProbeDistance;
        public float GroundedDistance => _groundedDistance;
        public float MaximumGroundAngle => _maximumGroundAngle;

        private void OnValidate()
        {
            if (_sprintSpeed < _walkSpeed)
            {
                _sprintSpeed = _walkSpeed;
            }

            if (_crouchSpeed > _walkSpeed)
            {
                _crouchSpeed = _walkSpeed;
            }

            if (_crouchingHeight > _standingHeight)
            {
                _crouchingHeight = _standingHeight;
            }

            if (_groundedDistance > _groundProbeDistance)
            {
                _groundedDistance = _groundProbeDistance;
            }
        }
    }
}