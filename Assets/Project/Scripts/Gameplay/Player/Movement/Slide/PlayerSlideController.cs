using Breachpoint.Gameplay.Player.Input;
using UnityEngine;

namespace Breachpoint.Gameplay.Player.Movement.Slide
{
    public sealed class PlayerSlideController
    {
        private const float MovementThreshold = 0.01f;
        private const float DownhillThreshold = 0.01f;

        private readonly PlayerMovementConfig _config;

        private bool _isCrouchHeld;
        private bool _resumeSlideAfterLanding;

        private Vector3 _slideDirection;

        private float _slideSpeed;
        private float _slideTimeRemaining;
        private float _slideCooldownRemaining;
        private float _slideMomentumRetentionRemaining;
        private float _slideGroundGraceRemaining;

        private float _previousFixedPositionY;

        public bool IsSliding { get; private set; }

        public PlayerSlideController(
            PlayerMovementConfig config)
        {
            _config = config;
        }

        public void Initialize(
            float currentPositionY)
        {
            Reset();

            _previousFixedPositionY =
                currentPositionY;
        }

        public void ReadInput(
            IPlayerInput input)
        {
            if (input == null)
            {
                return;
            }

            _isCrouchHeld =
                input.IsCrouchHeld;

            if (!_isCrouchHeld)
            {
                _resumeSlideAfterLanding =
                    false;
            }
        }

        public float CalculateDownwardTravelSpeed(
            float currentPositionY,
            float fixedDeltaTime)
        {
            if (fixedDeltaTime <=
                Mathf.Epsilon)
            {
                return 0f;
            }

            float downwardDistance =
                _previousFixedPositionY -
                currentPositionY;

            return
                Mathf.Max(
                    0f,
                    downwardDistance /
                    fixedDeltaTime);
        }

        public void UpdateCooldown(
            float fixedDeltaTime)
        {
            if (_slideCooldownRemaining <= 0f)
            {
                return;
            }

            _slideCooldownRemaining =
                Mathf.Max(
                    0f,
                    _slideCooldownRemaining -
                    fixedDeltaTime);
        }

        public void TryStart(
            Vector3 currentVelocity,
            Vector3 desiredDirection,
            bool isGrounded,
            bool isSprintHeld)
        {
            if (!_isCrouchHeld ||
                IsSliding ||
                !isGrounded ||
                _slideCooldownRemaining > 0f ||
                !isSprintHeld)
            {
                return;
            }

            Vector3 horizontalVelocity =
                GetHorizontalVelocity(
                    currentVelocity);

            float horizontalSpeed =
                horizontalVelocity.magnitude;

            if (horizontalSpeed <
                _config.MinimumSlideStartSpeed)
            {
                return;
            }

            Vector3 startDirection =
                horizontalSpeed >
                MovementThreshold
                    ? horizontalVelocity.normalized
                    : desiredDirection;

            if (startDirection.sqrMagnitude <=
                MovementThreshold)
            {
                return;
            }

            StartSlide(
                startDirection,
                Mathf.Max(
                    horizontalSpeed,
                    _config.InitialSlideSpeed));
        }

        public void TryResumeAfterLanding(
            Vector3 currentVelocity,
            bool isGrounded,
            bool wasGrounded)
        {
            if (!_resumeSlideAfterLanding ||
                IsSliding ||
                !_isCrouchHeld ||
                !isGrounded ||
                wasGrounded)
            {
                return;
            }

            Vector3 horizontalVelocity =
                GetHorizontalVelocity(
                    currentVelocity);

            float horizontalSpeed =
                horizontalVelocity.magnitude;

            if (horizontalSpeed <
                _config.MinimumSlideResumeSpeed)
            {
                _resumeSlideAfterLanding =
                    false;

                return;
            }

            StartSlide(
                horizontalVelocity.normalized,
                horizontalSpeed);

            _resumeSlideAfterLanding = false;
            _slideCooldownRemaining = 0f;
        }

