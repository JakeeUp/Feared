using Brawler.Core;
using Brawler.Presentation;
using UnityEngine;

namespace Brawler.Player
{
    /// <summary>
    /// Movement dust for the player: a puff when a dash or roll starts, a burst on hard landings and a small puff
    /// under the double jump. Watches the controller's state each frame (no hooks in the states themselves) and asks
    /// the scene <see cref="VfxPlayer"/> to play the clips.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFx : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [Header("References")]
        [SerializeField] private PlayerController _player;
        [SerializeField] private VfxPlayer _vfx;

        [Header("Clips")]
        [SerializeField] private SpriteClip _dashClip;
        [SerializeField] private SpriteClip _landClip;
        [SerializeField] private SpriteClip _doubleJumpClip;

        [Header("Tuning")]
        [Tooltip("Falling faster than this (units/s) when touching down plays the landing burst.")]
        [SerializeField, Min(0f)] private float _landSpeed = 6f;

        [Tooltip("Size of the dust effects.")]
        [SerializeField, Min(0.1f)] private float _scale = 1f;

        // ============ State ============

        private IState<PlayerContext> _lastState;
        private bool _wasGrounded = true;
        private bool _wasDoubleJumping;
        private float _fallSpeed;

        // ============ Lifecycle ============

        private void LateUpdate()
        {
            PlayerContext context = _player != null ? _player.Context : null;
            if (context == null || _vfx == null)
            {
                return;
            }

            Vector3 feet = _player.transform.position;
            IState<PlayerContext> state = _player.CurrentState;
            if (!ReferenceEquals(state, _lastState) && ReferenceEquals(state, context.States.Dash))
            {
                // Puff left behind, mirrored to trail away from the dash direction.
                _vfx.Play(_dashClip, feet - Vector3.right * (0.3f * context.FacingSign), _scale, context.FacingSign < 0);
            }

            bool grounded = context.IsGrounded;
            if (grounded && !_wasGrounded && _fallSpeed >= _landSpeed)
            {
                _vfx.Play(_landClip, feet, _scale);
            }

            if (context.IsDoubleJumping && !_wasDoubleJumping)
            {
                _vfx.Play(_doubleJumpClip, feet, _scale * 0.8f);
            }

            _fallSpeed = grounded ? 0f : Mathf.Max(0f, -context.VerticalVelocity);
            _wasGrounded = grounded;
            _wasDoubleJumping = context.IsDoubleJumping;
            _lastState = state;
        }
    }
}
