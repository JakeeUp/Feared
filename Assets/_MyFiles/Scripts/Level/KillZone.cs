using Brawler.Enemy;
using Brawler.Player;
using UnityEngine;

namespace Brawler.Level
{
    /// <summary>
    /// Bottomless pit / hazard volume. The player takes a chunk of damage and is put back at the last checkpoint with
    /// the rest of their health (dying respawns as usual). Enemies that fall in are removed.
    /// </summary>
    public sealed class KillZone : PlayerTrigger
    {
        [Tooltip("Damage for falling in.")]
        [SerializeField, Min(0)] private int _damage = 15;

        /// <inheritdoc />
        protected override void OnPlayerEnter(PlayerController player)
        {
            if (!player.TakeEnvironmentDamage(_damage))
            {
                player.Teleport(player.RespawnPoint);
            }
        }

        /// <inheritdoc />
        protected override void OnOtherEnter(Collider other)
        {
            if (other.TryGetComponent(out EnemyController enemy) && enemy.isActiveAndEnabled)
            {
                enemy.Despawn();
            }
        }
    }
}