        public void UpdateGroundState(
            GroundInfo groundInfo,
            float fixedDeltaTime)
        {
            if (!IsSliding)
            {
                return;
            }

            if (!_isCrouchHeld)
            {
                StopSlide();

                return;
            }

            if (groundInfo.IsGrounded)
            {
                _slideGroundGraceRemaining =
                    _config.SlideGroundGraceTime;

                return;
            }

            _slideGroundGraceRemaining =
                Mathf.Max(
                    0f,
                    _slideGroundGraceRemaining -
                    fixedDeltaTime);

            if (_slideGroundGraceRemaining <= 0f)
            {
                StopSlide();
            }
        }

        public Vector3 PrepareJump(
            Vector3 currentVelocity)
        {
            Vector3 horizontalVelocity =
                GetHorizontalVelocity(
                    currentVelocity);

            if (horizontalVelocity.sqrMagnitude <=
                MovementThreshold)
            {
                Vector3 direction =
                    GetHorizontalVelocity(
                        _slideDirection);

                if (direction.sqrMagnitude >
                    MovementThreshold)
                {
                    direction.Normalize();
                }

                horizontalVelocity =
                    direction *
                    _slideSpeed;
            }

            _resumeSlideAfterLanding =
                _isCrouchHeld;

            StopSlide(
                false);

            return horizontalVelocity;
        }

        public Vector3 CalculateGroundVelocity(
            GroundInfo groundInfo,
            Vector3 desiredDirection,
            float downwardTravelSpeed,
            float fixedDeltaTime)
        {
            Vector3 surfaceDirection =
                Vector3.ProjectOnPlane(
                    _slideDirection,
                    groundInfo.Normal);

            if (surfaceDirection.sqrMagnitude <=
                MovementThreshold)
            {
                surfaceDirection =
                    GetHorizontalVelocity(
                        _slideDirection);
            }

            if (surfaceDirection.sqrMagnitude >
                MovementThreshold)
            {
                surfaceDirection.Normalize();
            }

            Vector3 desiredSurfaceDirection =
                Vector3.ProjectOnPlane(
                    desiredDirection,
                    groundInfo.Normal);

            if (desiredSurfaceDirection.sqrMagnitude >
                MovementThreshold)
            {
                desiredSurfaceDirection.Normalize();

                float maximumRadians =
                    _config.SlideSteeringSpeed *
                    Mathf.Deg2Rad *
                    fixedDeltaTime;

                surfaceDirection =
                    Vector3.RotateTowards(
                            surfaceDirection,
                            desiredSurfaceDirection,
                            maximumRadians,
                            0f)
                        .normalized;
            }

            _slideDirection =
                surfaceDirection;

            float downhillFactor =
                CalculateDownhillFactor(
                    groundInfo,
                    surfaceDirection);

            float stairFactor =
                Mathf.InverseLerp(
                    _config.MinimumStairDescentSpeed,
                    _config.MaximumStairDescentSpeed,
                    downwardTravelSpeed);

            bool isGainingMomentum =
                Mathf.Max(
                    downhillFactor,
                    stairFactor) >
                DownhillThreshold;

            if (isGainingMomentum)
            {
                ApplyMomentumGain(
                    downhillFactor,
                    stairFactor,
                    fixedDeltaTime);
            }
            else
            {
                UpdateMomentumRetention(
                    fixedDeltaTime);
            }

            ApplySlideDeceleration(
                fixedDeltaTime);

            _slideSpeed =
                Mathf.Clamp(
                    _slideSpeed,
                    0f,
                    _config.MaximumSlideSpeed);

            Vector3 groundAdhesionVelocity =
                -groundInfo.Normal *
                _config.GroundedVerticalSpeed;

            Vector3 resultVelocity =
                surfaceDirection *
                _slideSpeed +
                groundAdhesionVelocity;

            if (_slideSpeed <=
                    _config.SlideEndSpeed ||
                _slideTimeRemaining <= 0f)
            {
                StopSlide();
            }

            return resultVelocity;
        }

        public Vector3 CalculateAirborneVelocity(
            Vector3 currentVelocity,
            float fixedDeltaTime)
        {
            Vector3 horizontalVelocity =
                GetHorizontalVelocity(
                    currentVelocity);

            float verticalVelocity =
                Mathf.Max(
                    currentVelocity.y -
                    _config.Gravity *
                    _config.FallGravityMultiplier *
                    fixedDeltaTime,
                    -_config.MaxFallSpeed);

            return
                horizontalVelocity +
                Vector3.up *
                verticalVelocity;
        }

