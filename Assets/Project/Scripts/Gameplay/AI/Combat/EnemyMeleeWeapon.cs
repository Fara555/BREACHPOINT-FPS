using System;
using Breachpoint.Gameplay.Combat;
using Breachpoint.Gameplay.Weapons;
using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    public sealed class EnemyMeleeWeapon : MonoBehaviour, IEnemyWeapon
    {
        private EnemyActor _actor;
        private EnemyCombatConfig _config;
        private readonly EnemyPhysics _physics = new EnemyPhysics();
        public bool UsesAmmunition => false;
        public event Action<WeaponShotResult> Attacked;
        public void Initialize(EnemyActor actor, EnemyCombatConfig config) { _actor = actor; _config = config; }
        public bool CanAttack(PerceptionTarget target) => target != null && target.IsAlive &&
            Factions.AreHostile(_actor.Target.Faction, target.Faction) &&
            Vector3.Distance(_actor.Eyes.position, target.AimPosition) <= _config.Range &&
            _physics.ClearLine(_actor.Eyes.position, target, _config.HitMask, transform);
        public bool Attack(PerceptionTarget target, float spreadMultiplier)
        {
            if (!CanAttack(target)) return false;
            Vector3 origin = _actor.Eyes.position;
            target.Health.TakeDamage(new DamageInfo(_config.Damage, target.AimPosition,
                (target.AimPosition - origin).normalized, gameObject));
            Attacked?.Invoke(new WeaponShotResult(origin, target.AimPosition, Vector3.zero, null));
            return true;
        }
    }
}
