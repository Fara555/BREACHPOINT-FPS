using Breachpoint.Gameplay.Player.Input;
using UnityEngine;

namespace Breachpoint.Gameplay.Player.Movement.Jump
{
    public sealed class PlayerJumpController
    {
        private readonly PlayerMovementConfig _config;

        private bool _isJumpHeld;
        private bool _isJumpActive;

        private float _jumpElapsedTime;
        private float _coyoteTimeRemaining;
        private float _jumpBufferTimeRemaining;
        private float _maximumDownwardSpeed;

        private float _currentGravityMultiplier;
        private float _gravityMultiplierVelocity;

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

            if (input.WasJumpPressed)
            {
                _jumpBufferTimeRemaining =
                    _config.JumpBufferTime;
            }
        }

        public void Update(
            float deltaTime)
        {
            if (_jumpBufferTimeRemaining > 0f)
            {
                _jumpBufferTimeRemaining =
                    Mathf.Max(
                        0f,
                        _jumpBufferTimeRemaining -
                        deltaTime);
            }
        }

        public void FixedUpdate(
            bool isGrounded,
            float fixedDeltaTime)
        {
            UpdateCoyoteTime(
                isGrounded,
                fixedDeltaTime);

            if (_isJumpActive)
            {
                _jumpElapsedTime +=
                    fixedDeltaTime;
            }

            if (isGrounded &&
                _isJumpActive)
            {
                _isJumpActive = false;
                _jumpElapsedTime = 0f;
            }
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

            _isJumpActive = true;
            _jumpElapsedTime = 0f;

            _currentGravityMultiplier = 1f;
            _gravityMultiplierVelocity = 0f;

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
            float verticalVelocity,
            float fixedDeltaTime)
        {
            float targetMultiplier =
                CalculateTargetGravityMultiplier(
                    verticalVelocity);

            _currentGravityMultiplier =
                Mathf.SmoothDamp(
                    _currentGravityMultiplier,
                    targetMultiplier,
                    ref _gravityMultiplierVelocity,
                    _config.GravityTransitionSmoothTime,
                    Mathf.Infinity,
                    fixedDeltaTime);

            return
                _currentGravityMultiplier;
        }

        public void Reset()
        {
            _isJumpHeld = false;
            _isJumpActive = false;

            _jumpElapsedTime = 0f;
            _coyoteTimeRemaining = 0f;
            _jumpBufferTimeRemaining = 0f;
            _maximumDownwardSpeed = 0f;

            _currentGravityMultiplier = 1f;
            _gravityMultiplierVelocity = 0f;
        }

        private void UpdateCoyoteTime(
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

        private float CalculateTargetGravityMultiplier(
            float verticalVelocity)
        {
            if (verticalVelocity < 0f)
            {
                return
                    CalculateFallGravityMultiplier(
                        verticalVelocity);
            }

            bool canUseShortJumpGravity =
                _isJumpActive &&
                !_isJumpHeld &&
                _jumpElapsedTime >=
                _config.MinimumJumpHoldTime;

            if (canUseShortJumpGravity)
            {
                return
                    _config
                        .JumpReleaseGravityMultiplier;
            }

            return
                CalculateAscendingGravityMultiplier(
                    verticalVelocity);
        }

        private float CalculateAscendingGravityMultiplier(
            float verticalVelocity)
        {
            float apexProximity =
                1f -
                Mathf.Clamp01(
                    Mathf.Abs(verticalVelocity) /
                    _config.ApexVelocityThreshold);

            float smoothApexProximity =
                SmoothStep(
                    apexProximity);

            return
                Mathf.Lerp(
                    1f,
                    _config.ApexGravityMultiplier,
                    smoothApexProximity);
        }

        private float CalculateFallGravityMultiplier(
            float verticalVelocity)
        {
            float fallSpeed =
                Mathf.Abs(
                    verticalVelocity);

            float fallBlend =
                Mathf.Clamp01(
                    fallSpeed /
                    _config.ApexVelocityThreshold);

            float smoothFallBlend =
                SmoothStep(
                    fallBlend);

            return
                Mathf.Lerp(
                    _config.ApexGravityMultiplier,
                    _config.FallGravityMultiplier,
                    smoothFallBlend);
        }

        private float CalculateJumpSpeed()
        {
            return
                Mathf.Sqrt(
                    2f *
                    _config.Gravity *
                    _config.JumpHeight);
        }

        private static float SmoothStep(
            float value)
        {
            value =
                Mathf.Clamp01(
                    value);

            return
                value *
                value *
                (3f -
                 2f *
                 value);
        }
    }
}