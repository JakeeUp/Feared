using Brawler.Combat;
using UnityEngine;

namespace Brawler.Player
{
    /// <summary>
    /// Chain presentation. Draws the chain from the player's hand to the hook (flying out, taut while swinging, reeling
    /// the player in during a zip), and previews what the chain button would grab right now: a ring brightens, an enemy
    /// gets the reticle. Reads the player state only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrappleRope : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [SerializeField] private PlayerController _player;

        [Tooltip("Two-point line from the hand to the hook. Disabled while the chain is away.")]
        [SerializeField] private LineRenderer _line;

        [Tooltip("Optional hook sprite placed at the chain's end.")]
        [SerializeField] private Transform _hook;

        [Tooltip("Optional marker shown on the enemy the chain would hook.")]
        [SerializeField] private Transform _reticle;

        // ============ Runtime ============

        private GrappleAnchor _highlighted;

        // ============ Lifecycle ============

        private void LateUpdate()
        {
            if (_player == null || _player.Context == null)
            {
                return;
            }

            PlayerContext context = _player.Context;
            bool hasEnd = TryGetChainEnd(context, out Vector3 end);

            UpdatePreview(context, hasEnd);

            if (_line != null)
            {
                _line.enabled = hasEnd;
            }

            if (_hook != null)
            {
                _hook.gameObject.SetActive(hasEnd);
            }

            if (!hasEnd)
            {
                return;
            }

            Vector3 from = context.RopeOrigin;
            if (_line != null)
            {
                _line.SetPosition(0, from);
                _line.SetPosition(1, end);
            }

            if (_hook != null)
            {
                _hook.position = end;
            }
        }

        private void OnDisable()
        {
            SetHighlight(null);
            if (_reticle != null)
            {
                _reticle.gameObject.SetActive(false);
            }
        }

        // ============ Helpers ============

        /// <summary>Where the chain's far end is: the flying hook, the ring being swung from, or the zip target.</summary>
        private bool TryGetChainEnd(PlayerContext context, out Vector3 end)
        {
            PlayerStates states = context.States;
            if (context.Chain.IsFlying)
            {
                end = context.Chain.Tip;
                return true;
            }

            if (ReferenceEquals(_player.CurrentState, states.Grapple) && states.Grapple.Anchor != null)
            {
                end = states.Grapple.Anchor.Position;
                return true;
            }

            if (ReferenceEquals(_player.CurrentState, states.Zip) && states.Zip.Target != null)
            {
                end = states.Zip.Target.ChainPoint;
                return true;
            }

            end = default;
            return false;
        }

        /// <summary>Shows what the button would grab. While the chain is out, the swing owns the ring highlight.</summary>
        private void UpdatePreview(PlayerContext context, bool chainOut)
        {
            GrappleAnchor anchor = null;
            IChainTarget enemy = null;
            if (!chainOut)
            {
                ChainTargeting.FindBest(context, out anchor, out enemy);
            }
            else if (_highlighted != null && _highlighted != context.States.Grapple.Anchor)
            {
                _highlighted.IsTargeted = false;
                _highlighted = null;
            }

            if (!chainOut)
            {
                SetHighlight(anchor);
            }
            else
            {
                _highlighted = null;
            }

            if (_reticle != null)
            {
                _reticle.gameObject.SetActive(enemy != null);
                if (enemy != null)
                {
                    _reticle.position = enemy.ChainPoint;
                }
            }
        }

        private void SetHighlight(GrappleAnchor anchor)
        {
            if (anchor == _highlighted)
            {
                return;
            }

            if (_highlighted != null)
            {
                _highlighted.IsTargeted = false;
            }

            _highlighted = anchor;
            if (_highlighted != null)
            {
                _highlighted.IsTargeted = true;
            }
        }
    }
}
