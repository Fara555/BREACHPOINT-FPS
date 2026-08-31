using UnityEngine;

namespace Breachpoint.Gameplay.Combat
{
    public interface IDamageable
    {
        void TakeDamage(DamageInfo damageInfo);
    }
}
