using System;
using Brawler.Enemy;
using Brawler.Player;
using UnityEngine;

namespace Brawler.Level
{
    /// <summary>
    /// The boss room: walking in seals the entrance, shows the title and boss bar and wakes the boss inside the arena
    /// bounds. Dying resets the fight (boss back to full, gates open). Victory opens the exit and shows the end card.
    /// </summary>
    public sealed class BossArena : PlayerTrigger
    {
        // ============ Serialized Fields ============

        [SerializeField] private BossController _boss;

        [SerializeField] private BossHealthBar _bar;

        [SerializeField] private string _bossName = "THE UNDEAD EXECUTIONER";

        [SerializeField] private string _bossTitle = "Keeper of the Altar";

        [Tooltip("Boss movement bounds (world X).")]
        [SerializeField] private float _minX;

        [SerializeField] private float _maxX;

        [Tooltip("Closed during the fight.")]
        [SerializeField] private GameObject[] _gates = new GameObject[0];

        [Tooltip("Opened (deactivated) once the boss is dead.")]
        [SerializeField] private GameObject[] _exitBlockers = new GameObject[0];

        [SerializeField] private string _victoryTitle = "CATHEDRAL CLEANSED";

        [SerializeField] private string _victorySubtitle = "The bells fall silent.";

        // ============ Runtime ============

        private PlayerController _player;
        private Action _onPlayerRespawned;
        private Action _onDefeated;
        private Vector3 _bossStart;
        private bool _fighting;
        private bool _won;

        // ============ Events ============

        /// <summary>Raised when the boss dies.</summary>
        public event Action Won;

        // ============ Lifecycle ============

        private void Awake()
        {
            _onPlayerRespawned = OnPlayerRespawned;
            _onDefeated = OnBossDefeated;
            SetActive(_gates, false, instant: true);
            SetActive(_exitBlockers, true, instant: true);

            if (_boss != null)
            {
                _bossStart = _boss.transform.position;
                _boss.Defeated += _onDefeated;
            }
        }

        private void OnDestroy()
        {
            if (_boss != null)
            {
                _boss.Defeated -= _onDefeated;
            }

            if (_player != null)
            {
                _player.Respawned -= _onPlayerRespawned;
            }
        }

        // ============ PlayerTrigger ============

        /// <inheritdoc />
        protected override void OnPlayerEnter(PlayerController player)
        {
            if (_fighting || _won || _boss == null)
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

            _fighting = true;
            SetActive(_gates, true);
            _boss.Activate(player.transform, _minX, _maxX);
            if (_bar != null)
            {
                _bar.Show(_boss.Health, _bossName);
            }

            if (LevelBanner.Instance != null)
            {
                LevelBanner.Instance.ShowTitle(_bossName, _bossTitle, 2.5f);
            }
        }

        // ============ Outcomes ============

        private void OnPlayerRespawned()
        {
            if (!_fighting)
            {
                return;
            }

            _fighting = false;
            _boss.ResetBoss(_bossStart);
            SetActive(_gates, false);
            if (_bar != null)
            {
                _bar.Hide();
            }
        }

        private void OnBossDefeated()
        {
            _fighting = false;
            _won = true;
            SetActive(_gates, false);
            SetActive(_exitBlockers, false);
            if (_bar != null)
            {
                _bar.Hide();
            }

            if (LevelBanner.Instance != null)
            {
                LevelBanner.Instance.ShowTitle(_victoryTitle, _victorySubtitle, 6f);
            }

            Won?.Invoke();
        }

        // ============ Helpers ============

        private static void SetActive(GameObject[] objects, bool active, bool instant = false)
        {
            foreach (GameObject go in objects)
            {
                SealBarrier.Set(go, active, instant);
            }
        }

        // ============ Debug ============

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Vector3 p = transform.position;
            Gizmos.DrawLine(new Vector3(_minX, p.y, p.z), new Vector3(_maxX, p.y, p.z));
        }
    }
}
