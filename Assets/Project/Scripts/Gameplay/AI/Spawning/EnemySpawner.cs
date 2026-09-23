using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using VContainer.Unity;
namespace Breachpoint.Gameplay.AI
{
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private LifetimeScope _parentScope;
        [SerializeField] private EnemyLifetimeScope[] _prefabs = new EnemyLifetimeScope[0];
        [SerializeField] private Transform[] _spawnPoints = new Transform[0];
        [SerializeField] private PatrolRoute _route;
        [SerializeField, Min(1)] private int _maximumAlive = 10;
        [SerializeField, Min(1)] private int _totalToSpawn = 20;
        [SerializeField, Min(0.1f)] private float _spawnInterval = 2f;
        [SerializeField, Min(0f)] private float _corpseDuration = 4f;
        private sealed class Slot
        {
            public EnemyLifetimeScope Scope;
            public EnemyActor Actor;
            public int PrefabIndex;
            public float DiedAt = -1f;
        }
        private readonly List<Slot> _slots = new List<Slot>();
        private float _nextSpawn;
        public int SpawnedCount { get; private set; }
        public int AliveCount { get; private set; }
        private void Update()
        {
            AliveCount = 0;
            foreach (Slot slot in _slots)
            {
                if (slot.Scope == null || !slot.Scope.gameObject.activeSelf) continue;
                if (!slot.Actor.Health.IsDead) { AliveCount++; continue; }
                if (slot.DiedAt < 0) slot.DiedAt = Time.time;
                if (Time.time - slot.DiedAt >= _corpseDuration) slot.Scope.gameObject.SetActive(false);
            }
            if (_parentScope == null || _prefabs.Length == 0 || _spawnPoints.Length == 0 ||
                SpawnedCount >= _totalToSpawn || AliveCount >= _maximumAlive || Time.time < _nextSpawn) return;
            _nextSpawn = Time.time + _spawnInterval;
            int index = SpawnedCount % _prefabs.Length;
            Transform point = _spawnPoints[SpawnedCount % _spawnPoints.Length];
            if (point == null || _prefabs[index] == null || !NavMesh.SamplePosition(point.position, out NavMeshHit hit, 2f, NavMesh.AllAreas)) return;
            Slot available = null;
            foreach (Slot slot in _slots)
                if (slot.Scope != null && !slot.Scope.gameObject.activeSelf && slot.PrefabIndex == index) { available = slot; break; }
            if (available == null)
            {
                // Also cap retained corpses; mass deaths cannot grow the pool without bound.
                if (_slots.Count >= _maximumAlive + _prefabs.Length)
                {
                    Slot retired = null;
                    foreach (Slot slot in _slots)
                        if (slot.Scope == null || !slot.Scope.gameObject.activeSelf) { retired = slot; break; }
                    if (retired == null) return;
                    if (retired.Scope != null) Destroy(retired.Scope.gameObject);
                    _slots.Remove(retired);
                }
                EnemyLifetimeScope scope;
                using (LifetimeScope.EnqueueParent(_parentScope)) scope = Instantiate(_prefabs[index], hit.position, point.rotation, transform);
                available = new Slot { Scope = scope, Actor = scope.GetComponent<EnemyActor>(), PrefabIndex = index };
                _slots.Add(available);
            }
            else available.Scope.transform.SetPositionAndRotation(hit.position, point.rotation);
            available.DiedAt = -1f; available.Actor.Route = _route;
            available.Scope.gameObject.SetActive(true); SpawnedCount++; AliveCount++;
        }
        private void OnDisable()
        { foreach (Slot slot in _slots) if (slot.Scope != null) slot.Scope.gameObject.SetActive(false); AliveCount = 0; }
        private void OnValidate()
        {
            _maximumAlive = Mathf.Max(1, _maximumAlive); _totalToSpawn = Mathf.Max(1, _totalToSpawn);
            _spawnInterval = Mathf.Max(0.1f, _spawnInterval); _corpseDuration = Mathf.Max(0f, _corpseDuration);
        }
    }
}
