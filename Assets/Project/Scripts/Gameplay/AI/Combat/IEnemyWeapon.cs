using System;
using Breachpoint.Gameplay.Weapons;
namespace Breachpoint.Gameplay.AI
{
    public interface IEnemyWeapon
    {
        bool UsesAmmunition { get; }
        event Action<WeaponShotResult> Attacked;
        void Initialize(EnemyActor actor, EnemyCombatConfig config);
        bool CanAttack(PerceptionTarget target);
        bool Attack(PerceptionTarget target, float spreadMultiplier);
    }
}
