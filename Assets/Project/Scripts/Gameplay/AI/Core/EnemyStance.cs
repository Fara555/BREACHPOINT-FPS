using UnityEngine;
using UnityEngine.AI;

namespace Breachpoint.Gameplay.AI
{
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyActor))]
    public sealed class EnemyStance : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float _crouchHeight = 1.2f;
        [SerializeField, Min(0.1f)] private float _crouchEyeHeight = 1.05f;
        private CapsuleCollider _body;
        private NavMeshAgent _agent;
        private Transform _eyes;
        private float _standingHeight;
        private Vector3 _standingCenter;
        private Vector3 _standingEyes;
        private float _standingAgentHeight;
        private bool _initialized;
        public bool IsCrouching { get; private set; }

        private void Awake() => Initialize();
        private void Initialize()
        {
            if (_initialized) return;
            _body = GetComponent<CapsuleCollider>();
            _agent = GetComponent<NavMeshAgent>();
            _eyes = GetComponent<EnemyActor>().Eyes;
            if (_body == null || _agent == null || _eyes == null)
            {
                Debug.LogError($"{name}: EnemyStance requires body capsule, NavMeshAgent and Eyes.", this);
                enabled = false; return;
            }
            _standingHeight = _body.height; _standingCenter = _body.center;
            _standingEyes = _eyes.localPosition; _standingAgentHeight = _agent.height;
            _initialized = true;
        }
        public void SetCrouching(bool crouching)
        {
            Initialize();
            if (!_initialized) return;
            IsCrouching = crouching;
            _body.height = crouching ? _crouchHeight : _standingHeight;
            _body.center = crouching ? new Vector3(_standingCenter.x, _crouchHeight * 0.5f, _standingCenter.z) : _standingCenter;
            _agent.height = crouching ? _crouchHeight : _standingAgentHeight;
            _eyes.localPosition = crouching ? new Vector3(_standingEyes.x, _crouchEyeHeight, _standingEyes.z) : _standingEyes;
        }
        private void OnDisable() => SetCrouching(false);
    }
}
