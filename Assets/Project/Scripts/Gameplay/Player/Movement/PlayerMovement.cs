using Breachpoint.Gameplay.Player.Input;
using UnityEngine;
using VContainer;

namespace Breachpoint.Gameplay.Player.Movement
{
    public sealed class PlayerMovement : MonoBehaviour
    {
        private const float StanceThreshold = 0.01f;
        private const float MovementThreshold = 0.01f;
        private const int ClearanceHitCapacity = 8;

        [Header("References")]
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private CapsuleCollider _capsuleCollider;
        [SerializeField] private Transform _orientation;
        [SerializeField] private Transform _cameraRoot;
        [SerializeField] private PlayerGroundDetector _groundDetector;
        [SerializeField] private PlayerMovementConfig _config;

        private readonly RaycastHit[] _clearanceHits =
            new RaycastHit[ClearanceHitCapacity];

        private IPlayerInput _input;

        private Vector2 _moveInput;

        private bool _isSprintHeld;
        private bool _wantsToCrouch;
        private bool _isStandingBlocked;

        private float _standingBottomLocalY;
        private float _coyoteTimeRemaining;
        private float _jumpBufferTimeRemaining;

        public bool IsGrounded { get; private set; }
        public bool IsCrouching { get; private set; }

        public PlayerMovementState CurrentState { get; private set; }

        public Vector3 Velocity =>
            _rigidbody != null
                ? _rigidbody.linearVelocity
                : Vector3.zero;

        [Inject]
        public void Construct(IPlayerInput input)
        {
            _input = input;
        }

        private void Awake()
        {
            ValidateReferences();
            ConfigureRigidbody();
            InitializeStance();

            CurrentState = PlayerMovementState.Idle;
        }

        private void Update()
        {
            ReadInput();
            UpdateJumpBuffer();
            UpdateCameraHeight();
        }

        private void FixedUpdate()
        {
            if (!CanSimulate())
            {
                return;
            }

            UpdateStance();

            GroundInfo groundInfo =
                _groundDetector.DetectGround();

            IsGrounded = groundInfo.IsGrounded;

            UpdateCoyoteTime();

            Vector3 currentVelocity =
                _rigidbody.linearVelocity;

            Vector3 horizontalVelocity =
                GetHorizontalVelocity(currentVelocity);

            float verticalVelocity =
                currentVelocity.y;

            bool didJump = TryConsumeJump();

            horizontalVelocity =
                CalculateHorizontalVelocity(
                    horizontalVelocity,
                    groundInfo);

            verticalVelocity = didJump
                ? CalculateJumpSpeed()
                : CalculateVerticalVelocity(
                    verticalVelocity,
                    groundInfo);

            Vector3 finalVelocity =
                horizontalVelocity +
                Vector3.up * verticalVelocity;

            _rigidbody.linearVelocity =
                finalVelocity;

            UpdateMovementState(finalVelocity);
        }

        private void ReadInput()
        {
            if (_input == null)
            {
                return;
            }

            _moveInput = Vector2.ClampMagnitude(
                _input.Move,
                1f);

            _wantsToCrouch =
                _input.IsCrouchHeld;

            _isSprintHeld =
                _input.IsSprintHeld &&
                !_wantsToCrouch &&
                !IsCrouching;

            if (_input.WasJumpPressed)
            {
                _jumpBufferTimeRemaining =
                    _config.JumpBufferTime;
            }
        }

        private void UpdateJumpBuffer()
        {
            if (_jumpBufferTimeRemaining <= 0f)
            {
                return;
            }

            _jumpBufferTimeRemaining -=
                Time.deltaTime;
        }

        private void UpdateCoyoteTime()
        {
            if (IsGrounded)
            {
                _coyoteTimeRemaining =
                    _config.CoyoteTime;

                return;
            }

            _coyoteTimeRemaining = Mathf.Max(
                0f,
                _coyoteTimeRemaining -
                Time.fixedDeltaTime);
        }

