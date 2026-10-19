using Brawler.Combat;
using Brawler.Core;
using Brawler.Presentation;
using UnityEngine;

namespace Brawler.Enemy
{
    /// <summary>
    /// Maps the enemy's state to a <see cref="SpriteClip"/> each frame. The attack follows its frame data like the
    /// player's (wind-up held through the long startup, swing on the active frames) and flashes red once at the start
    /// of every wind-up as the telegraph. Knockdowns tumble, lie, then play the get-up; the dead body blinks before it
    /// despawns.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAnimationDriver : MonoBehaviour
    {
        // ============ Constants ============

        /// <summary>Blink rate of a dead body in its last moments (toggles per second).</summary>
        private const float DeathBlinkRate = 14f;

        /// <summary>Linger progress after which the dead body starts blinking.</summary>
        private const float DeathBlinkStart = 0.5f;

        // ============ Serialized Fields ============

        [Header("References")]
        [SerializeField] private EnemyController _enemy;
        [SerializeField] private SpriteAnimator _animator;
        [SerializeField] private BillboardSprite _billboard;

        [Tooltip("Used for the red attack telegraph.")]
        [SerializeField] private HitFlash _flash;

        [Header("Clips")]
        [SerializeField] private SpriteClip _idle;
        [SerializeField] private SpriteClip _run;
        [SerializeField] private SpriteClip _attack;

        [Tooltip("Clip frame where the blow lands / the shot leaves. 0 = default mapping (wind-up pose, hit pose, recovery).")]
        [SerializeField, Min(0)] private int _attackHitFrame;

        [Tooltip("Rising part of a jump.")]
        [SerializeField] private SpriteClip _jump;

        [Tooltip("Falling (after the jump's apex, or off a ledge).")]
        [SerializeField] private SpriteClip _fall;

        [Tooltip("Flinch pose during hitstun.")]
        [SerializeField] private SpriteClip _hurt;

        [Tooltip("Spinning in the air after a launch.")]
        [SerializeField] private SpriteClip _tumble;

        [Tooltip("Lying on the ground (also the dead body).")]
        [SerializeField] private SpriteClip _down;

        [Tooltip("Getting back up; spread over the get-up time.")]
        [SerializeField] private SpriteClip _getUp;

        [Tooltip("Optional one-shot death animation. Without it the tumble/down clips play.")]
        [SerializeField] private SpriteClip _death;

        [Header("Telegraph")]
        [SerializeField] private Color _telegraphColor = new Color(1f, 0.15f, 0.1f, 1f);

        [Tooltip("Fraction of the attack's startup the red flash lasts.")]
        [SerializeField, Range(0f, 1f)] private float _telegraphPortion = 0.7f;

        // ============ State ============

        private int _telegraphedActivation = -1;
        private int _shownFlinch = -1;

        // ============ Lifecycle ============

        private void LateUpdate()
        {
            if (_enemy == null || _animator == null || _enemy.Context == null)
            {
                return;
            }

            EnemyContext context = _enemy.Context;
            EnemyStates states = context.States;
            IState<EnemyContext> state = _enemy.CurrentState;
            bool visible = true;

            if (ReferenceEquals(state, states.Attack))
            {
                ShowAttack(states.Attack);
            }
            else if (ReferenceEquals(state, states.Knockdown))
            {
                ShowKnockdown(states.Knockdown);
            }
            else if (ReferenceEquals(state, states.Dead))
            {
                Play(_death != null ? _death : states.Dead.HasLanded ? Or(_down, _hurt) : Or(_tumble, _hurt));
                float linger = states.Dead.LingerProgress;
                visible = linger < DeathBlinkStart || Mathf.Repeat(Time.time * DeathBlinkRate, 2f) < 1f;
            }
            else if (ReferenceEquals(state, states.Air))
            {
                Play(context.VerticalVelocity > 0.5f ? Or(_jump, _idle) : Or(_fall, _idle));
            }
            else if (ReferenceEquals(state, states.Hitstun))
            {
                // Every hit restarts the flinch from its first frame, so each one reads on contact.
                bool newHit = states.Hitstun.EntryCount != _shownFlinch;
                _shownFlinch = states.Hitstun.EntryCount;
                Play(_hurt != null ? _hurt : _idle, newHit);
            }
            else if (ReferenceEquals(state, states.Chase) && context.Velocity.sqrMagnitude > 0.05f)
            {
                Play(_run);
            }
            else
            {
                Play(_idle);
            }

            if (_animator.Renderer != null && _animator.Renderer.enabled != visible)
            {
                _animator.Renderer.enabled = visible;
            }
        }

        // ============ Helpers ============

        /// <summary>The clip, or the fallback when the art has no such animation.</summary>
        private static SpriteClip Or(SpriteClip clip, SpriteClip fallback)
        {
            return clip != null ? clip : fallback;
        }

        private void Play(SpriteClip clip, bool restart = false)
        {
            _animator.Play(clip, restart);
            if (_billboard != null && clip != null)
            {
                _billboard.Mirror = clip.Mirrored;
            }
        }

        private void ShowAttack(EnemyAttackState attackState)
        {
            AttackData attack = attackState.CurrentAttack;
            if (attack == null || _attack == null)
            {
                return;
            }

            AttackFrames frames = attack.Frames;
            int frame = attackState.CurrentFrame;
            _animator.ShowFrame(_attack, frames.GetAnimationFrame(frame, _attack.FrameCount, _attackHitFrame));
            if (_billboard != null)
            {
                _billboard.Mirror = _attack.Mirrored;
            }

            // One red flash per swing, as the wind-up begins.
            if (attackState.ActivationCount != _telegraphedActivation && _flash != null)
            {
                _telegraphedActivation = attackState.ActivationCount;
                _flash.Flash(_telegraphColor, FrameTime.ToSeconds(frames.Startup) * _telegraphPortion);
            }
        }

        private void ShowKnockdown(EnemyKnockdownState knockdown)
        {
            switch (knockdown.Phase)
            {
                case EnemyKnockdownState.KnockdownPhase.Airborne:
                    Play(Or(_tumble, _hurt));
                    break;
                case EnemyKnockdownState.KnockdownPhase.Down:
                    Play(Or(_down, _hurt));
                    break;
                default:
                    if (_getUp != null)
                    {
                        int frame = Mathf.Min(_getUp.FrameCount - 1, Mathf.FloorToInt(knockdown.PhaseProgress * _getUp.FrameCount));
                        _animator.ShowFrame(_getUp, frame);
                    }
                    else
                    {
                        Play(_idle);
                    }

                    break;
            }
        }
    }
}
