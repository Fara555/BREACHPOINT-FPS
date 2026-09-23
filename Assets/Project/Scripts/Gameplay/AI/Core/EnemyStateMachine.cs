using System;
using System.Collections.Generic;

namespace Breachpoint.Gameplay.AI
{
    public enum EnemyStateId { Idle, Patrol, Investigate, Chase, Combat, Search, Stunned, Dead }
    public enum EnemyStateGroup { Passive, Suspicious, Combat, Disabled, Dead }

    public interface IEnemyState
    {
        EnemyStateId Id { get; }
        void Enter();
        void Tick(float deltaTime);
        void Exit();
    }

    // The root owns terminal death; leaf states never initiate transitions.
    public sealed class EnemyStateMachine
    {
        private readonly Dictionary<EnemyStateId, IEnemyState> _states = new Dictionary<EnemyStateId, IEnemyState>();
        private IEnemyState _current;
        private bool _transitioning;
        private bool _deathRequested;
        private readonly Queue<string> _history = new Queue<string>(16);
        public EnemyStateId Current => _current?.Id ?? EnemyStateId.Idle;
        public bool HasState => _current != null;
        public string LastReason { get; private set; }
        public IReadOnlyCollection<string> History => _history;
        public event Action<EnemyStateId, string> Changed;
        public EnemyStateGroup Group => Current == EnemyStateId.Dead ? EnemyStateGroup.Dead :
            Current == EnemyStateId.Stunned ? EnemyStateGroup.Disabled :
            Current == EnemyStateId.Combat || Current == EnemyStateId.Chase ? EnemyStateGroup.Combat :
            Current == EnemyStateId.Investigate || Current == EnemyStateId.Search ? EnemyStateGroup.Suspicious : EnemyStateGroup.Passive;

        public void Register(IEnemyState state) => _states.Add(state.Id, state);

        public bool Change(EnemyStateId id, string reason)
        {
            if (_transitioning)
            {
                if (id == EnemyStateId.Dead) _deathRequested = true;
                return false;
            }
            if (_current != null && (Current == id || Current == EnemyStateId.Dead)) return false;
            if (!_states.TryGetValue(id, out IEnemyState next)) throw new ArgumentException($"Unregistered enemy state: {id}");
            _transitioning = true;
            try
            {
                _current?.Exit();
                _current = next;
                LastReason = reason;
                if (_history.Count == 16) _history.Dequeue();
                _history.Enqueue(id + ": " + reason);
                _current.Enter();
                Changed?.Invoke(id, reason);
            }
            finally { _transitioning = false; }
            if (_deathRequested)
            {
                _deathRequested = false;
                Change(EnemyStateId.Dead, "death requested during transition");
            }
            return true;
        }

        public void Tick(float deltaTime) => _current?.Tick(deltaTime);
        public void Reset()
        {
            if (_transitioning) throw new InvalidOperationException("Cannot reset during a state transition.");
            _current?.Exit();
            _current = null;
            LastReason = null;
            _deathRequested = false;
            _history.Clear();
        }
    }
}
