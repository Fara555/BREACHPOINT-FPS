using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    public abstract class EnemyState : IEnemyState
    {
        protected readonly EnemyContext C;
        public abstract EnemyStateId Id { get; }
        protected EnemyState(EnemyContext context) => C = context;
        public virtual void Enter() { C.Memory.StateEnteredAt = C.Now; }
        public abstract void Tick(float deltaTime);
        public virtual void Exit() => C.Navigation.Stop();
    }
    public sealed class IdleState : EnemyState
    {
        public IdleState(EnemyContext c) : base(c) { }
        public override EnemyStateId Id => EnemyStateId.Idle;
        public override void Enter() { base.Enter(); C.Navigation.Stop(); }
        public override void Tick(float dt) { }
    }
    public sealed class PatrolState : EnemyState
    {
        private float _waitUntil;
        public PatrolState(EnemyContext c) : base(c) { }
        public override EnemyStateId Id => EnemyStateId.Patrol;
        public override void Enter() { base.Enter(); _waitUntil = 0f; }
        public override void Tick(float dt)
        {
            if (C.Now < _waitUntil || C.Actor.Route == null) return;
            if (!C.Actor.Route.TryGetPoint(C.Memory.PatrolIndex, out Vector3 point)) { Advance(); return; }
            C.Navigation.MoveTo(point, false, C.Now);
            if (C.Navigation.Arrived || C.Navigation.Failed) Advance();
        }
        private void Advance()
        { C.Memory.PatrolIndex++; C.Navigation.Stop(); _waitUntil = C.Now + C.Config.Movement.PatrolWait; }
    }
    public sealed class InvestigateState : EnemyState
    {
        public InvestigateState(EnemyContext c) : base(c) { }
        public override EnemyStateId Id => EnemyStateId.Investigate;
        public override void Tick(float dt)
        {
            C.Navigation.MoveTo(C.Memory.NoisePosition, false, C.Now);
            if (C.Now - C.Memory.NoiseTime >= C.Config.Decision.InvestigateDuration) C.Memory.HasNoise = false;
        }
    }
    public sealed class ChaseState : EnemyState
    {
        public ChaseState(EnemyContext c) : base(c) { }
        public override EnemyStateId Id => EnemyStateId.Chase;
        public override void Tick(float dt) => C.Navigation.MoveTo(C.Memory.LastKnownPosition, true, C.Now);
    }
    public sealed class SearchState : EnemyState
    {
        public SearchState(EnemyContext c) : base(c) { }
        public override EnemyStateId Id => EnemyStateId.Search;
        public override void Tick(float dt)
        {
            C.Navigation.MoveTo(C.Memory.LastKnownPosition, false, C.Now);
            if (C.Navigation.Arrived || C.Navigation.Failed)
                C.Navigation.Face(C.Actor.transform.position + Quaternion.Euler(0, (C.Now - C.Memory.StateEnteredAt) * 70f, 0) * Vector3.forward, dt);
            if (C.Now - C.Memory.StateEnteredAt >= C.Config.Decision.SearchDuration)
            { C.Memory.HasContact = C.Memory.HasNoise = false; C.Memory.Target = null; C.Memory.Alert = 0f; }
        }
    }
    public sealed class CombatState : EnemyState
    {
        public CombatState(EnemyContext c) : base(c) { }
        public override EnemyStateId Id => EnemyStateId.Combat;
        public override void Tick(float dt)
        {
            PerceptionTarget target = C.Memory.Target;
            if (target == null || !target.IsAlive || !C.Memory.Visible) { C.Combat.Stop(); return; }
            Vector3 offset = C.Actor.transform.position - target.transform.position;
            float distance = offset.magnitude;
            EnemyCombatConfig config = C.Config.Combat;
            if (distance > config.PreferredRange + 0.5f)
                C.Navigation.MoveTo(target.transform.position + offset.normalized * config.PreferredRange, true, C.Now);
            else if (distance < config.MinimumRange)
                C.Navigation.MoveTo(C.Actor.transform.position + offset.normalized * (config.MinimumRange - distance + 1f), false, C.Now);
            else C.Navigation.Stop();
            C.Navigation.Face(target.AimPosition, dt);
            Vector3 facing = target.AimPosition - C.Actor.Eyes.position;
            facing.y = 0f;
            if (Vector3.Angle(C.Actor.transform.forward, facing) < 15f)
                C.Combat.Attack(target, C.Now);
        }
        public override void Exit() { base.Exit(); C.Combat.Stop(); }
    }
    public sealed class StunnedState : EnemyState
    {
        public StunnedState(EnemyContext c) : base(c) { }
        public override EnemyStateId Id => EnemyStateId.Stunned;
        public override void Enter() { base.Enter(); C.Navigation.Stop(); C.Combat.Stop(); }
        public override void Tick(float dt) { }
    }
    public sealed class DeadState : EnemyState
    {
        public DeadState(EnemyContext c) : base(c) { }
        public override EnemyStateId Id => EnemyStateId.Dead;
        public override void Enter() { base.Enter(); C.Navigation.Stop(); C.Combat.Stop(); C.Memory.Target = null; C.Memory.Visible = false; }
        public override void Tick(float dt) { }
    }
}
