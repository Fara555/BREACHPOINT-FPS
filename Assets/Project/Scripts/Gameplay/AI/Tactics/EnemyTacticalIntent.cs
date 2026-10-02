using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    public enum EnemyTacticalIntent { HoldEngage, Suppress, Advance, Reposition, FlankLeft, FlankRight, TakeCover, ReloadProtected, Fallback, Search, WaitForLane }
    public enum EnemySquadRole { Support, Anchor, Mover, Flanker, Searching, Reloading }
    public enum EnemyKnowledge { None, DirectSight, SharedConfirmedContact, LastKnownPosition, HeardPosition, SearchHypothesis }
    public enum TacticalReason { CleanLane, SharedPressure, OutOfRange, BetterAngle, CoverProtection, ReloadNeeded, TooClose, LostSight, LaneBlocked, NoCandidate, Commitment, ImprovementTooSmall, InvalidCurrent }
    public struct EnemyTacticalCandidate
    {
        public EnemyTacticalIntent Intent;
        public Vector3 Destination;
        public float Score;
        public bool Valid;
        public TacticalReason Reason;
        public EnemyCoverPoint Cover;
        public EnemyTacticalCandidate(EnemyTacticalIntent intent, Vector3 destination, float score, TacticalReason reason, bool valid = true)
        { Intent = intent; Destination = destination; Score = score; Reason = reason; Valid = valid; Cover = null; }
    }
}