        private bool TryConsumeJump()
        {
            bool hasBufferedJump =
                _jumpBufferTimeRemaining > 0f;

            bool canUseGroundJump =
                IsGrounded ||
                _coyoteTimeRemaining > 0f;

            if (!hasBufferedJump ||
                !canUseGroundJump ||
                IsCrouching ||
                _wantsToCrouch)
            {
                return false;
            }

            _jumpBufferTimeRemaining = 0f;
            _coyoteTimeRemaining = 0f;
            IsGrounded = false;

            return true;
        }

        private void UpdateStance()
        {
            _isStandingBlocked =
                !_wantsToCrouch &&
                !CanStandUp();

            bool shouldCrouch =
                _wantsToCrouch ||
                _isStandingBlocked;

            float targetHeight = shouldCrouch
                ? _config.CrouchingHeight
                : _config.StandingHeight;

            float heightStep =
                _config.StanceTransitionSpeed *
                Time.fixedDeltaTime;

            float newHeight = Mathf.MoveTowards(
                _capsuleCollider.height,
                targetHeight,
                heightStep);

            ApplyColliderHeight(newHeight);

            IsCrouching =
                shouldCrouch ||
                Mathf.Abs(
                    _capsuleCollider.height -
                    _config.StandingHeight) >
                StanceThreshold;
        }

        private void ApplyColliderHeight(float height)
        {
            float minimumHeight =
                _capsuleCollider.radius * 2f;

            float validHeight = Mathf.Max(
                height,
                minimumHeight);

            _capsuleCollider.height =
                validHeight;

            Vector3 center =
                _capsuleCollider.center;

            center.y =
                _standingBottomLocalY +
                validHeight * 0.5f;

            _capsuleCollider.center =
                center;
        }

        private void UpdateCameraHeight()
        {
            if (_cameraRoot == null ||
                _config == null)
            {
                return;
            }

            bool useCrouchingHeight =
                _wantsToCrouch ||
                IsCrouching ||
                _isStandingBlocked;

            float targetCameraY =
                useCrouchingHeight
                    ? _config.CrouchingCameraLocalY
                    : _config.StandingCameraLocalY;

            Vector3 localPosition =
                _cameraRoot.localPosition;

            localPosition.y = Mathf.MoveTowards(
                localPosition.y,
                targetCameraY,
                _config.CameraTransitionSpeed *
                Time.deltaTime);

            _cameraRoot.localPosition =
                localPosition;
        }

        private bool CanStandUp()
        {
            if (_capsuleCollider == null ||
                _config == null)
            {
                return false;
            }

            float currentHeight =
                _capsuleCollider.height;

            float standingHeight =
                _config.StandingHeight;

            if (currentHeight >=
                standingHeight - StanceThreshold)
            {
                return true;
            }

            float radius =
                GetWorldCapsuleRadius();

            radius = Mathf.Max(
                0.01f,
                radius -
                _config.ClearancePadding);

            Vector3 currentTopSphereCenter =
                GetTopSphereCenter(
                    currentHeight,
                    _capsuleCollider.center.y,
                    radius);

            float standingCenterY =
                _standingBottomLocalY +
                standingHeight * 0.5f;

            Vector3 standingTopSphereCenter =
                GetTopSphereCenter(
                    standingHeight,
                    standingCenterY,
                    radius);

            Vector3 castOffset =
                standingTopSphereCenter -
                currentTopSphereCenter;

            float castDistance =
                castOffset.magnitude;

            if (castDistance <= Mathf.Epsilon)
            {
                return true;
            }

            Vector3 castDirection =
                castOffset / castDistance;

            int hitCount =
                Physics.SphereCastNonAlloc(
                    currentTopSphereCenter,
                    radius,
                    castDirection,
                    _clearanceHits,
                    castDistance,
                    _config.ClearanceMask,
                    QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider =
                    _clearanceHits[i].collider;

                if (hitCollider == null)
                {
                    continue;
                }

                if (hitCollider ==
                    _capsuleCollider)
                {
                    continue;
                }

                if (hitCollider.transform
                    .IsChildOf(transform))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private Vector3 GetTopSphereCenter(
            float localHeight,
            float localCenterY,
            float worldRadius)
        {
            Vector3 localCenter =
                _capsuleCollider.center;

            localCenter.y =
                localCenterY;

            Vector3 worldCenter =
                transform.TransformPoint(
                    localCenter);

            float verticalScale =
                Mathf.Abs(
                    transform.lossyScale.y);

            float worldHeight =
                localHeight *
                verticalScale;

            float topOffset = Mathf.Max(
                0f,
                worldHeight * 0.5f -
                worldRadius);

            return worldCenter +
                   transform.up * topOffset;
        }

        private float GetWorldCapsuleRadius()
        {
            Vector3 scale =
                transform.lossyScale;

            float horizontalScale = Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.z));

            return
                _capsuleCollider.radius *
                horizontalScale;
        }

