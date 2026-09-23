using System;
using System.Collections.Generic;
using UnityEngine;

namespace Breachpoint.Gameplay.AI
{
    public enum Faction { Neutral, Player, Hostile, Friendly }
    public static class Factions
    {
        public static bool AreHostile(Faction a, Faction b) =>
            a != Faction.Neutral && b != Faction.Neutral && a != b &&
            (a == Faction.Hostile || b == Faction.Hostile);
    }

    public readonly struct NoiseStimulus
    {
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly float Time;
        public readonly Faction Faction;
        public NoiseStimulus(Vector3 position, float radius, Faction faction, float time)
        { Position = position; Radius = radius; Faction = faction; Time = time; }
    }

    // Owned by the scene's VContainer scope, never a static singleton.
    public sealed class EnemyWorld
    {
        private readonly List<PerceptionTarget> _targets = new List<PerceptionTarget>(32);
        public IReadOnlyList<PerceptionTarget> Targets => _targets;
        public event Action<NoiseStimulus> Noise;
        public void Register(PerceptionTarget target) { if (!_targets.Contains(target)) _targets.Add(target); }
        public void Unregister(PerceptionTarget target) => _targets.Remove(target);
        public void Emit(NoiseStimulus stimulus) => Noise?.Invoke(stimulus);
    }
}
