using Breachpoint.Gameplay.Combat;
using Breachpoint.Gameplay.Weapons;
using Breachpoint.Gameplay.Weapons.Effects;
using UnityEngine;

namespace Breachpoint.Gameplay.AI
{
    public sealed class EnemyVfxPresenter : MonoBehaviour
    {
        [SerializeField] private WeaponMuzzleFlash _muzzleFlashEffect;
        [SerializeField] private WeaponProjectileTracerPool _tracerPool;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _shotClip;
        [SerializeField] private AudioClip _deathClip;

        private IEnemyWeapon _weapon;
        private Health _health;
        private EnemyBrain _brain;

        private void Awake()
        {
            _weapon = GetComponent<IEnemyWeapon>();
            _health = GetComponent<Health>();
            _brain = GetComponent<EnemyBrain>();
        }

        private void OnEnable()
        {
            if (_weapon != null) _weapon.Attacked += Attack;
            if (_health != null) _health.Died += Die;
            if (_brain != null) _brain.ResetCompleted += Clear;
            Clear();
        }

        private void OnDisable()
        {
            if (_weapon != null) _weapon.Attacked -= Attack;
            if (_health != null) _health.Died -= Die;
            if (_brain != null) _brain.ResetCompleted -= Clear;
            Clear();
        }

        private void Attack(WeaponShotResult shot)
        {
            if (_muzzleFlashEffect != null) _muzzleFlashEffect.Play();
            if (_tracerPool != null) _tracerPool.Play(shot.Origin, shot.EndPoint);
            if (_audioSource != null && _shotClip != null) _audioSource.PlayOneShot(_shotClip);
        }

        private void Die()
        {
            Clear();
            if (_audioSource != null && _deathClip != null) _audioSource.PlayOneShot(_deathClip);
        }

        private void Clear()
        {
            if (_muzzleFlashEffect != null) _muzzleFlashEffect.Clear();
            if (_tracerPool != null) _tracerPool.Clear();
            if (_audioSource != null) _audioSource.Stop();
        }
    }
}
