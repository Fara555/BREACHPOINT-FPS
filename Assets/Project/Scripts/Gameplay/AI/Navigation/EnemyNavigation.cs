using UnityEngine;
using UnityEngine.AI;

namespace Breachpoint.Gameplay.AI
{
    public enum NavigationResult { None, Moving, Arrived, Unavailable, Partial, Invalid, Stuck }
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyNavigation : MonoBehaviour
    {
        private NavMeshAgent _agent;
        private EnemyMovementConfig _config;
        private NavMeshPath _path;
        private float _nextRepath;
        private float _lastProgressTime;
        private Vector3 _lastProgressPosition;
        private bool _requested;
        public NavigationResult Result { get; private set; }
        public Vector3 Velocity => Ready ? _agent.velocity : Vector3.zero;
        public Vector3 Destination { get; private set; }
        public bool Ready => _agent != null && _agent.enabled && _agent.isOnNavMesh;
        public bool Failed => Result == NavigationResult.Unavailable || Result == NavigationResult.Invalid || Result == NavigationResult.Partial || Result == NavigationResult.Stuck;
        public bool Arrived => Ready && _requested && !_agent.pathPending &&
            _agent.remainingDistance <= _agent.stoppingDistance + 0.15f;
        public void Initialize(EnemyMovementConfig config)
        {
            _config = config; _agent = GetComponent<NavMeshAgent>();
            _path = new NavMeshPath();
            _agent.acceleration = config.Acceleration; _agent.angularSpeed = config.TurnSpeed;
            _agent.stoppingDistance = config.StoppingDistance;
        }
        public bool ResetAt(Vector3 position)
        {
            _nextRepath = 0f; _requested = false; Result = NavigationResult.None;
            if (_agent == null || !NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, _agent.areaMask))
            { Result = NavigationResult.Unavailable; return false; }
            bool success = _agent.Warp(hit.position);
            if (Ready) { _agent.ResetPath(); _agent.velocity = Vector3.zero; _agent.isStopped = true; }
            return success;
        }
        public void MoveTo(Vector3 point, bool run, float now)
        {
            if (!Ready) { Result = NavigationResult.Unavailable; return; }
            _agent.speed = run ? _config.RunSpeed : _config.WalkSpeed;
            if (now < _nextRepath) return;
            _nextRepath = now + _config.RepathInterval;
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 1.5f, _agent.areaMask) ||
                !_agent.CalculatePath(hit.position, _path) || _path.status != NavMeshPathStatus.PathComplete)
            {
                Stop(); _nextRepath = now + _config.RepathInterval;
                Result = _path.status == NavMeshPathStatus.PathPartial ? NavigationResult.Partial : NavigationResult.Invalid; return;
            }
            if (!_requested || (Destination - hit.position).sqrMagnitude > 1f)
            { _lastProgressTime = now; _lastProgressPosition = transform.position; }
            Destination = hit.position; _requested = true;
            _agent.isStopped = false; _agent.SetPath(_path); Result = NavigationResult.Moving;
            if ((transform.position - _lastProgressPosition).sqrMagnitude > 0.04f)
            { _lastProgressTime = now; _lastProgressPosition = transform.position; }
            else if (!Arrived && now - _lastProgressTime > _config.StuckTimeout)
            { Stop(); _nextRepath = now + _config.RepathInterval; Result = NavigationResult.Stuck; }
            if (Arrived) Result = NavigationResult.Arrived;
        }
        public void Stop()
        {
            if (Ready) { _agent.isStopped = true; _agent.ResetPath(); _agent.velocity = Vector3.zero; }
            _requested = false; _nextRepath = 0f;
        }
        public void Face(Vector3 point, float deltaTime)
        {
            Vector3 direction = point - transform.position; direction.y = 0;
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), _config.TurnSpeed * deltaTime);
        }
        private void OnDisable() => Stop();
    }
}
