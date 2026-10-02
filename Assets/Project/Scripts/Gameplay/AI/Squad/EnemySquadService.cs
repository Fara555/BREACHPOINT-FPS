using System.Collections.Generic;
using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    public sealed class EnemySquadService
    {
        private readonly Dictionary<string, EnemySquad> _squads = new Dictionary<string, EnemySquad>(8);
        private readonly Dictionary<EnemyContext, EnemySquad> _membership = new Dictionary<EnemyContext, EnemySquad>(32);
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
            for (int i = 0; i < squad.Members.Count; i++) if (squad.Members[i].Context == context) { squad.Leave(squad.Members[i], died); break; }
            _membership.Remove(context); if (squad.Members.Count == 0) _squads.Remove(squad.Id);
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
