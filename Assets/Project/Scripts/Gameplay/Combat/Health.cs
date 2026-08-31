using System;
using UnityEngine;
using UnityEngine.Events;

namespace Breachpoint.Gameplay.Combat
{
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField, Min(1f)]
        private float _maximumHealth = 100f;

        [SerializeField]
        private bool _destroyOnDeath;

        [SerializeField]
        private bool _logDamage;

        [Header("Events")]
        [SerializeField]
        private UnityEvent _damaged;

        [SerializeField]
        private UnityEvent _died;

        [SerializeField, HideInInspector]
        private float _currentHealth;

        public event Action<DamageInfo> DamageReceived;
        public event Action Died;

        public float MaximumHealth => _maximumHealth;
        public float CurrentHealth => _currentHealth;
        public bool IsDead { get; private set; }

        private void Awake()
        {
            ResetHealth();
        }

        public void TakeDamage(DamageInfo damageInfo)
        {
            if (IsDead || damageInfo.Amount <= 0f)
            {
                return;
            }

            _currentHealth = Mathf.Max(
                0f,
                _currentHealth - damageInfo.Amount);

            if (_logDamage)
            {
                Debug.Log(
                    $"{name} received {damageInfo.Amount:0.##} damage. " +
                    $"Health: {_currentHealth:0.##}/{_maximumHealth:0.##}.",
                    this);
            }

            DamageReceived?.Invoke(damageInfo);
            _damaged?.Invoke();

            if (_currentHealth > 0f)
            {
                return;
            }

            Die();
        }

        public void ResetHealth()
        {
            _currentHealth = _maximumHealth;
            IsDead = false;
        }

        private void Die()
        {
            if (IsDead)
            {
                return;
            }

            IsDead = true;

            Died?.Invoke();
            _died?.Invoke();

            if (_destroyOnDeath)
            {
                Destroy(gameObject);
            }
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                _currentHealth = _maximumHealth;
            }
        }
    }
}
