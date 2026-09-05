namespace Breachpoint.Gameplay.Weapons
{
    public interface IWeaponAimState
    {
        bool IsAiming { get; }
        bool IsReloading { get; }
    }
}
