using UnityEngine;

namespace Brawler.Presentation
{
    /// <summary>
    /// Plays <see cref="SpriteClip"/>s on a <see cref="SpriteRenderer"/>. Code-driven (no Animator graph): a driver
    /// calls <see cref="Play"/> each frame with the clip it wants, or <see cref="ShowFrame"/> to pick the frame itself.
    /// Runs on scaled time, so animation freezes with everything else during hitstop.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpriteAnimator : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [Tooltip("Renderer to drive. Defaults to the SpriteRenderer on this GameObject.")]
        [SerializeField] private SpriteRenderer _renderer;

        [Tooltip("Optional clip to start with.")]
        [SerializeField] private SpriteClip _defaultClip;

        // ============ State ============

        private SpriteClip _clip;
        private float _time;
        private bool _external; // frame set by ShowFrame this frame; skip the clock

        // ============ Properties ============

        /// <summary>The clip currently shown.</summary>
        public SpriteClip CurrentClip => _clip;

        /// <summary>True once a non-looping clip has reached its last frame.</summary>
        public bool IsFinished => _clip != null && !_clip.Loop && _time >= _clip.Duration;

        /// <summary>Renderer being driven.</summary>
        public SpriteRenderer Renderer => _renderer;

        // ============ Lifecycle ============

        private void Awake()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }

            if (_defaultClip != null)
            {
                Play(_defaultClip);
            }
        }

        private void Update()
        {
            if (_clip == null || _external)
            {
                _external = false;
                return;
            }

            _time += Time.deltaTime;
            Apply(FrameAt(_time));
        }

        // ============ API ============

        /// <summary>Switches to <paramref name="clip"/> (no-op if already playing it, unless <paramref name="restart"/>).</summary>
        public void Play(SpriteClip clip, bool restart = false)
        {
            if (clip == null || (clip == _clip && !restart))
            {
                return;
            }

            _clip = clip;
            _time = 0f;
            Apply(0);
        }

        /// <summary>Shows a specific frame of <paramref name="clip"/> this frame, bypassing the clock.</summary>
        public void ShowFrame(SpriteClip clip, int index)
        {
            if (clip == null)
            {
                return;
            }

            _clip = clip;
            _external = true;
            Apply(index);
        }

        // ============ Helpers ============

        private int FrameAt(float time)
        {
            int frame = Mathf.FloorToInt(time * _clip.FramesPerSecond);
            return _clip.Loop ? frame % Mathf.Max(1, _clip.FrameCount) : Mathf.Min(frame, _clip.FrameCount - 1);
        }

        private void Apply(int index)
        {
            if (_renderer == null)
            {
                return;
            }

            Sprite sprite = _clip.GetFrame(index);
            if (_renderer.sprite != sprite)
            {
                _renderer.sprite = sprite;
            }
        }

        // ============ Validation ============

        private void OnValidate()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }
        }
    }
}
