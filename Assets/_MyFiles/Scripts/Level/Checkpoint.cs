using Brawler.Combat;
using Brawler.Player;
using UnityEngine;

namespace Brawler.Level
{
    /// <summary>
    /// Celeste-style checkpoint at the start of a challenge: touching it moves the player's respawn point here and
    /// lights its flame. The first touch also heals the player to full (a breather before the next room).
    /// </summary>
    public sealed class Checkpoint : PlayerTrigger
    {
        // ============ Serialized Fields ============

        [Tooltip("Respawn position. Defaults to this transform.")]
        [SerializeField] private Transform _spawnPoint;

        [Tooltip("Shown once the checkpoint is lit (flame, glow).")]
        [SerializeField] private GameObject _litVisual;

        [Tooltip("Shown until the checkpoint is lit.")]
        [SerializeField] private GameObject _unlitVisual;

        [SerializeField] private string _toast = "Checkpoint";

        // ============ Runtime ============

        private bool _lit;

        // ============ Lifecycle ============

        private void Awake()
        {
            SetLit(false);
        }

        // ============ PlayerTrigger ============

        /// <inheritdoc />
        protected override void OnPlayerEnter(PlayerController player)
        {
            player.RespawnPoint = _spawnPoint != null ? _spawnPoint.position : transform.position;
            if (_lit)
            {
                return;
            }

            SetLit(true);
            if (player.TryGetComponent(out Health health))
            {
                health.ResetToMax();
            }

            if (LevelBanner.Instance != null)
            {
                LevelBanner.Instance.ShowToast(_toast);
            }
        }

        // ============ Helpers ============

        private void SetLit(bool lit)
        {
            _lit = lit;
            if (_litVisual != null)
            {
                _litVisual.SetActive(lit);
            }

            if (_unlitVisual != null)
            {
                _unlitVisual.SetActive(!lit);
            }
        }
    }
}
