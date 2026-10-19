using Brawler.Player;
using UnityEngine;

namespace Brawler.Level
{
    /// <summary>
    /// Base for level volumes that react to the player walking in (checkpoints, titles, encounters). Needs a trigger
    /// collider on the same GameObject; the player's CharacterController raises the trigger messages.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class PlayerTrigger : MonoBehaviour
    {
        /// <summary>Called when the player enters the volume.</summary>
        protected abstract void OnPlayerEnter(PlayerController player);

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out PlayerController player))
            {
                OnPlayerEnter(player);
            }
            else
            {
                OnOtherEnter(other);
            }
        }

        /// <summary>Called for any non-player collider entering (enemies falling into pits).</summary>
        protected virtual void OnOtherEnter(Collider other)
        {
        }

        protected virtual void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }
    }
}