        public void EndFixedStep(
            float currentPositionY)
        {
            _previousFixedPositionY =
                currentPositionY;
        }

        public void Reset()
        {
            IsSliding = false;

            _isCrouchHeld = false;
            _resumeSlideAfterLanding = false;

            _slideDirection =
                Vector3.zero;

            _slideSpeed = 0f;
            _slideTimeRemaining = 0f;
            _slideCooldownRemaining = 0f;
            _slideMomentumRetentionRemaining = 0f;
            _slideGroundGraceRemaining = 0f;
        }

        private void StartSlide(
            Vector3 direction,
            float speed)
        {
            IsSliding = true;

            _slideDirection =
                direction.normalized;

            _slideSpeed =
                Mathf.Clamp(
                    speed,
                    0f,
                    _config.MaximumSlideSpeed);

            _slideTimeRemaining =
                _config.MaximumSlideDuration;

            _slideGroundGraceRemaining =
                _config.SlideGroundGraceTime;
        }

        private void ApplyMomentumGain(
            float downhillFactor,
            float stairFactor,
            float fixedDeltaTime)
        {
            float slopeAcceleration =
                downhillFactor *
                _config.SlideDownhillAcceleration;

            float stairAcceleration =
                stairFactor *
                _config.SlideStairAcceleration;

            float acceleration =
                Mathf.Max(
                    slopeAcceleration,
                    stairAcceleration);

            _slideSpeed +=
                acceleration *
                fixedDeltaTime;

            _slideMomentumRetentionRemaining =
                _config.SlideMomentumRetentionTime;

            _slideTimeRemaining =
                _config.MaximumSlideDuration;
        }

        private void UpdateMomentumRetention(
            float fixedDeltaTime)
        {
            _slideMomentumRetentionRemaining =
                Mathf.Max(
                    0f,
                    _slideMomentumRetentionRemaining -
                    fixedDeltaTime);

            _slideTimeRemaining =
                Mathf.Max(
                    0f,
                    _slideTimeRemaining -
                    fixedDeltaTime);
        }

        private void ApplySlideDeceleration(
            float fixedDeltaTime)
        {
            float decelerationMultiplier =
                _slideMomentumRetentionRemaining > 0f
                    ? _config
                        .RetainedMomentumDecelerationMultiplier
                    : 1f;

            float deceleration =
                _config.SlideDeceleration *
                decelerationMultiplier;

            _slideSpeed =
                Mathf.MoveTowards(
                    _slideSpeed,
                    0f,
                    deceleration *
                    fixedDeltaTime);
        }

        private static float CalculateDownhillFactor(
            GroundInfo groundInfo,
            Vector3 slideDirection)
        {
            if (groundInfo.SurfaceAngle <=
                Mathf.Epsilon)
            {
                return 0f;
            }

            Vector3 downhillDirection =
                Vector3.ProjectOnPlane(
                    Vector3.down,
                    groundInfo.Normal);

            if (downhillDirection.sqrMagnitude <=
                MovementThreshold)
            {
                return 0f;
            }

            downhillDirection.Normalize();

            float downhillAlignment =
                Mathf.Max(
                    0f,
                    Vector3.Dot(
                        slideDirection,
                        downhillDirection));

            float slopeSteepness =
                Mathf.Sin(
                    groundInfo.SurfaceAngle *
                    Mathf.Deg2Rad);

            return
                downhillAlignment *
                slopeSteepness;
        }

        private void StopSlide(
            bool startCooldown = true)
        {
            if (!IsSliding)
            {
                return;
            }

            IsSliding = false;

            _slideDirection =
                Vector3.zero;

            _slideSpeed = 0f;
            _slideTimeRemaining = 0f;
            _slideGroundGraceRemaining = 0f;

            if (startCooldown)
            {
                _slideCooldownRemaining =
                    _config.SlideCooldown;
            }
        }

        private static Vector3 GetHorizontalVelocity(
            Vector3 velocity)
        {
            velocity.y = 0f;

            return velocity;
        }
    }
}