        private Vector3 CalculateHorizontalVelocity(
            Vector3 currentHorizontalVelocity,
            GroundInfo groundInfo)
        {
            Vector3 desiredDirection =
                GetDesiredDirection();

            if (groundInfo.IsGrounded &&
                desiredDirection.sqrMagnitude >
                MovementThreshold)
            {
                desiredDirection =
                    Vector3.ProjectOnPlane(
                        desiredDirection,
                        groundInfo.Normal)
                    .normalized;
            }

            float targetSpeed =
                GetTargetSpeed();

            Vector3 targetVelocity =
                desiredDirection *
                targetSpeed;

            float acceleration =
                GetAcceleration(
                    desiredDirection,
                    groundInfo.IsGrounded);

            if (!groundInfo.IsGrounded)
            {
                targetVelocity =
                    Vector3.Lerp(
                        currentHorizontalVelocity,
                        targetVelocity,
                        _config.AirControl);
            }

            return Vector3.MoveTowards(
                currentHorizontalVelocity,
                targetVelocity,
                acceleration *
                Time.fixedDeltaTime);
        }

        private float CalculateVerticalVelocity(
            float currentVerticalVelocity,
            GroundInfo groundInfo)
        {
            if (groundInfo.IsGrounded &&
                currentVerticalVelocity <= 0f)
            {
                return
                    -_config
                        .GroundedVerticalSpeed;
            }

            float nextVerticalVelocity =
                currentVerticalVelocity -
                _config.Gravity *
                Time.fixedDeltaTime;

            return Mathf.Max(
                nextVerticalVelocity,
                -_config.MaxFallSpeed);
        }

        private float GetTargetSpeed()
        {
            if (IsCrouching ||
                _wantsToCrouch ||
                _isStandingBlocked)
            {
                return
                    _config.CrouchSpeed;
            }

            return _isSprintHeld
                ? _config.SprintSpeed
                : _config.WalkSpeed;
        }

        private Vector3 GetDesiredDirection()
        {
            if (_orientation == null)
            {
                return Vector3.zero;
            }

            Vector3 forward =
                _orientation.forward;

            Vector3 right =
                _orientation.right;

            forward.y = 0f;
            right.y = 0f;

            forward.Normalize();
            right.Normalize();

            Vector3 direction =
                forward * _moveInput.y +
                right * _moveInput.x;

            return Vector3.ClampMagnitude(
                direction,
                1f);
        }

        private static Vector3 GetHorizontalVelocity(
            Vector3 velocity)
        {
            return new Vector3(
                velocity.x,
                0f,
                velocity.z);
        }

        private float GetAcceleration(
            Vector3 desiredDirection,
            bool isGrounded)
        {
            if (!isGrounded)
            {
                return
                    _config.AirAcceleration;
            }

            return desiredDirection.sqrMagnitude >
                   MovementThreshold
                ? _config.GroundAcceleration
                : _config.GroundDeceleration;
        }

