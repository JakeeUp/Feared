using Brawler.Combat;
using Brawler.Core;
using Brawler.Presentation;
using UnityEngine;

namespace Brawler.Enemy
{
    /// <summary>
    /// Picks the boss's sprite each frame from <see cref="BossController.State"/>: the swipe follows its frame data, the
    /// dash plays wind-up / charge / recover slices of one sheet, and every new move flashes a red telegraph.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossAnimationDriver : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [SerializeField] private BossController _boss;

        [SerializeField] private SpriteAnimator _animator;

        [SerializeField] private BillboardSprite _billboard;

        [SerializeField] private HitFlash _flash;

        [Header("Clips")]
        [SerializeField] private SpriteClip _idle;

        [SerializeField] private SpriteClip _swipe;

        [Tooltip("Swipe clip frame where the scythe connects.")]
        [SerializeField, Min(0)] private int _swipeHitFrame = 2;

        [Tooltip("Shadow dash sheet: the first DashWindupFrames are the wind-up, then the charge loop, the last two recover.")]
        [SerializeField] private SpriteClip _dash;

        [SerializeField, Min(1)] private int _dashWindupFrames = 5;

        [SerializeField] private SpriteClip _summon;

        [SerializeField] private SpriteClip _death;

        [Header("Feedback")]
        [SerializeField] private Color _telegraphColor = new Color(1f, 0.15f, 0.1f, 1f);

        [Tooltip("Charge loop speed (frames/s).")]
        [SerializeField, Min(1f)] private float _chargeFps = 16f;

        // ============ Runtime ============

        private int _telegraphedMove;

        // ============ Lifecycle ============

        private void LateUpdate()
        {
            if (_boss == null || _animator == null)
            {
                return;
            }

            float t = _boss.StateTime;
            switch (_boss.State)
            {
                case BossController.BossState.Intro:
                    // Rises out of the dark: the idle loop fading in.
                    Show(_idle, Mathf.FloorToInt(t * 8f));
                    SetAlpha(Mathf.Clamp01(t / Mathf.Max(0.01f, _boss.IntroSeconds)));
                    break;
                case BossController.BossState.Swipe:
                    AttackData swipe = _boss.SwipeAttack;
                    if (swipe != null && _swipe != null)
                    {
                        _animator.ShowFrame(_swipe, swipe.Frames.GetAnimationFrame(_boss.AttackFrame, _swipe.FrameCount, _swipeHitFrame));
                    }

                    break;
                case BossController.BossState.DashWindup:
                    ShowSlice(_dash, 0, _dashWindupFrames, t / Mathf.Max(0.01f, _boss.DashWindup));
                    break;
                case BossController.BossState.Dash:
                    if (_dash != null)
                    {
                        int loop = Mathf.Max(1, _dash.FrameCount - _dashWindupFrames - 2);
                        _animator.ShowFrame(_dash, _dashWindupFrames + Mathf.FloorToInt(t * _chargeFps) % loop);
                    }

                    break;
                case BossController.BossState.DashRecover:
                    if (_dash != null)
                    {
                        _animator.ShowFrame(_dash, Mathf.Max(0, _dash.FrameCount - 2 + Mathf.Min(1, Mathf.FloorToInt(t * 4f))));
                    }

                    break;
                case BossController.BossState.Summon:
                    ShowSlice(_summon, 0, _summon != null ? _summon.FrameCount : 1, t / Mathf.Max(0.01f, _boss.SummonCast));
                    break;
                case BossController.BossState.Dead:
                    if (_death != null)
                    {
                        _animator.ShowFrame(_death, Mathf.Min(_death.FrameCount - 1, Mathf.FloorToInt(t * 10f)));
                    }

                    SetAlpha(Mathf.Clamp01(2.5f - t));
                    break;
                case BossController.BossState.Dormant:
                    SetAlpha(0f);
                    break;
                default:
                    _animator.Play(_idle);
                    SetAlpha(1f);
                    break;
            }

            if (_billboard != null)
            {
                _billboard.Mirror = false;
            }

            Flash();
        }

        // ============ Helpers ============

        private void Flash()
        {
            if (_flash == null)
            {
                return;
            }

            // Hits are flashed by HitFlash itself (HitEvent); this is only the move telegraph.
            if (_boss.MoveCount != _telegraphedMove)
            {
                _telegraphedMove = _boss.MoveCount;
                _flash.Flash(_telegraphColor, 0.35f);
            }
        }

        private void Show(SpriteClip clip, int frame)
        {
            if (clip != null && clip.FrameCount > 0)
            {
                _animator.ShowFrame(clip, frame % clip.FrameCount);
            }
        }

        /// <summary>Shows frames [start, end) of <paramref name="clip"/> spread over progress 0..1.</summary>
        private void ShowSlice(SpriteClip clip, int start, int end, float progress)
        {
            if (clip == null || clip.FrameCount == 0)
            {
                return;
            }

            end = Mathf.Min(end, clip.FrameCount);
            int frame = start + Mathf.Min(end - start - 1, Mathf.FloorToInt(Mathf.Clamp01(progress) * (end - start)));
            _animator.ShowFrame(clip, Mathf.Max(0, frame));
        }

        private void SetAlpha(float alpha)
        {
            SpriteRenderer sr = _animator.Renderer;
            if (sr == null)
            {
                return;
            }

            Color c = sr.color;
            if (!Mathf.Approximately(c.a, alpha))
            {
                c.a = alpha;
                sr.color = c;
            }
        }
    }
}
