using System.Collections.Generic;
using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    public struct EnemySharedContact
    {
        public PerceptionTarget Target;
        public Vector3 Position;
        public Vector3 AimPosition;
        public Vector3 SenderPosition;
        public float SightTime;
    }
    public sealed class EnemySquadMemberState
    {
        public EnemyContext Context { get; }
        public EnemySquadRole Role { get; internal set; }
        public bool Shooter { get; internal set; }
        public bool Mover { get; internal set; }
        public bool CanPressure { get; set; }
        public bool RequiresExposure { get; set; }
        public float ShooterScore { get; internal set; }
        public float ShooterUntil { get; internal set; }
        public float MoverUntil { get; internal set; }
        public float RoleChangedAt { get; internal set; }
        public float LastShotAt { get; set; } = float.NegativeInfinity;
        public Vector3 ReservedPosition { get; internal set; }
        public int FlankSide { get; internal set; }
        internal EnemySharedContact PendingContact;
        internal float DeliverAt = float.PositiveInfinity;
        internal float NextPublish;
        public bool Alive => Context.Actor != null && Context.Actor.gameObject.activeInHierarchy && !Context.Actor.Health.IsDead;
        internal EnemySquadMemberState(EnemyContext context) => Context = context;
    }
    public sealed class EnemySquad
    {
        private readonly List<EnemySquadMemberState> _members = new List<EnemySquadMemberState>(32);
        private readonly EnemySquadConfig _config;
        private float _nextCleanup;
        public string Id { get; }
        public IReadOnlyList<EnemySquadMemberState> Members => _members;
        public EnemySharedContact SharedContact { get; private set; }
        public int Casualties { get; private set; }
        public int ActiveShooters { get; private set; }
        public int ActiveMovers { get; private set; }
        public EnemySquad(string id, EnemySquadConfig config)
        { Id = id; _config = config; SharedContact = new EnemySharedContact { SightTime = float.NegativeInfinity }; }
        public EnemySquadMemberState Join(EnemyContext context)
        {
            for (int i = 0; i < _members.Count; i++) if (_members[i].Context == context) return _members[i];
            var member = new EnemySquadMemberState(context); _members.Add(member); return member;
        }
        public void Leave(EnemySquadMemberState member, bool died)
        { ReleaseSlots(member); if (_members.Remove(member) && died) Casualties++; }
        public void UpdateCommunication(EnemySquadMemberState sender, float now)
        {
            Cleanup(now);
            for (int i = 0; i < _members.Count; i++)
            {
                var member = _members[i];
                if (member.Alive && now >= member.DeliverAt)
                {
                    if (member.PendingContact.Target != null && member.PendingContact.Target.IsAlive && member.PendingContact.SightTime > SharedContact.SightTime) SharedContact = member.PendingContact;
                    member.DeliverAt = float.PositiveInfinity;
                }
            }
            EnemyBlackboard memory = sender.Context.Memory;
            if (!sender.Alive || !memory.Visible || memory.Target == null || !memory.Target.IsAlive || now < sender.NextPublish || !float.IsPositiveInfinity(sender.DeliverAt)) return;
            sender.PendingContact = new EnemySharedContact { Target = memory.Target, Position = memory.LastKnownPosition, AimPosition = memory.KnownAimPosition, SightTime = memory.LastSeenTime, SenderPosition = sender.Context.Actor.transform.position };
            sender.DeliverAt = now + _config.CommunicationLatency; sender.NextPublish = sender.DeliverAt + 0.05f;
        }
        public bool TryShared(EnemySquadMemberState receiver, float now, out EnemySharedContact contact)
        {
            contact = SharedContact;
            return receiver.Alive && contact.Target != null && contact.Target.IsAlive && now - contact.SightTime <= _config.MemoryDuration &&
                Vector3.Distance(receiver.Context.Actor.transform.position, contact.SenderPosition) <= _config.CommunicationRadius;
        }
        public bool TryShooter(EnemySquadMemberState member, float score, float now)
        {
            Cleanup(now);
            if (!member.Alive || !member.CanPressure || member.Context.Combat.IsReloading || member.Context.Combat.Ammo <= 0)
            { ReleaseShooter(member); return false; }
            if (member.Mover) return false;
            if (member.Shooter) { member.ShooterUntil = now + _config.SlotLease; member.ShooterScore = score; return true; }
            if (ActiveShooters >= _config.MaximumShooters)
            {
                EnemySquadMemberState weakest = null;
                for (int i = 0; i < _members.Count; i++)
                    if (_members[i].Shooter && now - _members[i].RoleChangedAt >= _config.RoleCooldown && (weakest == null || _members[i].ShooterScore < weakest.ShooterScore)) weakest = _members[i];
                if (weakest == null || score <= weakest.ShooterScore + 8f) return false;
                ReleaseShooter(weakest);
            }
            member.Shooter = true; member.ShooterScore = score; member.ShooterUntil = now + _config.SlotLease;
            member.Role = EnemySquadRole.Anchor; member.RoleChangedAt = now; ActiveShooters++; return true;
        }
        public bool HasPressure(EnemySquadMemberState except, float now)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                var member = _members[i];
                if (member != except && member.Alive && member.Shooter && member.CanPressure && !member.RequiresExposure && !member.Context.Combat.IsReloading && now - member.LastShotAt < 1.5f) return true;
            }
            return false;
        }
        public bool MovementSupported(EnemySquadMemberState member, float now)
        {
            bool potential = false; int alive = 0;
            for (int i = 0; i < _members.Count; i++)
            { var other = _members[i]; if (!other.Alive) continue; alive++; if (other != member && other.CanPressure && !other.RequiresExposure && !other.Context.Combat.IsReloading && other.Context.Combat.Ammo > 0) potential = true; }
            return alive <= 1 || HasPressure(member, now) || !potential;
        }
        public bool PositionAvailable(EnemySquadMemberState member, Vector3 destination, int flankSide, float spacing)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                var other = _members[i]; if (other == member || !other.Alive) continue;
                if (other.Mover && (Vector3.Distance(other.ReservedPosition, destination) < spacing || flankSide != 0 && other.FlankSide == flankSide)) return false;
                if (Vector3.Distance(other.Context.Actor.transform.position, destination) < spacing * 0.5f) return false;
            }
            return true;
        }
        public bool TryMover(EnemySquadMemberState member, Vector3 destination, int flankSide, float spacing, float now, bool requirePressure = true)
        {
            Cleanup(now);
            if (!member.Alive || requirePressure && !MovementSupported(member, now) || !member.Mover && ActiveMovers >= _config.MaximumMovers) return false;
            for (int i = 0; i < _members.Count; i++)
            {
                var other = _members[i]; if (other == member || !other.Alive) continue;
                if (other.Mover && (Vector3.Distance(other.ReservedPosition, destination) < spacing || flankSide != 0 && other.FlankSide == flankSide)) return false;
                if (Vector3.Distance(other.Context.Actor.transform.position, destination) < spacing * 0.5f) return false;
            }
            ReleaseShooter(member);
            if (!member.Mover) { member.Mover = true; ActiveMovers++; member.RoleChangedAt = now; }
            member.MoverUntil = now + _config.SlotLease; member.ReservedPosition = destination; member.FlankSide = flankSide;
            member.Role = flankSide != 0 ? EnemySquadRole.Flanker : EnemySquadRole.Mover; return true;
        }
        public void ReleaseShooter(EnemySquadMemberState member)
        { if (member.Shooter) { member.Shooter = false; ActiveShooters--; } }
        public void ReleaseMovement(EnemySquadMemberState member)
        { if (member.Mover) { member.Mover = false; ActiveMovers--; } member.FlankSide = 0; member.ReservedPosition = Vector3.zero; }
        public void ReleaseSlots(EnemySquadMemberState member)
        { ReleaseShooter(member); ReleaseMovement(member); member.Role = EnemySquadRole.Support; }
        public void Cleanup(float now)
        {
            if (now < _nextCleanup) return; _nextCleanup = now + 0.1f;
            for (int i = 0; i < _members.Count; i++)
            {
                var member = _members[i];
                if (!member.Alive) { ReleaseSlots(member); member.DeliverAt = float.PositiveInfinity; continue; }
                if (member.Shooter && (now >= member.ShooterUntil || member.Context.Combat.IsReloading || member.Context.Combat.Ammo <= 0 || !member.CanPressure)) ReleaseShooter(member);
                if (member.Mover && now >= member.MoverUntil) ReleaseMovement(member);
            }
        }
    }
}