        private float CalculateJumpSpeed()
        {
            return Mathf.Sqrt(
                2f *
                _config.Gravity *
                _config.JumpHeight);
        }

        private void UpdateMovementState(
            Vector3 velocity)
        {
            if (!IsGrounded)
            {
                CurrentState =
                    PlayerMovementState.Airborne;

                return;
            }

            if (IsCrouching ||
                _wantsToCrouch ||
                _isStandingBlocked)
            {
                CurrentState =
                    PlayerMovementState.Crouching;

                return;
            }

            Vector3 horizontalVelocity =
                GetHorizontalVelocity(velocity);

            bool isMoving =
                horizontalVelocity.sqrMagnitude >
                MovementThreshold;

            if (!isMoving)
            {
                CurrentState =
                    PlayerMovementState.Idle;

                return;
            }

            CurrentState = _isSprintHeld
                ? PlayerMovementState.Sprinting
                : PlayerMovementState.Walking;
        }

        private void InitializeStance()
        {
            if (_capsuleCollider == null ||
                _cameraRoot == null ||
                _config == null)
            {
                return;
            }

            _standingBottomLocalY =
                _config.StandingCenterY -
                _config.StandingHeight *
                0.5f;

            ApplyColliderHeight(
                _config.StandingHeight);

            Vector3 cameraPosition =
                _cameraRoot.localPosition;

            cameraPosition.y =
                _config
                    .StandingCameraLocalY;

            _cameraRoot.localPosition =
                cameraPosition;

            IsCrouching = false;
            _isStandingBlocked = false;
        }

        private bool CanSimulate()
        {
            return
                _rigidbody != null &&
                _capsuleCollider != null &&
                _orientation != null &&
                _cameraRoot != null &&
                _groundDetector != null &&
                _config != null;
        }

        private void ConfigureRigidbody()
        {
            if (_rigidbody == null)
            {
                return;
            }

            _rigidbody.useGravity = false;

            _rigidbody.interpolation =
                RigidbodyInterpolation
                    .Interpolate;

            _rigidbody
                .collisionDetectionMode =
                CollisionDetectionMode
                    .ContinuousDynamic;

            _rigidbody.constraints =
                RigidbodyConstraints
                    .FreezeRotationX |
                RigidbodyConstraints
                    .FreezeRotationY |
                RigidbodyConstraints
                    .FreezeRotationZ;
        }

        private void ValidateReferences()
        {
            if (_rigidbody == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerMovement)} requires a Rigidbody reference.",
                    this);
            }

            if (_capsuleCollider == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerMovement)} requires a CapsuleCollider reference.",
                    this);
            }

            if (_orientation == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerMovement)} requires an Orientation reference.",
                    this);
            }

            if (_cameraRoot == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerMovement)} requires a CameraRoot reference.",
                    this);
            }

            if (_groundDetector == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerMovement)} requires a PlayerGroundDetector reference.",
                    this);
            }

            if (_config == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerMovement)} requires a PlayerMovementConfig reference.",
                    this);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_capsuleCollider == null ||
                _config == null)
            {
                return;
            }

            float radius =
                GetWorldCapsuleRadius();

            radius = Mathf.Max(
                0.01f,
                radius -
                _config.ClearancePadding);

            Vector3 currentTop =
                GetTopSphereCenter(
                    _capsuleCollider.height,
                    _capsuleCollider.center.y,
                    radius);

            float standingBottom =
                _config.StandingCenterY -
                _config.StandingHeight *
                0.5f;

            float standingCenterY =
                standingBottom +
                _config.StandingHeight *
                0.5f;

            Vector3 standingTop =
                GetTopSphereCenter(
                    _config.StandingHeight,
                    standingCenterY,
                    radius);

            Gizmos.DrawWireSphere(
                currentTop,
                radius);

            Gizmos.DrawWireSphere(
                standingTop,
                radius);

            Gizmos.DrawLine(
                currentTop,
                standingTop);
        }
#endif
    }
}