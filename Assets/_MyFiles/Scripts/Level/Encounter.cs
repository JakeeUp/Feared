using System;
using System.Collections.Generic;
using Brawler.Enemy;
using Brawler.Player;
using UnityEngine;

namespace Brawler.Level
{
    /// <summary>
    /// A fight that starts when the player walks in: optionally closes gates (an arena lock), then spawns its waves one
    /// after another, each once the previous one is dead. Clearing the last wave opens the gates for good. If the player
    /// dies mid-fight, everything resets (enemies removed, gates opened) so the fight replays from the checkpoint.
    /// Ambient groups are simply encounters without gates.
    /// </summary>
    public sealed class Encounter : PlayerTrigger
    {
        // ============ Types ============

        /// <summary>One enemy to spawn.</summary>
        [Serializable]
        public struct SpawnEntry
        {
            public EnemyController Prefab;
            public Transform Point;
        }

        /// <summary>A group of enemies released together.</summary>
        [Serializable]
        public sealed class Wave
        {
            [Tooltip("Seconds after the previous wave is cleared (or the fight starts).")]
            [Min(0f)] public float Delay = 0.6f;

            public SpawnEntry[] Spawns = new SpawnEntry[0];
        }

        private enum Phase
        {
            Waiting,
            Running,
            Cleared,
        }

        // ============ Serialized Fields ============

        [SerializeField] private EnemyCoordinator _coordinator;

        [SerializeField] private Wave[] _waves = new Wave[0];

        [Tooltip("Closed while the fight runs (arena lock). Leave empty for an ambient group.")]
        [SerializeField] private GameObject[] _gates = new GameObject[0];

        [Tooltip("Optional toast when the fight starts and ends.")]
        [SerializeField] private string _startToast = string.Empty;

        [SerializeField] private string _clearToast = string.Empty;

        // ============ Runtime ============

        private readonly List<EnemyController> _alive = new List<EnemyController>();
        private Action<EnemyController> _onDespawned;
        private Action _onPlayerRespawned;
        private PlayerController _player;
        private Phase _phase;
        private int _waveIndex;
        private float _waveTimer;
        private bool _waveSpawned;

        // ============ Events ============

        /// <summary>Raised once when the last wave dies.</summary>
        public event Action Cleared;

        // ============ Lifecycle ============

        private void Awake()
        {
            _onDespawned = OnEnemyDespawned;
            _onPlayerRespawned = ResetEncounter;
            SetGates(false, instant: true);
        }

        private void OnDestroy()
        {
            if (_player != null)
            {
                _player.Respawned -= _onPlayerRespawned;
            }
        }

        private void Update()
        {
            if (_phase != Phase.Running || _waveSpawned)
            {
                return;
            }

            _waveTimer -= Time.deltaTime;
            if (_waveTimer <= 0f)
            {
                SpawnWave(_waves[_waveIndex]);
            }
        }

        // ============ PlayerTrigger ============

        /// <inheritdoc />
        protected override void OnPlayerEnter(PlayerController player)
        {
            if (_phase != Phase.Waiting || _waves.Length == 0)
            {
                return;
            }

            if (_player != player)
            {
                if (_player != null)
                {
                    _player.Respawned -= _onPlayerRespawned;
                }

                _player = player;
                _player.Respawned += _onPlayerRespawned;
            }

            _phase = Phase.Running;
            SetGates(true);
            BeginWave(0);
            Toast(_startToast);
        }

        // ============ Waves ============

        private void BeginWave(int index)
        {
            _waveIndex = index;
            _waveTimer = _waves[index].Delay;
            _waveSpawned = false;
        }

        private void SpawnWave(Wave wave)
        {
            _waveSpawned = true;
            foreach (SpawnEntry entry in wave.Spawns)
            {
                if (entry.Prefab == null || entry.Point == null)
                {
                    continue;
                }

                EnemyController enemy = Instantiate(entry.Prefab, entry.Point.position, Quaternion.identity);
                enemy.Despawned += _onDespawned;
                enemy.Spawn(entry.Point.position, _coordinator);
                _alive.Add(enemy);
            }

            if (_alive.Count == 0)
            {
                AdvanceWave();
            }
        }

        private void OnEnemyDespawned(EnemyController enemy)
        {
            enemy.Despawned -= _onDespawned;
            _alive.Remove(enemy);
            Destroy(enemy.gameObject);

            if (_phase == Phase.Running && _waveSpawned && _alive.Count == 0)
            {
                AdvanceWave();
            }
        }

        private void AdvanceWave()
        {
            if (_waveIndex + 1 < _waves.Length)
            {
                BeginWave(_waveIndex + 1);
                return;
            }

            _phase = Phase.Cleared;
            SetGates(false);
            Toast(_clearToast);
            Cleared?.Invoke();
        }

        /// <summary>Player died: remove the live enemies, open the gates, and wait to be re-triggered.</summary>
        private void ResetEncounter()
        {
            if (_phase != Phase.Running)
            {
                return;
            }

            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                if (_alive[i] != null)
                {
                    _alive[i].Despawned -= _onDespawned;
                    Destroy(_alive[i].gameObject);
                }
            }

            _alive.Clear();
            _phase = Phase.Waiting;
            SetGates(false);
        }

        // ============ Helpers ============

        private void SetGates(bool closed, bool instant = false)
        {
            foreach (GameObject gate in _gates)
            {
                SealBarrier.Set(gate, closed, instant);
            }
        }

        private static void Toast(string text)
        {
            if (!string.IsNullOrEmpty(text) && LevelBanner.Instance != null)
            {
                LevelBanner.Instance.ShowToast(text);
            }
        }
    }
}
