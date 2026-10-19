using System;
using UnityEngine;

namespace Brawler.Enemy
{
    /// <summary>
    /// Keeps up to <c>MaxAlive</c> enemies in play for testing: instantiates the pool once, spawns at the spawn points
    /// in turn, and respawns a defeated enemy after <c>RespawnDelay</c>. Injects the <see cref="EnemyCoordinator"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySpawner : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [Tooltip("Enemy prefab (root has an EnemyController).")]
        [SerializeField] private EnemyController _prefab;

        [Tooltip("Shared brain handed to every spawned enemy.")]
        [SerializeField] private EnemyCoordinator _coordinator;

        [Tooltip("Where enemies appear, used in turn.")]
        [SerializeField] private Transform[] _spawnPoints = new Transform[0];

        [Tooltip("Enemies alive at once.")]
        [SerializeField, Min(0)] private int _maxAlive = 3;

        [Tooltip("Seconds before a defeated enemy is replaced.")]
        [SerializeField, Min(0f)] private float _respawnDelay = 2.5f;

        // ============ State ============

        private EnemyController[] _enemies;
        private float[] _respawnAt; // per slot; < 0 = alive or not scheduled
        private int _nextPoint;
        private Action<EnemyController> _onDespawned;

        // ============ Lifecycle ============

        private void Awake()
        {
            _onDespawned = OnDespawned;
            _enemies = new EnemyController[_prefab != null ? _maxAlive : 0];
            _respawnAt = new float[_enemies.Length];
            for (int i = 0; i < _enemies.Length; i++)
            {
                _enemies[i] = Instantiate(_prefab, transform);
                _enemies[i].name = $"{_prefab.name}_{i}";
                _enemies[i].Despawned += _onDespawned;
                _enemies[i].gameObject.SetActive(false);
                _respawnAt[i] = 0f; // spawn on the first Update
            }
        }

        private void OnDestroy()
        {
            if (_enemies == null)
            {
                return;
            }

            foreach (EnemyController enemy in _enemies)
            {
                if (enemy != null)
                {
                    enemy.Despawned -= _onDespawned;
                }
            }
        }

        private void Update()
        {
            for (int i = 0; i < _enemies.Length; i++)
            {
                if (_respawnAt[i] >= 0f && Time.time >= _respawnAt[i])
                {
                    _respawnAt[i] = -1f;
                    _enemies[i].Spawn(NextSpawnPosition(), _coordinator);
                }
            }
        }

        // ============ Helpers ============

        private void OnDespawned(EnemyController enemy)
        {
            int index = Array.IndexOf(_enemies, enemy);
            if (index >= 0)
            {
                _respawnAt[index] = Time.time + _respawnDelay;
            }
        }

        private Vector3 NextSpawnPosition()
        {
            if (_spawnPoints.Length == 0)
            {
                return transform.position;
            }

            Transform point = _spawnPoints[_nextPoint % _spawnPoints.Length];
            _nextPoint++;
            return point != null ? point.position : transform.position;
        }
    }
}
