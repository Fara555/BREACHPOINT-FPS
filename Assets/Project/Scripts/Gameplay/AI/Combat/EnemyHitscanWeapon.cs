using System;
using Breachpoint.Gameplay.Combat;
using Breachpoint.Gameplay.Weapons;
using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    public sealed class EnemyHitscanWeapon : MonoBehaviour, IEnemyWeapon
    {
        private EnemyActor _actor;
        private EnemyCombatConfig _config;
        private readonly EnemyPhysics _physics = new EnemyPhysics();
        public bool UsesAmmunition => true;
        public event Action<WeaponShotResult> Attacked;
        public void Initialize(EnemyActor actor, EnemyCombatConfig config) { _actor = actor; _config = config; }
        public bool CanAttack(PerceptionTarget target) => target != null && target.IsAlive &&
            Factions.AreHostile(_actor.Target.Faction, target.Faction) &&
            Vector3.Distance(_actor.Muzzle.position, target.AimPosition) <= _config.Range &&
            !_physics.MuzzleBlocked(_actor.Muzzle.position, _config.HitMask, transform) &&
            _physics.ClearLine(_actor.Eyes.position, target, _config.HitMask, transform) &&
            _physics.ClearLine(_actor.Muzzle.position, target, _config.HitMask, transform);
        public bool Attack(PerceptionTarget target, float spreadMultiplier)
        {
            if (!CanAttack(target)) return false;
            Vector3 origin = _actor.Muzzle.position;
            Vector3 forward = (target.AimPosition - origin).normalized;
            Quaternion aim = Quaternion.LookRotation(forward);
            Vector2 spread = UnityEngine.Random.insideUnitCircle * Mathf.Tan(_config.SpreadAngle * spreadMultiplier * Mathf.Deg2Rad);
            Vector3 direction = (forward + aim * new Vector3(spread.x, spread.y, 0f)).normalized;
            RaycastHit hit;
            bool hasHit = _physics.TryFirstHit(origin, direction, _config.Range, _config.HitMask, transform, out hit);
            if (hasHit)
            {
                PerceptionTarget member = hit.collider.GetComponentInParent<PerceptionTarget>();
                if (member != null && Factions.AreHostile(_actor.Target.Faction, member.Faction))
                    member.Health.TakeDamage(new DamageInfo(_config.Damage, hit.point, direction, gameObject));
            }
            Attacked?.Invoke(new WeaponShotResult(origin, hasHit ? hit.point : origin + direction * _config.Range,
                hasHit ? hit.normal : Vector3.zero, hasHit ? hit.collider : null));
            return true;
        }
    }
}
