using System;
using UnityEngine;

namespace Breachpoint.Gameplay.AI
{
    public sealed class EnemyPerception : IDisposable
    {
        private readonly EnemyContext _context;
        private readonly EnemyWorld _world;
        private readonly EnemyPhysics _physics = new EnemyPhysics();
        private float _nextCheck;
        private bool _listening;
        public EnemyPerception(EnemyContext context, EnemyWorld world) { _context = context; _world = world; }
        public void Start(float now)
        {
            Stop(); _world.Noise += Hear; _listening = true;
            _nextCheck = now + UnityEngine.Random.value * _context.Config.Perception.Interval;
        }
        public void Stop() { if (_listening) _world.Noise -= Hear; _listening = false; }
        public void Dispose() => Stop();
        public void Tick(float now)
        {
            if (now < _nextCheck) return;
            EnemyPerceptionConfig config = _context.Config.Perception;
            _nextCheck = now + config.Interval;
            EnemyBlackboard memory = _context.Memory;
            memory.Visible = false;
            PerceptionTarget selected = null;
            float closest = config.ViewRange * config.ViewRange;
            var targets = _world.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                PerceptionTarget target = targets[i];
                if (!target.IsAlive || !Factions.AreHostile(_context.Config.Faction, target.Faction) ||
                    (config.TargetMask.value & (1 << target.gameObject.layer)) == 0) continue;
                Vector3 offset = target.AimPosition - _context.Actor.Eyes.position;
                float distance = offset.sqrMagnitude;
                if (distance > closest || Vector3.Angle(_context.Actor.transform.forward, offset) > config.ViewAngle * 0.5f) continue;
                if (!_physics.ClearLine(_context.Actor.Eyes.position, target, config.ObstructionMask, _context.Actor.transform)) continue;
                closest = distance; selected = target;
            }
            if (selected != null)
            {
                memory.Target = selected; memory.Visible = true; memory.HasContact = true;
                memory.LastKnownPosition = selected.transform.position; memory.LastSeenTime = now;
                memory.Alert = Mathf.Min(1f, memory.Alert + config.AlertRise * config.Interval);
            }
            else
            {
                memory.Alert = Mathf.Max(0f, memory.Alert - config.AlertFall * config.Interval);
                if (memory.Target != null && !memory.Target.IsAlive) { memory.Target = null; memory.HasContact = false; }
            }
        }
        private void Hear(NoiseStimulus noise)
        {
            if (!Factions.AreHostile(_context.Config.Faction, noise.Faction)) return;
            float range = Mathf.Min(noise.Radius, _context.Config.Perception.HearingRange);
            if ((_context.Actor.transform.position - noise.Position).sqrMagnitude > range * range) return;
            _context.Memory.HasNoise = true; _context.Memory.NoisePosition = noise.Position; _context.Memory.NoiseTime = noise.Time;
        }
    }
}
