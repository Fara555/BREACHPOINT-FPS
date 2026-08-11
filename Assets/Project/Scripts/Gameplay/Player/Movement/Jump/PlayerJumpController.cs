using Breachpoint.Gameplay.Player.Input;
using UnityEngine;

namespace Breachpoint.Gameplay.Player.Movement.Jump
{
    public sealed class PlayerJumpController
    {
        private readonly PlayerMovementConfig _config;

        private bool _isJumpHeld;
        private bool _wasJumpReleased;

        private float _coyoteTimeRemaining;
        private float _jumpBufferTimeRemaining;
        private float _maximumDownwardSpeed;

        public PlayerJumpController(
            PlayerMovementConfig config)
        {
            _config = config;
        }

        public void ReadInput(
            IPlayerInput input)
        {
            if (input == null)
            {
                return;
            }

            _isJumpHeld =
                input.IsJumpHeld;

            _wasJumpReleased |=
                input.WasJumpReleased;

            if (input.WasJumpPressed)
            {
                _jumpBufferTimeRemaining =
                    _config.JumpBufferTime;
            }
        }

        public void Update(
            float deltaTime)
        {
            if (_jumpBufferTimeRemaining <= 0f)
            {
                return;
            }

            _jumpBufferTimeRemaining =
                Mathf.Max(
                    0f,
                    _jumpBufferTimeRemaining -
                    deltaTime);
        }

        public void UpdateCoyoteTime(
            bool isGrounded,
            float fixedDeltaTime)
        {
            if (isGrounded)
            {
                _coyoteTimeRemaining =
                    _config.CoyoteTime;

                return;
            }

            _coyoteTimeRemaining =
                Mathf.Max(
                    0f,
                    _coyoteTimeRemaining -
                    fixedDeltaTime);
        }

        public void TrackDownwardSpeed(
            bool isGrounded,
            Vector3 velocity)
        {
            if (isGrounded)
            {
                return;
            }

            float downwardSpeed =
                Mathf.Max(
                    0f,
                    -velocity.y);

            _maximumDownwardSpeed =
                Mathf.Max(
                    _maximumDownwardSpeed,
                    downwardSpeed);
        }

        public bool TryConsumeJump(
            bool isGrounded,
            bool canJump)
        {
            bool hasBufferedJump =
                _jumpBufferTimeRemaining > 0f;

            bool canUseGroundJump =
                isGrounded ||
                _coyoteTimeRemaining > 0f;

            if (!hasBufferedJump ||
                !canUseGroundJump ||
                !canJump)
            {
                return false;
            }

            _jumpBufferTimeRemaining = 0f;
            _coyoteTimeRemaining = 0f;
            _maximumDownwardSpeed = 0f;
            _wasJumpReleased = false;

            return true;
        }

        public bool TryGetLandingSpeed(
            bool isGrounded,
            bool wasGrounded,
            out float downwardSpeed)
        {
            downwardSpeed = 0f;

            if (!isGrounded ||
                wasGrounded)
            {
                return false;
            }

            downwardSpeed =
                _maximumDownwardSpeed;

            _maximumDownwardSpeed = 0f;

            return true;
        }

        public Vector3 ApplyJumpCut(
            Vector3 currentVelocity,
            bool isGrounded)
        {
            if (isGrounded ||
                !_wasJumpReleased ||
                currentVelocity.y <= 0f)
            {
                return currentVelocity;
            }

            currentVelocity.y *=
                _config.JumpCutVelocityMultiplier;

            return currentVelocity;
        }

        public Vector3 CalculateJumpVelocity(
            Vector3 currentVelocity)
        {
            Vector3 horizontalVelocity =
                new(
                    currentVelocity.x,
                    0f,
                    currentVelocity.z);

            return
                horizontalVelocity +
                Vector3.up *
                CalculateJumpSpeed();
        }

        public Vector3 CalculateJumpVelocity(
            Vector3 currentVelocity,
            Vector3 horizontalOverride)
        {
            horizontalOverride.y = 0f;

            return
                horizontalOverride +
                Vector3.up *
                CalculateJumpSpeed();
        }

        public float CalculateGravityMultiplier(
            float verticalVelocity)
        {
            if (verticalVelocity < 0f)
            {
                return
                    _config.FallGravityMultiplier;
            }

            if (_isJumpHeld &&
                Mathf.Abs(verticalVelocity) <=
                _config.ApexVelocityThreshold)
            {
                return
                    _config.ApexGravityMultiplier;
            }

            return 1f;
        }

        public void EndFixedStep()
        {
            _wasJumpReleased = false;
        }

        public void Reset()
        {
            _isJumpHeld = false;
            _wasJumpReleased = false;

            _coyoteTimeRemaining = 0f;
            _jumpBufferTimeRemaining = 0f;
            _maximumDownwardSpeed = 0f;
        }

        private float CalculateJumpSpeed()
        {
            return
                Mathf.Sqrt(
                    2f *
                    _config.Gravity *
                    _config.JumpHeight);
        }
    }
}