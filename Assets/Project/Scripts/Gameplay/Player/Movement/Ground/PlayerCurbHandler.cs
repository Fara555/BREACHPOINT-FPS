using UnityEngine;
using VContainer;

namespace Breachpoint.Gameplay.Player.Movement
{
    public sealed class PlayerCurbHandler : MonoBehaviour
    {
        private const float MovementThreshold = 0.01f;
        private const float FootProbeHeight = 0.03f;
        private const float SurfaceProbePadding = 0.05f;
        private const float SurfaceProbeInset = 0.03f;
        private const float ClearancePadding = 0.02f;
        private const int ClearanceCapacity = 8;

        [Header("References")]
        [SerializeField]
        private Rigidbody _rigidbody;

        [SerializeField]
        private CapsuleCollider _capsuleCollider;

        private readonly Collider[] _clearanceResults =
            new Collider[ClearanceCapacity];

        private PlayerMovementConfig _config;

        [Inject]
        public void Construct(
            PlayerMovementConfig config)
        {
            _config = config;
        }

        private void Awake()
        {
            ValidateReferences();
        }

        public bool TryResolveCurb(
            Vector3 movementDirection,
            GroundInfo groundInfo,
            out float resolvedCurbHeight)
        {
            resolvedCurbHeight = 0f;

            if (!CanResolveCurb(
                    movementDirection,
                    groundInfo))
            {
                return false;
            }

            Vector3 direction =
                GetHorizontalDirection(
                    movementDirection);

            GetCapsuleGeometry(
                out Vector3 worldCenter,
                out Vector3 worldBottom,
                out float worldHeight,
                out float worldRadius);

            Vector3 obstacleProbeOrigin =
                worldBottom +
                Vector3.up *
                FootProbeHeight;

            float obstacleProbeDistance =
                worldRadius +
                _config.CurbCheckDistance;

            if (!Physics.Raycast(
                    obstacleProbeOrigin,
                    direction,
                    out RaycastHit obstacleHit,
                    obstacleProbeDistance,
                    _config.GroundMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (IsPlayerCollider(
                    obstacleHit.collider))
            {
                return false;
            }

            float obstacleAngle =
                Vector3.Angle(
                    obstacleHit.normal,
                    Vector3.up);

            if (obstacleAngle <=
                _config.MaximumGroundAngle)
            {
                return false;
            }

            Vector3 surfaceProbeOrigin =
                obstacleHit.point +
                direction *
                SurfaceProbeInset +
                Vector3.up *
                (_config.MaximumCurbHeight +
                 SurfaceProbePadding);

            float surfaceProbeDistance =
                _config.MaximumCurbHeight +
                SurfaceProbePadding * 2f;

            if (!Physics.Raycast(
                    surfaceProbeOrigin,
                    Vector3.down,
                    out RaycastHit surfaceHit,
                    surfaceProbeDistance,
                    _config.GroundMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (IsPlayerCollider(
                    surfaceHit.collider))
            {
                return false;
            }

            float surfaceAngle =
                Vector3.Angle(
                    surfaceHit.normal,
                    Vector3.up);

            if (surfaceAngle >
                _config.MaximumGroundAngle)
            {
                return false;
            }

            float curbHeight =
                Vector3.Dot(
                    surfaceHit.point -
                    worldBottom,
                    Vector3.up);

            if (curbHeight <=
                    FootProbeHeight ||
                curbHeight >
                    _config.MaximumCurbHeight)
            {
                return false;
            }

            Vector3 positionOffset =
                Vector3.up *
                curbHeight;

            if (!HasClearance(
                    worldCenter,
                    worldHeight,
                    worldRadius,
                    positionOffset))
            {
                return false;
            }

            _rigidbody.position +=
                positionOffset;

            resolvedCurbHeight =
                curbHeight;

            return true;
        }

        private bool CanResolveCurb(
            Vector3 movementDirection,
            GroundInfo groundInfo)
        {
            return
                _rigidbody != null &&
                _capsuleCollider != null &&
                _config != null &&
                groundInfo.IsGrounded &&
                movementDirection.sqrMagnitude >
                MovementThreshold;
        }

        private bool HasClearance(
            Vector3 worldCenter,
            float worldHeight,
            float worldRadius,
            Vector3 positionOffset)
        {
            float clearanceRadius =
                Mathf.Max(
                    0.01f,
                    worldRadius -
                    ClearancePadding);

            float halfSegmentLength =
                Mathf.Max(
                    0f,
                    worldHeight * 0.5f -
                    clearanceRadius);

            Vector3 candidateCenter =
                worldCenter +
                positionOffset +
                Vector3.up *
                ClearancePadding;

            Vector3 upperPoint =
                candidateCenter +
                Vector3.up *
                halfSegmentLength;

            Vector3 lowerPoint =
                candidateCenter -
                Vector3.up *
                halfSegmentLength;

            int overlapCount =
                Physics.OverlapCapsuleNonAlloc(
                    upperPoint,
                    lowerPoint,
                    clearanceRadius,
                    _clearanceResults,
                    _config.GroundMask,
                    QueryTriggerInteraction.Ignore);

            for (int i = 0;
                 i < overlapCount;
                 i++)
            {
                Collider overlap =
                    _clearanceResults[i];

                if (overlap == null ||
                    IsPlayerCollider(
                        overlap))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private void GetCapsuleGeometry(
            out Vector3 worldCenter,
            out Vector3 worldBottom,
            out float worldHeight,
            out float worldRadius)
        {
            Vector3 scale =
                transform.lossyScale;

            float horizontalScale =
                Mathf.Max(
                    Mathf.Abs(scale.x),
                    Mathf.Abs(scale.z));

            float verticalScale =
                Mathf.Abs(scale.y);

            worldRadius =
                _capsuleCollider.radius *
                horizontalScale;

            worldHeight =
                Mathf.Max(
                    _capsuleCollider.height *
                    verticalScale,
                    worldRadius * 2f);

            worldCenter =
                transform.TransformPoint(
                    _capsuleCollider.center);

            worldBottom =
                worldCenter -
                Vector3.up *
                (worldHeight * 0.5f);
        }

        private bool IsPlayerCollider(
            Collider targetCollider)
        {
            if (targetCollider == null)
            {
                return false;
            }

            return
                targetCollider ==
                _capsuleCollider ||
                targetCollider.transform
                    .IsChildOf(transform);
        }

        private static Vector3 GetHorizontalDirection(
            Vector3 direction)
        {
            direction.y = 0f;

            return
                direction.sqrMagnitude >
                MovementThreshold
                    ? direction.normalized
                    : Vector3.zero;
        }

        private void ValidateReferences()
        {
            if (_rigidbody == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerCurbHandler)} requires Rigidbody.",
                    this);
            }

            if (_capsuleCollider == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerCurbHandler)} requires CapsuleCollider.",
                    this);
            }
        }
    }
}