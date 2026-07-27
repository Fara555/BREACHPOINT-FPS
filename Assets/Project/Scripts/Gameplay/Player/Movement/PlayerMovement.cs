using Breachpoint.Gameplay.Player.Input;
using UnityEngine;
using VContainer;

namespace Breachpoint.Gameplay.Player.Movement
{
    public sealed class PlayerMovement : MonoBehaviour
    {
        private const float StanceThreshold = 0.01f;
        private const int ClearanceResultCapacity = 8;

        [Header("References")]
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private CapsuleCollider _capsuleCollider;
        [SerializeField] private Transform _orientation;
        [SerializeField] private Transform _cameraRoot;
        [SerializeField] private PlayerGroundDetector _groundDetector;
        [SerializeField] private PlayerMovementConfig _config;

        private readonly RaycastHit[] _clearanceHits =
            new RaycastHit[ClearanceResultCapacity];

        private IPlayerInput _input;

        private Vector2 _moveInput;
        private bool _isSprintHeld;
        private bool _isCrouchHeld;
        private bool _jumpRequested;
        private bool _wantsToCrouch;

        private float _standingBottomLocalY;
        private float _targetColliderHeight;
        private bool _isStandingBlocked;

        public bool IsGrounded { get; private set; }
        public bool IsCrouching { get; private set; }

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
        }

        private void Update()
        {
            ReadInput();
            UpdateCameraHeight();
        }

        private void FixedUpdate()
        {
            if (!CanSimulate())
            {
                return;
            }

            UpdateStance();

            GroundInfo groundInfo = _groundDetector.DetectGround();
            IsGrounded = groundInfo.IsGrounded;

            Vector3 velocity = _rigidbody.linearVelocity;
            Vector3 horizontalVelocity = GetHorizontalVelocity(velocity);
            float verticalVelocity = velocity.y;

            horizontalVelocity = CalculateHorizontalVelocity(
                horizontalVelocity,
                groundInfo);

            verticalVelocity = CalculateVerticalVelocity(
                verticalVelocity,
                groundInfo);

            _rigidbody.linearVelocity =
                horizontalVelocity +
                Vector3.up * verticalVelocity;

            _jumpRequested = false;
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

            _isCrouchHeld = _input.IsCrouchHeld;
            _wantsToCrouch = _isCrouchHeld;

            _isSprintHeld =
                _input.IsSprintHeld &&
                !_wantsToCrouch &&
                !IsCrouching;

            if (_input.WasJumpPressed)
            {
                _jumpRequested = true;
            }
        }

        private void UpdateStance()
        {
            _isStandingBlocked =
                !_wantsToCrouch &&
                !CanStandUp();

            bool shouldCrouch =
                _wantsToCrouch ||
                _isStandingBlocked;

            _targetColliderHeight = shouldCrouch
                ? _config.CrouchingHeight
                : _config.StandingHeight;

            float heightStep =
                _config.StanceTransitionSpeed *
                Time.fixedDeltaTime;

            float newHeight = Mathf.MoveTowards(
                _capsuleCollider.height,
                _targetColliderHeight,
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

            _capsuleCollider.height = validHeight;

            Vector3 center = _capsuleCollider.center;

            center.y =
                _standingBottomLocalY +
                validHeight * 0.5f;

            _capsuleCollider.center = center;
        }

        private void UpdateCameraHeight()
        {
            if (_cameraRoot == null || _config == null)
            {
                return;
            }

            bool shouldUseCrouchingCamera =
                _wantsToCrouch ||
                IsCrouching ||
                _isStandingBlocked;

            float targetCameraY = shouldUseCrouchingCamera
                ? _config.CrouchingCameraLocalY
                : _config.StandingCameraLocalY;

            Vector3 localPosition =
                _cameraRoot.localPosition;

            localPosition.y = Mathf.MoveTowards(
                localPosition.y,
                targetCameraY,
                _config.CameraTransitionSpeed *
                Time.deltaTime);

            _cameraRoot.localPosition = localPosition;
        }

        private bool CanStandUp()
        {
            if (_capsuleCollider == null || _config == null)
            {
                return false;
            }

            float currentHeight =
                _capsuleCollider.height;

            float standingHeight =
                _config.StandingHeight;

            if (currentHeight >= standingHeight - StanceThreshold)
            {
                return true;
            }

            float radius = GetWorldCapsuleRadius();

            radius = Mathf.Max(
                0.01f,
                radius - _config.ClearancePadding);

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

            Vector3 castDirection =
                standingTopSphereCenter -
                currentTopSphereCenter;

            float castDistance =
                castDirection.magnitude;

            if (castDistance <= Mathf.Epsilon)
            {
                return true;
            }

            castDirection /= castDistance;

            int hitCount = Physics.SphereCastNonAlloc(
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

                if (hitCollider == _capsuleCollider)
                {
                    continue;
                }

                if (hitCollider.transform.IsChildOf(transform))
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

            localCenter.y = localCenterY;

            Vector3 worldCenter =
                transform.TransformPoint(localCenter);

            float verticalScale =
                Mathf.Abs(transform.lossyScale.y);

            float worldHeight =
                localHeight * verticalScale;

            float topOffset =
                Mathf.Max(
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

            return _capsuleCollider.radius *
                   horizontalScale;
        }

        private Vector3 CalculateHorizontalVelocity(
            Vector3 currentHorizontalVelocity,
            GroundInfo groundInfo)
        {
            Vector3 desiredDirection =
                GetDesiredDirection();

            if (groundInfo.IsGrounded &&
                desiredDirection.sqrMagnitude > 0f)
            {
                desiredDirection = Vector3.ProjectOnPlane(
                    desiredDirection,
                    groundInfo.Normal).normalized;
            }

            float targetSpeed =
                GetTargetSpeed();

            Vector3 targetVelocity =
                desiredDirection * targetSpeed;

            float acceleration = GetAcceleration(
                desiredDirection,
                groundInfo.IsGrounded);

            if (!groundInfo.IsGrounded)
            {
                targetVelocity = Vector3.Lerp(
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
            if (_jumpRequested &&
                groundInfo.IsGrounded &&
                !IsCrouching)
            {
                return CalculateJumpSpeed();
            }

            if (groundInfo.IsGrounded &&
                currentVerticalVelocity <= 0f)
            {
                return -_config.GroundedVerticalSpeed;
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
                return _config.CrouchSpeed;
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
                return _config.AirAcceleration;
            }

            return desiredDirection.sqrMagnitude > 0f
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
                _config.StandingCameraLocalY;

            _cameraRoot.localPosition =
                cameraPosition;

            _targetColliderHeight =
                _config.StandingHeight;

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
                RigidbodyInterpolation.Interpolate;

            _rigidbody.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;

            _rigidbody.constraints =
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationY |
                RigidbodyConstraints.FreezeRotationZ;
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

            float radius = GetWorldCapsuleRadius();

            radius = Mathf.Max(
                0.01f,
                radius - _config.ClearancePadding);

            float currentCenterY =
                _capsuleCollider.center.y;

            Vector3 currentTop =
                GetTopSphereCenter(
                    _capsuleCollider.height,
                    currentCenterY,
                    radius);

            float standingBottom =
                _config.StandingCenterY -
                _config.StandingHeight * 0.5f;

            float standingCenterY =
                standingBottom +
                _config.StandingHeight * 0.5f;

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