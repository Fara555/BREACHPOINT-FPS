using System;

namespace Breachpoint.Gameplay.Weapons.Effects
{
    public interface IWeaponReloadWindowSource
    {
        event Action<float> ReloadWindowStarted;
        event Action ReloadWindowEnded;
    }
}
