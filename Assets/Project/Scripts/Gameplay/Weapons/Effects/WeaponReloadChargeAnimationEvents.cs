using UnityEngine;

namespace Breachpoint.Gameplay.Weapons.Effects
{
    public sealed class WeaponReloadChargeAnimationEvents : MonoBehaviour
    {
        [SerializeField]
        private WeaponReloadChargeVfx _reloadChargeVfx;

        public void BeginFistCharge()
        {
            if (_reloadChargeVfx != null)
            {
                _reloadChargeVfx.BeginFistCharge();
            }
        }

        public void EndFistCharge()
        {
            if (_reloadChargeVfx != null)
            {
                _reloadChargeVfx.EndFistCharge();
            }
        }
    }
}
