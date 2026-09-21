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

        public void BeginFistGlow()
        {
            if (_reloadChargeVfx != null)
            {
                _reloadChargeVfx.BeginFistGlow();
            }
        }

        public void BeginEnergyTransfer()
        {
            if (_reloadChargeVfx != null)
            {
                _reloadChargeVfx.BeginEnergyTransfer();
            }
        }

        public void CompleteEnergyTransfer()
        {
            if (_reloadChargeVfx != null)
            {
                _reloadChargeVfx.CompleteEnergyTransfer();
            }
        }

        public void EndFistChargeImmediate()
        {
            if (_reloadChargeVfx != null)
            {
                _reloadChargeVfx.EndFistChargeImmediate();
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
