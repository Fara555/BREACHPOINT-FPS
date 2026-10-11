using System.Collections.Generic;
using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    public sealed class EnemySquadService
    {
        private readonly Dictionary<string, EnemySquad> _squads = new Dictionary<string, EnemySquad>(8);
        private readonly Dictionary<EnemyContext, EnemySquad> _membership = new Dictionary<EnemyContext, EnemySquad>(32);
        private sealed class EncounterContact
        {
            public Faction Faction;
            public EnemySharedContact Confirmed = new EnemySharedContact { SightTime = float.NegativeInfinity };
            public EnemySharedContact Pending = new EnemySharedContact { SightTime = float.NegativeInfinity };
            public float DeliverAt = float.PositiveInfinity;
        }
        private readonly Dictionary<string, EncounterContact> _encounters = new Dictionary<string, EncounterContact>(8);
        public IEnumerable<EnemySquad> Squads => _squads.Values;
        public EnemySquad Join(EnemyContext context, string id, EnemySquadConfig config, out EnemySquadMemberState member)
        {
            Leave(context, false);
            if (string.IsNullOrWhiteSpace(id)) id = "solo-" + context.Actor.GetEntityId();
            if (!_squads.TryGetValue(id, out var squad)) { squad = new EnemySquad(id, config); _squads.Add(id, squad); }
            member = squad.Join(context); _membership.Add(context, squad); return squad;
        }
        public void Leave(EnemyContext context, bool died)
        {
            if (!_membership.TryGetValue(context, out var squad)) return;
            string encounterId = null;
            for (int i = 0; i < squad.Members.Count; i++) if (squad.Members[i].Context == context)
            { encounterId = squad.Members[i].EncounterId; squad.Leave(squad.Members[i], died); break; }
            _membership.Remove(context); if (squad.Members.Count == 0) _squads.Remove(squad.Id);
            if (string.IsNullOrEmpty(encounterId)) return;
            foreach (var remaining in _squads.Values)
                for (int i = 0; i < remaining.Members.Count; i++)
                    if (remaining.Members[i].EncounterId == encounterId) return;
            _encounters.Remove(encounterId);
        }
        public void UpdateEncounter(EnemySquadMemberState sender, EnemySquadConfig config, float now)
        {
            if (!sender.Alive || string.IsNullOrEmpty(sender.EncounterId) || config.EncounterCommunicationRadius <= 0f) return;
            if (!_encounters.TryGetValue(sender.EncounterId, out var encounter))
            { encounter = new EncounterContact { Faction = sender.Context.Config.Faction }; _encounters.Add(sender.EncounterId, encounter); }
            if (encounter.Faction != sender.Context.Config.Faction) return;
            if (now >= encounter.DeliverAt)
            {
                if (encounter.Pending.Target != null && encounter.Pending.Target.IsAlive && encounter.Pending.SightTime > encounter.Confirmed.SightTime)
                    encounter.Confirmed = encounter.Pending;
                encounter.DeliverAt = float.PositiveInfinity;
            }
            EnemyBlackboard memory = sender.Context.Memory;
            // Only personal confirmed sight/damage publishes a newer observation.
            // Receiving a radio contact never refreshes its timestamp or hidden position.
            if (!memory.HasContact || memory.Target == null || !memory.Target.IsAlive ||
                memory.LastSeenTime <= memory.SharedContactTime || now - memory.LastSeenTime > sender.Context.Config.Perception.Interval + .05f ||
                !float.IsPositiveInfinity(encounter.DeliverAt) || memory.LastSeenTime <= encounter.Pending.SightTime) return;
            encounter.Pending = new EnemySharedContact { Target = memory.Target, Position = memory.LastKnownPosition,
                AimPosition = memory.KnownAimPosition, SenderPosition = sender.Context.Actor.transform.position, SightTime = memory.LastSeenTime };
            encounter.DeliverAt = now + config.CommunicationLatency;
        }
        public bool TryEncounter(EnemySquadMemberState receiver, EnemySquadConfig config, float now, out EnemySharedContact contact)
        {
            contact = default;
            if (!receiver.Alive || string.IsNullOrEmpty(receiver.EncounterId) || config.EncounterCommunicationRadius <= 0f ||
                !_encounters.TryGetValue(receiver.EncounterId, out var encounter) || encounter.Faction != receiver.Context.Config.Faction) return false;
            contact = encounter.Confirmed;
            return contact.Target != null && contact.Target.IsAlive && now - contact.SightTime <= config.MemoryDuration &&
                (receiver.Context.Actor.transform.position - contact.SenderPosition).sqrMagnitude <= config.EncounterCommunicationRadius * config.EncounterCommunicationRadius;
        }
        public int FillAllyPositions(EnemyContext except, Vector3[] positions)
        {
            int count = 0;
            foreach (var pair in _membership)
            {
                if (pair.Key == except || pair.Key.Actor == null || pair.Key.Actor.Health.IsDead || !pair.Key.Actor.gameObject.activeInHierarchy) continue;
                if (count == positions.Length) break;
                positions[count++] = pair.Key.Actor.transform.position;
            }
            return count;
        }
    }
}
