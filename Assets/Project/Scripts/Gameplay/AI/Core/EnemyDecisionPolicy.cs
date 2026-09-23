namespace Breachpoint.Gameplay.AI
{
    public sealed class EnemyDecisionPolicy
    {
        public EnemyStateId Evaluate(EnemyContext c, EnemyStateMachine states, out string reason)
        {
            EnemyBlackboard b = c.Memory;
            if (c.Actor.Health.IsDead) { reason = "health depleted"; return EnemyStateId.Dead; }
            if (c.Now < b.StunnedUntil) { reason = "stunned"; return EnemyStateId.Stunned; }
            if (b.Visible && b.Target != null && b.Target.IsAlive && b.Alert >= c.Config.Decision.AlertThreshold)
            {
                bool inRange = UnityEngine.Vector3.Distance(c.Actor.Eyes.position, b.Target.AimPosition) <= c.Config.Combat.Range;
                reason = inRange ? "visible target in range" : "visible target out of range";
                return inRange ? EnemyStateId.Combat : EnemyStateId.Chase;
            }
            if (b.HasContact && c.Now - b.LastSeenTime <= c.Config.Perception.MemoryDuration)
            { reason = "last known target position"; return EnemyStateId.Chase; }
            if (b.HasContact) { reason = "contact lost"; return EnemyStateId.Search; }
            if (b.HasNoise && c.Now - b.NoiseTime <= c.Config.Decision.InvestigateDuration)
            { reason = "hostile noise"; return EnemyStateId.Investigate; }
            b.HasNoise = false;
            reason = "no threat";
            return c.Actor.Route != null && c.Actor.Route.Count > 0 ? EnemyStateId.Patrol : EnemyStateId.Idle;
        }
    }
}
