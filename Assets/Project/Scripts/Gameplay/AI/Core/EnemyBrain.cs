using System;
using Breachpoint.Gameplay.Combat;
using UnityEngine;
using VContainer;

namespace Breachpoint.Gameplay.AI
{
    public sealed class EnemyBrain : MonoBehaviour
    {
        private EnemyContext _context;
        private EnemyPerception _perception;
        private EnemyDecisionPolicy _policy;
        private bool _started;
        private bool _subscribed;
        private float _nextTick;
        private float _lastTick;
        public EnemyStateMachine States { get; private set; }
        public EnemyBlackboard Memory => _context?.Memory;
        public int PerceptionCheckCount => _perception != null ? _perception.CheckCount : 0;
        public EnemyTacticalController Tactics => _context?.Tactics;
        public EnemyCombat Combat => _context?.Combat;
        public EnemyArchetypeConfig Config => _context?.Config;
        public event Action ResetCompleted;
        public event Action DeathCompleted;

        [Inject]
        public void Construct(EnemyContext context, EnemyPerception perception, EnemyDecisionPolicy policy, EnemyStateMachine states)
        {
            _context = context; _perception = perception; _policy = policy; States = states;
            states.Register(new IdleState(context)); states.Register(new PatrolState(context));
            states.Register(new InvestigateState(context)); states.Register(new ChaseState(context));
            states.Register(new SearchState(context)); states.Register(new CombatState(context));
            states.Register(new StunnedState(context)); states.Register(new DeadState(context));
        }
        private void Start() { _started = true; ResetForSpawn(); }
        private void OnEnable() { if (_started && _context != null) ResetForSpawn(); }
        public void ResetForSpawn()
        {
            if (_context == null) return;
            Unsubscribe(); Tactics?.Suspend(); States.Reset(); Memory.Reset(); Combat.Reset();
            _context.Actor.Health.ResetHealth();
            _context.Navigation.ResetAt(transform.position);
            _context.Now = Time.time; _lastTick = Time.time;
            _nextTick = Time.time + UnityEngine.Random.value * Config.Decision.TickInterval;
            _context.Actor.Health.Died += Die; _context.Actor.Health.DamageReceived += ReceiveDamage; _subscribed = true;
            _perception.Start(Time.time); Tactics?.Reset(enabled);
            States.Change(_policy.Evaluate(_context, States, out string reason), reason);
            ResetCompleted?.Invoke();
        }
        private void Update()
        {
            if (_context == null || _context.Actor.Health.IsDead) return;
            _context.Now = Time.time;
            _perception.Tick(Time.time); Combat.Tick(Time.time);
            if (Time.time < _nextTick) return;
            float dt = Time.time - _lastTick; _lastTick = Time.time;
            _nextTick = Time.time + Config.Decision.TickInterval;
            Tactics?.Observe(Time.time);
            States.Change(_policy.Evaluate(_context, States, out string reason), reason);
            States.Tick(dt);
        }
        public void Stun(float seconds)
        {
            if (_context == null || _context.Actor.Health.IsDead) return;
            Memory.StunnedUntil = Mathf.Max(Memory.StunnedUntil, Time.time + Mathf.Max(0f, seconds));
            _context.Now = Time.time;
            States.Change(_policy.Evaluate(_context, States, out string reason), reason);
        }
        private void ReceiveDamage(DamageInfo damage)
        {
            if (damage.Source == null) return;
            PerceptionTarget source = damage.Source.GetComponentInParent<PerceptionTarget>();
            if (source == null || !source.IsAlive || !Factions.AreHostile(Config.Faction, source.Faction)) return;
            Memory.Target = source; Memory.LastKnownPosition = source.transform.position;
            Memory.KnownAimPosition = source.AimPosition; Memory.LastSeenTime = Time.time; Memory.HasContact = true; Memory.Alert = 1f;
        }
        private void Die()
        {
            _context.Now = Time.time; _perception.Stop(); Tactics?.Suspend();
            States.Change(EnemyStateId.Dead, "health depleted");
            DeathCompleted?.Invoke();
        }
        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _context.Actor.Health.Died -= Die; _context.Actor.Health.DamageReceived -= ReceiveDamage; _subscribed = false;
        }
        private void OnDestroy() => Tactics?.Dispose();
        private void OnDisable()
        { Unsubscribe(); Tactics?.Suspend(); _perception?.Stop(); _context?.Combat.Stop(); _context?.Navigation.StopAndClearFacing(); }
    }
}
