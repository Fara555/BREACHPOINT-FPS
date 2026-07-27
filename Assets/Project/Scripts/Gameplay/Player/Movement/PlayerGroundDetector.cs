using UnityEngine;

namespace Breachpoint.Gameplay.Player.Movement
{
    public sealed class PlayerGroundDetector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _groundProbe;
        [SerializeField] private PlayerMovementConfig _config;

        private readonly RaycastHit[] _hits = new RaycastHit[8];

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

            Vector3 origin = _groundProbe.position;

            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                _config.GroundProbeRadius,
                Vector3.down,
                _hits,
                _config.GroundProbeDistance,
                _config.GroundMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0)
            {
                CurrentGround = GroundInfo.None;
                return CurrentGround;
            }

            RaycastHit bestHit = default;
            bool hasValidHit = false;
            float shortestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _hits[i];

                if (hit.collider == null)
                {
                    continue;
                }

                if (hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                float surfaceAngle = Vector3.Angle(
                    hit.normal,
                    Vector3.up);

                if (surfaceAngle > _config.MaximumGroundAngle)
                {
                    continue;
                }

                if (hit.distance >= shortestDistance)
                {
                    continue;
                }

                bestHit = hit;
                shortestDistance = hit.distance;
                hasValidHit = true;
            }

            if (!hasValidHit)
            {
                CurrentGround = GroundInfo.None;
                return CurrentGround;
            }

            float bestSurfaceAngle = Vector3.Angle(
                bestHit.normal,
                Vector3.up);

            bool isGrounded =
                bestHit.distance <= _config.GroundedDistance;

            CurrentGround = new GroundInfo(
                isGrounded,
                bestHit.normal,
                bestHit.point,
                bestHit.collider,
                bestHit.distance,
                bestSurfaceAngle);

            return CurrentGround;
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

            Vector3 origin = _groundProbe.position;

            Vector3 groundedPosition =
                origin +
                Vector3.down * _config.GroundedDistance;

            Vector3 maximumPosition =
                origin +
                Vector3.down * _config.GroundProbeDistance;

            Gizmos.DrawWireSphere(
                origin,
                _config.GroundProbeRadius);

            Gizmos.DrawWireSphere(
                groundedPosition,
                _config.GroundProbeRadius);

            Gizmos.DrawWireSphere(
                maximumPosition,
                _config.GroundProbeRadius);

            Gizmos.DrawLine(origin, maximumPosition);
        }
#endif
    }
}