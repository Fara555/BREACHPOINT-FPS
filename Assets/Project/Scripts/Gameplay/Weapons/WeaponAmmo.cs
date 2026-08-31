using System;

namespace Breachpoint.Gameplay.Weapons
{
    public sealed class WeaponAmmo
    {
        public WeaponAmmo(WeaponConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            MagazineSize = config.MagazineSize;
            Magazine = MagazineSize;
            Reserve = config.StartingReserveAmmunition;
        }

        public int MagazineSize { get; }
        public int Magazine { get; private set; }
        public int Reserve { get; private set; }

        public bool HasLoadedRound => Magazine > 0;
        public bool CanReload => Magazine < MagazineSize && Reserve > 0;

        public bool TryConsumeRound()
        {
            if (!HasLoadedRound)
            {
                return false;
            }

            Magazine--;
            return true;
        }

        public void Reload()
        {
            if (!CanReload)
            {
                return;
            }

            int requestedRounds = MagazineSize - Magazine;
            int loadedRounds = Math.Min(requestedRounds, Reserve);

            Magazine += loadedRounds;
            Reserve -= loadedRounds;
        }
    }
}
