namespace Breachpoint.Gameplay.AI
{
    public sealed class EnemyTacticalPolicy
    {
        private readonly EnemyTacticalConfig _config;
        public EnemyTacticalPolicy(EnemyTacticalConfig config) => _config = config;
        public EnemyTacticalCandidate Choose(EnemyTacticalCandidate[] candidates, int count, EnemyTacticalCandidate current, float enteredAt, float now, bool force, out TacticalReason reason)
        {
            EnemyTacticalCandidate best = default;
            best.Score = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
                if (candidates[i].Valid && candidates[i].Score > best.Score) best = candidates[i];
            if (!best.Valid)
            {
                reason = TacticalReason.NoCandidate;
                return new EnemyTacticalCandidate(EnemyTacticalIntent.WaitForLane, current.Destination, 0f, reason);
            }
            if (!force && current.Valid)
            {
                if (now - enteredAt < _config.MinimumIntentDuration) { reason = TacticalReason.Commitment; return current; }
                if (best.Score <= current.Score + _config.ImprovementThreshold) { reason = TacticalReason.ImprovementTooSmall; return current; }
            }
            reason = !current.Valid ? TacticalReason.InvalidCurrent : best.Reason;
            return best;
        }
    }
}
