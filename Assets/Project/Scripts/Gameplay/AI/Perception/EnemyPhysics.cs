using UnityEngine;

namespace Breachpoint.Gameplay.AI
{
    public sealed class EnemyPhysics
    {
        private readonly RaycastHit[] _hits = new RaycastHit[32];
        private readonly Collider[] _overlaps = new Collider[16];
        public bool LastQuerySaturated { get; private set; }
        public bool TryFirstHit(Vector3 origin, Vector3 direction, float distance, int mask, Transform self, out RaycastHit nearest)
        {
            nearest = default;
            int count = Physics.RaycastNonAlloc(origin, direction, _hits, distance, mask, QueryTriggerInteraction.Ignore);
            LastQuerySaturated = count == _hits.Length;
            float closest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].transform.IsChildOf(self) || _hits[i].distance >= closest) continue;
                nearest = _hits[i]; closest = nearest.distance;
            }
            // Saturated queries fail closed in ClearLine; never shoot through an omitted obstruction.
            return count < _hits.Length && closest < float.PositiveInfinity;
        }
        public bool ClearSegment(Vector3 origin, Vector3 point, int mask, Transform self, Transform endpoint = null)
        {
            Vector3 delta = point - origin;
            if (delta.sqrMagnitude < 0.0001f) return false;
            int count = Physics.RaycastNonAlloc(origin, delta.normalized, _hits, delta.magnitude, mask, QueryTriggerInteraction.Ignore);
            if (count == _hits.Length) return false;
            float nearest = float.PositiveInfinity; Transform first = null;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].transform.IsChildOf(self) || _hits[i].distance >= nearest) continue;
                nearest = _hits[i].distance; first = _hits[i].transform;
            }
            return first == null || endpoint != null && first.IsChildOf(endpoint);
        }
        public bool ClearLine(Vector3 origin, PerceptionTarget target, int mask, Transform self)
        {
            Vector3 delta = target.AimPosition - origin;
            return delta.sqrMagnitude > 0.0001f &&
                TryFirstHit(origin, delta.normalized, delta.magnitude + 0.1f, mask, self, out RaycastHit hit) &&
                hit.transform.IsChildOf(target.transform);
        }
        public bool MuzzleBlocked(Vector3 origin, int mask, Transform self)
        {
            int count = Physics.OverlapSphereNonAlloc(origin, 0.07f, _overlaps, mask, QueryTriggerInteraction.Ignore);
            if (count == _overlaps.Length) return true;
            for (int i = 0; i < count; i++) if (!_overlaps[i].transform.IsChildOf(self)) return true;
            return false;
        }
    }
}
