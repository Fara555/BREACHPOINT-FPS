using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace Breachpoint.Gameplay.AI
{
    public enum EnemyCoverPhase { Reserved, Moving, Occupied }
    public enum EnemyCoverRejection { None, Disabled, Reserved, Distance, WrongSide, NoProtection, NoFiringLane, Unreachable, Crowded }
    public sealed class EnemyCoverReservation
    {
        public EnemyContext Owner { get; internal set; }
        public EnemyCoverPhase Phase { get; internal set; }
        public float ExpiresAt { get; internal set; }
        public float ReachDeadline { get; internal set; }
    }
    public struct EnemyCoverRating
    {
        public bool Valid;
        public float Score;
        public float PathCost;
        public bool FiringLane;
        public EnemyCoverRejection Rejection;
    }
    public sealed class EnemyCoverService
    {
        private readonly List<EnemyCoverPoint> _points = new List<EnemyCoverPoint>(32);
        private readonly Dictionary<EnemyCoverPoint, EnemyCoverReservation> _reservations = new Dictionary<EnemyCoverPoint, EnemyCoverReservation>(32);
        private readonly List<EnemyCoverPoint> _release = new List<EnemyCoverPoint>(32);
        private readonly EnemyPhysics _physics = new EnemyPhysics();
        private readonly NavMeshPath _path = new NavMeshPath();
        private readonly Vector3[] _corners = new Vector3[32];
        private float _nextCleanup;
        public IReadOnlyList<EnemyCoverPoint> Points => _points;
        public int ReservationCount => _reservations.Count;
        public int PathQueries { get; private set; }
        public EnemyCoverService(EnemyCoverPoint[] points)
        { if (points != null) foreach (var point in points) Register(point); }
        public void Register(EnemyCoverPoint point) { if (point != null && !_points.Contains(point)) _points.Add(point); }
        public void Unregister(EnemyCoverPoint point) { _points.Remove(point); _reservations.Remove(point); }
        public EnemyCoverReservation Reservation(EnemyCoverPoint point) => point != null && _reservations.TryGetValue(point, out var value) ? value : null;
        public bool Available(EnemyCoverPoint point, EnemyContext owner)
        { var value = Reservation(point); return value == null || value.Owner == owner; }
        public bool Reserve(EnemyCoverPoint point, EnemyContext owner, EnemyCoverConfig config, float now)
        {
            Cleanup(now);
            if (point == null || !point.IsUsable || !Available(point, owner) || owner.Actor.Health.IsDead || !owner.Actor.gameObject.activeInHierarchy) return false;
            var existing = Reservation(point);
            if (existing != null) { existing.ExpiresAt = now + config.ReservationLease; return true; }
            ReleaseOwner(owner);
            _reservations.Add(point, new EnemyCoverReservation { Owner = owner, Phase = EnemyCoverPhase.Reserved, ExpiresAt = now + config.ReservationLease, ReachDeadline = now + config.ReachTimeout });
            return true;
        }
        public bool Pulse(EnemyCoverPoint point, EnemyContext owner, EnemyCoverConfig config, float now, bool occupied)
        {
            Cleanup(now);
            var value = Reservation(point);
            if (value == null || value.Owner != owner || !point.IsUsable) return false;
            if (occupied && Vector3.Distance(owner.Actor.transform.position, point.ProtectedPosition) > 0.8f) return false;
            value.ExpiresAt = now + config.ReservationLease;
            value.Phase = occupied ? EnemyCoverPhase.Occupied : value.Phase == EnemyCoverPhase.Occupied ? EnemyCoverPhase.Occupied : EnemyCoverPhase.Moving;
            return true;
        }
        public void ReleaseOwner(EnemyContext owner)
        {
            _release.Clear();
            foreach (var pair in _reservations) if (pair.Value.Owner == owner) _release.Add(pair.Key);
            for (int i = 0; i < _release.Count; i++) _reservations.Remove(_release[i]);
        }
        public void Cleanup(float now)
        {
            if (now < _nextCleanup) return;
            _nextCleanup = now + 0.25f; _release.Clear();
            foreach (var pair in _reservations)
            {
                var value = pair.Value; var actor = value.Owner.Actor;
                if (pair.Key == null || !pair.Key.IsUsable || actor == null || !actor.gameObject.activeInHierarchy || actor.Health.IsDead || now >= value.ExpiresAt || value.Phase != EnemyCoverPhase.Occupied && now >= value.ReachDeadline) _release.Add(pair.Key);
            }
            for (int i = 0; i < _release.Count; i++) _reservations.Remove(_release[i]);
        }
        public bool PositionAvailable(Vector3 position, EnemyContext owner, float spacing)
        {
            foreach (var pair in _reservations)
            {
                if (pair.Value.Owner == owner || pair.Key == null) continue;
                if (Vector3.Distance(position, pair.Key.ProtectedPosition) < spacing || Vector3.Distance(position, pair.Key.ExposurePosition) < spacing) return false;
            }
            return true;
        }
        public bool Reachable(Vector3 origin, Vector3 destination, int areaMask, out Vector3 sampled, out float cost)
        {
            PathQueries++; sampled = destination; cost = 0f;
            if (!NavMesh.SamplePosition(destination, out var hit, 0.6f, areaMask) || Mathf.Abs(hit.position.y - destination.y) > 0.6f || !NavMesh.CalculatePath(origin, hit.position, areaMask, _path) || _path.status != NavMeshPathStatus.PathComplete) return false;
            int count = _path.GetCornersNonAlloc(_corners);
            if (count >= _corners.Length || count < 1) return false;
            for (int i = 1; i < count; i++) cost += Vector3.Distance(_corners[i - 1], _corners[i]);
            sampled = hit.position; return true;
        }
        public EnemyCoverRating Evaluate(EnemyCoverPoint point, EnemyContext owner, Vector3 threatAim, EnemyCoverConfig config, Vector3[] allies, int allyCount, float spacing, int areaMask)
        {
            var result = new EnemyCoverRating { Score = float.NegativeInfinity };
            if (point == null || !point.IsUsable) { result.Rejection = EnemyCoverRejection.Disabled; return result; }
            if (!Available(point, owner)) { result.Rejection = EnemyCoverRejection.Reserved; return result; }
            Vector3 position = point.ProtectedPosition;
            if (Vector3.Distance(owner.Actor.transform.position, position) > config.SearchRadius) { result.Rejection = EnemyCoverRejection.Distance; return result; }
            Vector3 toward = threatAim - position; toward.y = 0f;
            if (toward.sqrMagnitude < 0.01f || Vector3.Dot(point.Normal, toward.normalized) < config.MinimumProtectionDot) { result.Rejection = EnemyCoverRejection.WrongSide; return result; }
            float height = point.Kind == EnemyCoverKind.Low ? 1.05f : 1.7f;
            Vector3 protectedEye = position + Vector3.up * height;
            Vector3 delta = threatAim - protectedEye;
            int mask = owner.Config.Perception.ObstructionMask;
            if (!_physics.TryFirstHit(protectedEye, delta.normalized, delta.magnitude, mask, owner.Actor.transform, out var hit) || hit.collider != point.Protection) { result.Rejection = EnemyCoverRejection.NoProtection; return result; }
            Transform target = owner.Memory.Target != null ? owner.Memory.Target.transform : null;
            result.FiringLane = _physics.ClearSegment(point.ExposurePosition + Vector3.up * 1.7f, threatAim, mask, owner.Actor.transform, target) &&
                _physics.ClearSegment(point.ExposurePosition + Vector3.up * config.StandingMuzzleHeight, threatAim, mask, owner.Actor.transform, target);
            if (!result.FiringLane) { result.Rejection = EnemyCoverRejection.NoFiringLane; return result; }
            if (!Reachable(owner.Actor.transform.position, position, areaMask, out _, out result.PathCost) || !Reachable(position, point.ExposurePosition, areaMask, out _, out _)) { result.Rejection = EnemyCoverRejection.Unreachable; return result; }
            float crowd = 0f;
            for (int i = 0; i < allyCount; i++)
            {
                float distance = Vector3.Distance(position, allies[i]);
                if (distance < spacing * 0.5f) { result.Rejection = EnemyCoverRejection.Crowded; return result; }
                if (distance < spacing * 2f) crowd += 1f - distance / (spacing * 2f);
            }
            float rangeError = Mathf.Abs(Vector3.Distance(position, threatAim) - owner.Config.Combat.PreferredRange);
            Vector3 midpoint = (owner.Actor.transform.position + position) * 0.5f + Vector3.up * 1.7f;
            bool exposedTravel = _physics.ClearSegment(midpoint, threatAim, mask, owner.Actor.transform, target);
            result.Score = config.ProtectionWeight + config.FiringWeight - result.PathCost * config.PathCostWeight - rangeError * config.RangeErrorWeight - crowd * config.CrowdingPenalty - (exposedTravel ? config.TravelExposurePenalty : 0f);
            result.Valid = true; return result;
        }
    }
}
