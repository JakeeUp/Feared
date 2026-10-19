using Brawler.Combat;
using Brawler.Core;
using Brawler.Presentation;
using UnityEngine;

namespace Brawler.Player
{
    /// <summary>
    /// Maps the player's current state to a <see cref="SpriteClip"/> each frame and plays it on a
    /// <see cref="SpriteAnimator"/>. Attack clips are not clock-driven: their frame is derived from the attack's frame
    /// data (startup -&gt; frame 0 wind-up, active -&gt; frame 1 swing, recovery -&gt; the rest), so the swing is always on
    /// screen exactly while the hitbox is live, whatever the tuning.
    /// </summary>
    /// <remarks>Read-only on gameplay: never writes the context. Lives on the player's Visual child.</remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerAnimationDriver : MonoBehaviour
    {
        // ============ Constants ============

        /// <summary>Vertical speed above which the rising (jump) clip shows instead of the fall clip.</summary>
        private const float RisingThreshold = 0.5f;


        // ============ Serialized Fields ============

        [Header("References")]
        [Tooltip("Player whose state is shown.")]
        [SerializeField] private PlayerController _player;

        [Tooltip("Animator on the character sprite.")]
        [SerializeField] private SpriteAnimator _animator;

        [Tooltip("Billboard on the character sprite (its Mirror flag is set per clip).")]
        [SerializeField] private BillboardSprite _billboard;

        [Header("Locomotion Clips")]
        [SerializeField] private SpriteClip _idle;
        [SerializeField] private SpriteClip _run;
        [SerializeField] private SpriteClip _dash;

        [Tooltip("Ground dodge roll; its frames are spread over the roll's timeline rather than played on a clock.")]
        [SerializeField] private SpriteClip _roll;
        [SerializeField] private SpriteClip _jump;

        [Tooltip("Somersault shown while rising from a double jump.")]
        [SerializeField] private SpriteClip _doubleJump;
        [SerializeField] private SpriteClip _fall;
        [SerializeField] private SpriteClip _wallGrab;
        [SerializeField] private SpriteClip _wallSlide;

        [Tooltip("Shown during hitstun. Falls back to Idle (HitFlash sells the hit) when empty.")]
        [SerializeField] private SpriteClip _hurt;

        [Header("Combat Clips")]
        [Tooltip("One clip per combo link (index = ComboTracker.CurrentIndex). Frame 0 = wind-up, 1 = swing, rest = recovery.")]
        [SerializeField] private SpriteClip[] _attacks = new SpriteClip[0];

        [Tooltip("Parry: frame 0 is the guard stance, frame 1 the deflecting swing shown after a successful parry.")]
        [SerializeField] private SpriteClip _parry;

        [Tooltip("Overhead slash (Attack + up). Falls back to the first combo clip.")]
        [SerializeField] private SpriteClip _upAttack;

        [Tooltip("Pogo slash (Attack + down in the air). Falls back to the first combo clip.")]
        [SerializeField] private SpriteClip _downAttack;

        [Tooltip("Optional swing pose while grappling. Falls back to the jump/fall clips.")]
        [SerializeField] private SpriteClip _grapple;

        [Header("Parry Feedback")]
        [Tooltip("Flashes on a successful parry.")]
        [SerializeField] private HitFlash _flash;

        [Tooltip("Player flash on a successful parry: held through the parry freeze, then a quick fade.")]
        [SerializeField] private Color _parryGlow = new Color(1f, 0.92f, 0.65f, 1f);

        [Tooltip("Fade-out of the parry flash after the freeze, in real seconds.")]
        [SerializeField, Min(0.01f)] private float _parryGlowFade = 0.1f;

        // ============ State ============

        private int _flashedParry;

        // ============ Lifecycle ============

        private void LateUpdate()
        {
            if (_player == null || _animator == null || _player.Context == null)
            {
                return;
            }

            PlayerContext context = _player.Context;
            PlayerStates states = context.States;
            IState<PlayerContext> state = _player.CurrentState;

            SpriteClip clip;
            if (ReferenceEquals(state, states.Attack))
            {
                clip = ShowAttack(context, states.Attack);
            }
            else if (ReferenceEquals(state, states.Dash) && states.Dash.IsRoll && _roll != null)
            {
                clip = _roll;
                int frame = Mathf.Min(_roll.FrameCount - 1, Mathf.FloorToInt(states.Dash.Progress * _roll.FrameCount));
                _animator.ShowFrame(_roll, frame);
            }
            else if (ReferenceEquals(state, states.Parry) && _parry != null)
            {
                clip = _parry;
                _animator.ShowFrame(_parry, states.Parry.Succeeded ? 1 : 0);
            }
            else
            {
                clip = PickClip(context, state);
                _animator.Play(clip);
            }

            if (states.Parry.SuccessCount != _flashedParry)
            {
                _flashedParry = states.Parry.SuccessCount;
                if (_flash != null)
                {
                    _flash.Flash(_parryGlow, _parryGlowFade, FrameTime.ToSeconds(context.Movement.ParryHitStopFrames));
                }
            }

            if (_billboard != null && clip != null)
            {
                _billboard.Mirror = clip.Mirrored;
            }
        }

        // ============ Helpers ============

        private SpriteClip PickClip(PlayerContext context, IState<PlayerContext> state)
        {
            PlayerStates states = context.States;
            if (ReferenceEquals(state, states.Move))
            {
                return _run;
            }

            if (ReferenceEquals(state, states.Dash) || ReferenceEquals(state, states.Zip))
            {
                return _dash;
            }

            if (ReferenceEquals(state, states.Air))
            {
                if (context.IsDoubleJumping && _doubleJump != null)
                {
                    return _doubleJump;
                }

                return context.VerticalVelocity > RisingThreshold ? _jump : _fall;
            }

            if (ReferenceEquals(state, states.Grapple))
            {
                if (_grapple != null)
                {
                    return _grapple;
                }

                return states.Grapple.SwingVelocity.y > RisingThreshold ? _jump : _fall;
            }

            if (ReferenceEquals(state, states.WallSlide))
            {
                return states.WallSlide.IsGrabbing ? _wallGrab : _wallSlide;
            }

            if (ReferenceEquals(state, states.Hitstun) && _hurt != null)
            {
                return _hurt;
            }

            return _idle;
        }

        private SpriteClip ShowAttack(PlayerContext context, AttackState attackState)
        {
            AttackData attack = attackState.CurrentAttack;
            if (attack == null || _attacks.Length == 0)
            {
                return null;
            }

            int index = Mathf.Max(0, context.ComboTracker.CurrentIndex) % _attacks.Length;
            SpriteClip clip = _attacks[index];
            if (ReferenceEquals(attack, context.UpAttack) && _upAttack != null)
            {
                clip = _upAttack;
            }
            else if (ReferenceEquals(attack, context.DownAttack) && _downAttack != null)
            {
                clip = _downAttack;
            }
            if (clip == null)
            {
                return null;
            }

            _animator.ShowFrame(clip, attack.Frames.GetAnimationFrame(attackState.CurrentFrame, clip.FrameCount));
            return clip;
        }
    }
}
