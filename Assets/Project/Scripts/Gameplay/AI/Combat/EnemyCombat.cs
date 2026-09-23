using System;
namespace Breachpoint.Gameplay.AI
{
    public sealed class EnemyCombat
    {
        private readonly EnemyContext _context;
        private readonly IEnemyWeapon _weapon;
        private readonly EnemyWorld _world;
        private PerceptionTarget _aimTarget;
        private float _aimStarted;
        private float _nextFire;
        private float _reloadEnds;
        private int _burst;
        public int Ammo { get; private set; }
        public bool IsReloading { get; private set; }
        public bool IsAiming => _aimTarget != null;
        public event Action Fired;
        public event Action ReloadStarted;
        public EnemyCombat(EnemyContext context, IEnemyWeapon weapon, EnemyWorld world)
        { _context = context; _weapon = weapon; _world = world; Reset(); }
        public void Reset()
        {
            Ammo = _context.Config.Combat.MagazineSize; IsReloading = false;
            _aimTarget = null; _aimStarted = _nextFire = _reloadEnds = 0f; _burst = 0;
        }
        public void Stop() { _aimTarget = null; IsReloading = false; _burst = 0; }
        public void Tick(float now)
        {
            if (IsReloading && now >= _reloadEnds) { Ammo = _context.Config.Combat.MagazineSize; IsReloading = false; }
        }
        public void Attack(PerceptionTarget target, float now)
        {
            EnemyCombatConfig config = _context.Config.Combat;
            if (!_weapon.CanAttack(target)) { _aimTarget = null; return; }
            if (_aimTarget != target) { _aimTarget = target; _aimStarted = now; }
            if (IsReloading || now < _nextFire || now - _aimStarted < config.ReactionTime) return;
            if (_weapon.UsesAmmunition && Ammo <= 0)
            { IsReloading = true; _reloadEnds = now + config.ReloadDuration; ReloadStarted?.Invoke(); return; }
            float spread = _context.Navigation.Velocity.sqrMagnitude > 0.1f ? config.MovingSpreadMultiplier : 1f;
            if (!_weapon.Attack(target, spread)) return;
            if (_weapon.UsesAmmunition) Ammo--;
            _burst++;
            _nextFire = now + (_burst >= config.BurstLength ? Math.Max(config.FireInterval, config.BurstPause) : config.FireInterval);
            if (_burst >= config.BurstLength) _burst = 0;
            Fired?.Invoke();
            _world.Emit(new NoiseStimulus(_context.Actor.transform.position, _weapon.UsesAmmunition ? 35f : 5f,
                _context.Config.Faction, now));
        }
    }
}
