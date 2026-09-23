using Breachpoint.Gameplay.Combat;
using Breachpoint.Gameplay.Weapons;
using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    public sealed class EnemyVfxPresenter : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _muzzleFlash;
        [SerializeField] private ParticleSystem _hitEffect;
        [SerializeField] private LineRenderer _tracer;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _shotClip;
        [SerializeField] private AudioClip _deathClip;
        private IEnemyWeapon _weapon;
        private Health _health;
        private float _tracerUntil;
        private void Awake() { _weapon = GetComponent<IEnemyWeapon>(); _health = GetComponent<Health>(); }
        private void OnEnable()
        {
            if (_weapon != null) _weapon.Attacked += Attack;
            if (_health != null) { _health.DamageReceived += Hit; _health.Died += Die; }
            Clear();
        }
        private void OnDisable()
        {
            if (_weapon != null) _weapon.Attacked -= Attack;
            if (_health != null) { _health.DamageReceived -= Hit; _health.Died -= Die; }
            Clear();
        }
        private void Attack(WeaponShotResult shot)
        {
            if (_muzzleFlash != null) _muzzleFlash.Play();
            if (_tracer != null) { _tracer.positionCount = 2; _tracer.SetPosition(0, shot.Origin); _tracer.SetPosition(1, shot.EndPoint); _tracer.enabled = true; _tracerUntil = Time.time + 0.06f; }
            if (_audioSource != null && _shotClip != null) _audioSource.PlayOneShot(_shotClip);
        }
        private void Hit(DamageInfo damage)
        {
            if (_hitEffect == null) return;
            _hitEffect.transform.position = damage.Point; _hitEffect.Play();
        }
        private void Die()
        {
            Clear();
            if (_audioSource != null && _deathClip != null) _audioSource.PlayOneShot(_deathClip);
        }
        private void Update() { if (_tracer != null && Time.time >= _tracerUntil) _tracer.enabled = false; }
        private void Clear()
        {
            if (_tracer != null) _tracer.enabled = false;
            if (_muzzleFlash != null) _muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_hitEffect != null) _hitEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_audioSource != null) _audioSource.Stop();
        }
    }
}
