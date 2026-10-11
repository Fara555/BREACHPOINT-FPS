using UnityEngine;
using UnityEngine.AI;
namespace Breachpoint.Gameplay.AI
{
    public struct EnemyTacticalFacts
    {
        public Vector3 ThreatAim;
        public Vector3 ThreatPosition;
        public bool DirectSight;
        public bool FireLane;
        public bool RecentSuppression;
        public bool RecentContact;
        public bool ReloadNeeded;
        public bool CanCrouch;
        public float IntentAge;
        public EnemyCoverPoint CurrentCover;
        public EnemyCoverPoint FailedCover;
        public bool HasFailedPosition;
        public Vector3 FailedPosition;
        public EnemyTacticalCandidate Current;
    }
    public struct EnemyCoverEvaluation
    {
        public EnemyCoverPoint Point;
        public EnemyCoverRating Rating;
    }
    public sealed class EnemyTacticalPlanner
    {
        private const float OpenEngagementScore = 25f;
        private const float BlockedEngagementScore = 15f;
        private const float OpenReloadScore = 35f;
        private const float FlankScore = 105f;
        private const float RepositionScore = 90f;
        private const float AlternateSidePenalty = 4f;
        private const float CoverEntryTolerance = .08f;
        private readonly EnemyContext _context;
        private readonly EnemyTacticalConfig _config;
        private readonly EnemyCoverService _covers;
        private readonly EnemySquadService _squads;
        private readonly EnemyPhysics _physics = new EnemyPhysics();
        private readonly Vector3[] _allies = new Vector3[64];
        private readonly EnemyTacticalCandidate[] _candidates = new EnemyTacticalCandidate[16];
        private readonly int _areaMask;
        private int _coverCursor;
        private readonly int _identityHash;
        private readonly EnemyHighCoverPresentation _highMotion;
        public EnemyTacticalCandidate[] Candidates => _candidates;
        public int Count { get; private set; }
        private readonly EnemyCoverEvaluation[] _evaluatedCovers = new EnemyCoverEvaluation[9];
        public EnemyCoverEvaluation[] EvaluatedCovers => _evaluatedCovers;
        public int CoverEvaluationCount { get; private set; }
        public EnemyCoverRating CurrentCoverRating { get; private set; }
        public EnemyTacticalPlanner(EnemyContext context, EnemyCoverService covers, EnemySquadService squads, EnemyTacticalConfig config)
        { _context = context; _covers = covers; _squads = squads; _config = config; _areaMask = context.Actor.GetComponent<NavMeshAgent>().areaMask; _identityHash = context.Actor.GetEntityId().GetHashCode(); _highMotion = context.Actor.GetComponent<EnemyHighCoverPresentation>(); }
        public void Reset()
        { Count = CoverEvaluationCount = _coverCursor = 0; CurrentCoverRating = default; }
        public EnemyTacticalCandidate RescoredCurrent(EnemyTacticalCandidate current, bool retainMovement = true)
        {
            for (int i = 0; i < Count; i++)
                if (_candidates[i].Intent == current.Intent && _candidates[i].Cover == current.Cover && Vector3.Distance(_candidates[i].Destination, current.Destination) < 1f) return _candidates[i];
            bool moving = current.Intent == EnemyTacticalIntent.Advance || current.Intent == EnemyTacticalIntent.Reposition || current.Intent == EnemyTacticalIntent.FlankLeft || current.Intent == EnemyTacticalIntent.FlankRight || current.Intent == EnemyTacticalIntent.Fallback || current.Intent == EnemyTacticalIntent.Search;
            if (retainMovement && moving && current.Valid && _context.Navigation.Ready && !_context.Navigation.Failed && Vector3.Distance(_context.Actor.transform.position, current.Destination) > 0.65f) return current;
            current.Valid = false; return current;
        }
        public void Build(EnemyTacticalFacts facts, EnemySquad squad, EnemySquadMemberState member)
        {
            Count = CoverEvaluationCount = 0; CurrentCoverRating = default;
            Vector3 position = _context.Actor.transform.position;
            float distance = Vector3.Distance(position, facts.ThreatPosition);
            if (!_context.Navigation.Ready)
            { Add(EnemyTacticalIntent.WaitForLane, position, 1f, TacticalReason.NoCandidate); return; }
            int allies = _squads.FillAllyPositions(_context, _allies);
            bool blocksReservedCover = !_covers.PositionAvailable(position, _context, _config.PreferredSpacing);
            if (facts.DirectSight || facts.RecentSuppression)
            {
                // Standing in the open is only a fallback when useful paths are unavailable.
                float hold = facts.FireLane ? OpenEngagementScore : BlockedEngagementScore;
                if (!blocksReservedCover) Add(facts.DirectSight ? EnemyTacticalIntent.HoldEngage : EnemyTacticalIntent.Suppress, position, hold + (facts.RecentSuppression && !facts.DirectSight ? 10f : 0f), facts.FireLane ? TacticalReason.CleanLane : TacticalReason.LaneBlocked);
            }
            else AddSearch(facts, squad, member);
            EnemyCoverPoint bestCover = null; float bestScore = float.NegativeInfinity;
            if (facts.CurrentCover != null)
            {
                CurrentCoverRating = _covers.Evaluate(facts.CurrentCover, _context, facts.ThreatAim, _config.Cover, _allies, allies, _config.PreferredSpacing, _areaMask);
                CurrentCoverRating = ValidateEntry(facts.CurrentCover, CurrentCoverRating);
                // Brief ally crossings do not discard a committed, protected cover action.
                RecordCover(facts.CurrentCover, CurrentCoverRating);
                if (!CurrentCoverRating.Valid && facts.IntentAge < (_context.Tactics != null ? _context.Tactics.CoverCommitmentDuration : _config.PositionTimeout) &&
                    (CurrentCoverRating.Rejection == EnemyCoverRejection.NoFiringLane || CurrentCoverRating.Rejection == EnemyCoverRejection.Crowded))
                    CurrentCoverRating = new EnemyCoverRating { Valid = true, Score = facts.Current.Score - _config.CommittedCoverBonus, Rejection = CurrentCoverRating.Rejection };
                float coverRepositionAfter = Mathf.Max(_config.RepositionAfter, _context.Tactics != null ? _context.Tactics.CoverCommitmentDuration : _config.PositionTimeout);
                if (CurrentCoverRating.Valid) { bestCover = facts.CurrentCover; bestScore = CurrentCoverRating.Score + _config.CommittedCoverBonus - (facts.IntentAge >= coverRepositionAfter ? 50f : 0f); }
            }
            int points = _covers.Points.Count;
            for (int i = 0; i < Mathf.Min(_config.CandidateBudget, points); i++)
            {
                EnemyCoverPoint point = _covers.Points[_coverCursor++ % points];
                if (point == facts.CurrentCover || point == facts.FailedCover || point != null && point.Kind == EnemyCoverKind.Low && !facts.CanCrouch) continue;
                var rating = _covers.Evaluate(point, _context, facts.ThreatAim, _config.Cover, _allies, allies, _config.PreferredSpacing, _areaMask);
                rating = ValidateEntry(point, rating);
                RecordCover(point, rating);
                if (rating.Valid && rating.Score + _config.CoverPreferenceBonus > bestScore) { bestCover = point; bestScore = rating.Score + _config.CoverPreferenceBonus; }
            }
            if (bestCover != null && distance >= _context.Config.Combat.MinimumRange + 0.5f)
            {
                var cover = new EnemyTacticalCandidate(facts.ReloadNeeded ? EnemyTacticalIntent.ReloadProtected : EnemyTacticalIntent.TakeCover, bestCover.ProtectedPosition, bestScore + (facts.ReloadNeeded ? 65f : 0f), facts.ReloadNeeded ? TacticalReason.ReloadNeeded : TacticalReason.CoverProtection) { Cover = bestCover };
                Add(cover);
            }
            if (facts.ReloadNeeded && distance >= _context.Config.Combat.MinimumRange + 0.5f) Add(EnemyTacticalIntent.ReloadProtected, position, OpenReloadScore, TacticalReason.ReloadNeeded);
            Vector3 away = position - facts.ThreatPosition; away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -_context.Actor.transform.forward;
            away.Normalize();
            if (distance < _context.Config.Combat.MinimumRange + 0.5f)
            {
                Vector3 fallback = position + away * Mathf.Max(4f, _context.Config.Combat.MinimumRange - distance + 2f);
                AddPosition(EnemyTacticalIntent.Fallback, fallback, 140f, TacticalReason.TooClose, squad, member, 0, facts, false);
                AddPosition(EnemyTacticalIntent.Fallback, position + Quaternion.Euler(0, 60f, 0) * away * 5f, 130f, TacticalReason.TooClose, squad, member, 0, facts, false);
            }
            else if ((facts.DirectSight || facts.RecentContact) && distance > _context.Config.Combat.PreferredRange + 2f)
            {
                Vector3 goal = facts.ThreatPosition + away * _context.Config.Combat.PreferredRange;
                Vector3 travel = goal - position;
                if (travel.magnitude > _config.MaximumTravel) goal = position + travel.normalized * _config.MaximumTravel;
                // Closing on a radio/occluded contact must not require an already
                // clear shooting lane at the next navigation waypoint.
                AddPosition(EnemyTacticalIntent.Advance, goal, _config.AdvanceScore, TacticalReason.OutOfRange, squad, member, 0, facts, false);
            }
            if (facts.DirectSight || facts.RecentContact || facts.RecentSuppression)
            {
                bool blocked = !facts.FireLane;
                bool canLeaveCover = facts.CurrentCover == null || _context.Tactics != null && facts.IntentAge >= _config.RepositionAfter && _context.Tactics.CanFlankFromCover;
                for (int side = -1; side <= 1; side += 2)
                {
                    float radius = blocked ? Mathf.Clamp(distance * .7f, _context.Config.Combat.MinimumRange + 2f, _context.Config.Combat.PreferredRange) : _context.Config.Combat.PreferredRange;
                    Vector3 flank = facts.ThreatPosition + Quaternion.Euler(0, side * (blocked ? 80f : 55f), 0) * away * radius;
                    if (Vector3.Distance(position, flank) < _config.FlankDistance * 0.4f) continue;
                    if (canLeaveCover && squad.MovementSupported(member, _context.Now))
                        AddPosition(side < 0 ? EnemyTacticalIntent.FlankLeft : EnemyTacticalIntent.FlankRight, flank, FlankScore + (blocked ? 45f : squad.HasPressure(member, _context.Now) ? 40f : 0f), TacticalReason.BetterAngle, squad, member, side, facts, facts.DirectSight && !blocked);
                }
                Vector3 lateral = position + Vector3.Cross(Vector3.up, away) * (_identityHash % 2 == 0 ? 3f : -3f);
                float repositionScore = blocksReservedCover ? _config.BlockedCoverRepositionScore : RepositionScore;
                AddPosition(EnemyTacticalIntent.Reposition, lateral, repositionScore, facts.FireLane ? TacticalReason.BetterAngle : TacticalReason.LaneBlocked, squad, member, 0, facts, true);
                AddPosition(EnemyTacticalIntent.Reposition, position * 2f - lateral, repositionScore - AlternateSidePenalty, TacticalReason.BetterAngle, squad, member, 0, facts, true);
            }
            Add(EnemyTacticalIntent.WaitForLane, position, 5f, TacticalReason.NoCandidate);
        }
        private void RecordCover(EnemyCoverPoint point, EnemyCoverRating rating)
        {
            if (CoverEvaluationCount < _evaluatedCovers.Length)
                _evaluatedCovers[CoverEvaluationCount++] = new EnemyCoverEvaluation { Point = point, Rating = rating };
        }
        private EnemyCoverRating ValidateEntry(EnemyCoverPoint point, EnemyCoverRating rating)
        {
            if (!rating.Valid || point.Kind != EnemyCoverKind.High || _highMotion == null || !_highMotion.CanDriveMotion) return rating;
            Vector3 entry = _highMotion.EntryPosition(point);
            if (!_covers.Reachable(_context.Actor.transform.position, entry, _areaMask, out var sampled, out _) ||
                Vector3.ProjectOnPlane(sampled - entry, Vector3.up).sqrMagnitude > CoverEntryTolerance * CoverEntryTolerance)
            { rating.Valid = false; rating.Rejection = EnemyCoverRejection.Unreachable; }
            return rating;
        }
        private void AddSearch(EnemyTacticalFacts facts, EnemySquad squad, EnemySquadMemberState member)
        {
            int first = Mathf.Abs(_identityHash % 8);
            for (int i = 0; i < 3; i++)
            {
                if (facts.RecentContact)
                {
                    Vector3 away = _context.Actor.transform.position - facts.ThreatPosition; away.y = 0f;
                    if (away.sqrMagnitude < .01f) away = -_context.Actor.transform.forward;
                    float sector = (first % 3 - 1) * 12f + (i - 1) * 18f;
                    Vector3 station = facts.ThreatPosition + Quaternion.Euler(0f, sector, 0f) * away.normalized * _context.Config.Combat.PreferredRange;
                    AddPosition(EnemyTacticalIntent.Reposition, station, 60f - i * 4f, TacticalReason.LostSight, squad, member, 0, facts, false);
                    continue;
                }
                float angle = (first + i) * 45f;
                Vector3 point = facts.ThreatPosition + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (3f + first % 3);
                AddPosition(EnemyTacticalIntent.Search, point, 60f - i * 4f, TacticalReason.LostSight, squad, member, 0, facts, false);
            }
        }
        private void AddPosition(EnemyTacticalIntent intent, Vector3 destination, float score, TacticalReason reason, EnemySquad squad, EnemySquadMemberState member, int side, EnemyTacticalFacts facts, bool futureLane)
        {
            if (!_covers.PositionAvailable(destination, _context, _config.PreferredSpacing)) return;
            if (facts.HasFailedPosition && Vector3.Distance(destination, facts.FailedPosition) < 1.5f) return;
            if (Vector3.Distance(destination, _context.Actor.transform.position) > _config.MaximumTravel || !squad.PositionAvailable(member, destination, side, _config.PreferredSpacing)) return;
            if (futureLane && !_physics.ClearSegment(destination + Vector3.up * 1.7f, facts.ThreatAim, _context.Config.Combat.HitMask, _context.Actor.transform, _context.Memory.Target != null ? _context.Memory.Target.transform : null)) return;
            if (!_covers.Reachable(_context.Actor.transform.position, destination, _areaMask, out var sampled, out float cost) || cost > _config.MaximumTravel * 1.5f) return;
            Add(intent, sampled, score - cost * 0.5f, reason);
        }
        private void Add(EnemyTacticalIntent intent, Vector3 position, float score, TacticalReason reason) => Add(new EnemyTacticalCandidate(intent, position, score, reason));
        private void Add(EnemyTacticalCandidate value) { if (Count < _candidates.Length) _candidates[Count++] = value; }
    }
}
