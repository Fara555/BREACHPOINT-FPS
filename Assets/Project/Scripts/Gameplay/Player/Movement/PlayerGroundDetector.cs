using UnityEngine;

namespace Breachpoint.Gameplay.Player.Movement
{
    public sealed class PlayerGroundDetector : MonoBehaviour
    {
        private const int GroundHitCapacity = 8;
        private const float ProbeStartOffset = 0.1f;

        [Header("References")]
        [SerializeField] private Transform _groundProbe;
        [SerializeField] private PlayerMovementConfig _config;

        private readonly RaycastHit[] _hits =
            new RaycastHit[GroundHitCapacity];

        public GroundInfo CurrentGround { get; private set; } =
            GroundInfo.None;

        private void Awake()
        {
            ValidateReferences();
        }

        public GroundInfo DetectGround()
        {
            if (_groundProbe == null || _config == null)
            {
                CurrentGround = GroundInfo.None;
                return CurrentGround;
            }

            Vector3 origin =
                _groundProbe.position +
                Vector3.up * ProbeStartOffset;

            float castDistance =
                _config.GroundProbeDistance +
                ProbeStartOffset;

            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                _config.GroundProbeRadius,
                Vector3.down,
                _hits,
                castDistance,
                _config.GroundMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0)
            {
                CurrentGround = GroundInfo.None;
                return CurrentGround;
            }

            bool hasWalkableHit = false;
            bool hasSteepHit = false;

            RaycastHit closestWalkableHit = default;
            RaycastHit closestSteepHit = default;

            float closestWalkableDistance =
                float.PositiveInfinity;

            float closestSteepDistance =
                float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _hits[i];

                if (!IsValidHit(hit))
                {
                    continue;
                }

                float correctedDistance = Mathf.Max(
                    0f,
                    hit.distance - ProbeStartOffset);

                float surfaceAngle = Vector3.Angle(
                    hit.normal,
                    Vector3.up);

                bool isWalkable =
                    surfaceAngle <=
                    _config.MaximumGroundAngle;

                if (isWalkable)
                {
                    if (correctedDistance >=
                        closestWalkableDistance)
                    {
                        continue;
                    }

                    closestWalkableHit = hit;
                    closestWalkableDistance =
                        correctedDistance;

                    hasWalkableHit = true;
                    continue;
                }

                if (correctedDistance >=
                    closestSteepDistance)
                {
                    continue;
                }

                closestSteepHit = hit;
                closestSteepDistance =
                    correctedDistance;

                hasSteepHit = true;
            }

            if (hasWalkableHit)
            {
                CurrentGround = CreateGroundInfo(
                    closestWalkableHit,
                    closestWalkableDistance,
                    true);

                return CurrentGround;
            }

            if (hasSteepHit)
            {
                CurrentGround = CreateGroundInfo(
                    closestSteepHit,
                    closestSteepDistance,
                    false);

                return CurrentGround;
            }

            CurrentGround = GroundInfo.None;
            return CurrentGround;
        }

        private bool IsValidHit(RaycastHit hit)
        {
            if (hit.collider == null)
            {
                return false;
            }

            return !hit.collider.transform.IsChildOf(
                transform);
        }

        private GroundInfo CreateGroundInfo(
            RaycastHit hit,
            float correctedDistance,
            bool isWalkable)
        {
            float surfaceAngle = Vector3.Angle(
                hit.normal,
                Vector3.up);

            bool isWithinGroundDistance =
                correctedDistance <=
                _config.GroundedDistance;

            return new GroundInfo(
                true,
                isWithinGroundDistance,
                isWalkable,
                hit.normal,
                hit.point,
                hit.collider,
                correctedDistance,
                surfaceAngle);
        }

        private void ValidateReferences()
        {
            if (_groundProbe == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerGroundDetector)} requires a GroundProbe reference.",
                    this);
            }

            if (_config == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerGroundDetector)} requires a PlayerMovementConfig reference.",
                    this);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_groundProbe == null || _config == null)
            {
                return;
            }

            Vector3 origin =
                _groundProbe.position +
                Vector3.up * ProbeStartOffset;

            Vector3 groundedPosition =
                origin +
                Vector3.down *
                (_config.GroundedDistance +
                 ProbeStartOffset);

            Vector3 maximumPosition =
                origin +
                Vector3.down *
                (_config.GroundProbeDistance +
                 ProbeStartOffset);

            Gizmos.DrawWireSphere(
                origin,
                _config.GroundProbeRadius);

            Gizmos.DrawWireSphere(
                groundedPosition,
                _config.GroundProbeRadius);

            Gizmos.DrawWireSphere(
                maximumPosition,
                _config.GroundProbeRadius);

            Gizmos.DrawLine(
                origin,
                maximumPosition);
        }
#endif
    }
}