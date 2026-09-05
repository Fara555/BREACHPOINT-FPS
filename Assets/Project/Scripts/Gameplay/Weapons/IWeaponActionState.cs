namespace Breachpoint.Gameplay.Weapons
{
    public interface IWeaponActionState
    {
        bool IsActionRequested { get; }
        bool IsReloading { get; }

        void ReportMotionReadyForAction(bool isReady);
    }
}
