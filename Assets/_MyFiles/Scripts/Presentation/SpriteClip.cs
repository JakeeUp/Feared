using UnityEngine;

namespace Brawler.Presentation
{
    /// <summary>
    /// A frame-by-frame sprite animation: ordered sprites plus playback rate. Played by <see cref="SpriteAnimator"/>,
    /// either on its own clock or with the frame chosen externally (attacks sync to their frame data that way).
    /// </summary>
    [CreateAssetMenu(menuName = "Brawler/Presentation/Sprite Clip", fileName = "Clip_New")]
    public sealed class SpriteClip : ScriptableObject
    {
        // ============ Serialized Fields ============

        [Tooltip("Frames in playback order.")]
        [SerializeField] private Sprite[] _frames = new Sprite[0];

        [Tooltip("Playback rate when the animator runs the clip on its own clock.")]
        [SerializeField, Min(0.01f)] private float _framesPerSecond = 12f;

        [Tooltip("Loop back to frame 0 at the end; otherwise hold the last frame.")]
        [SerializeField] private bool _loop = true;

        [Tooltip("Tick when the art is drawn facing the opposite way from the rest of the character's clips (the sprite is mirrored relative to FacingSign).")]
        [SerializeField] private bool _mirrored;

        // ============ Properties ============

        /// <summary>Number of frames.</summary>
        public int FrameCount => _frames != null ? _frames.Length : 0;

        /// <summary>Playback rate (frames per second).</summary>
        public float FramesPerSecond => _framesPerSecond;

        /// <summary>True if playback wraps.</summary>
        public bool Loop => _loop;

        /// <summary>True if this clip's art faces the other way.</summary>
        public bool Mirrored => _mirrored;

        /// <summary>Length of one pass in seconds.</summary>
        public float Duration => FrameCount / _framesPerSecond;

        // ============ API ============

        /// <summary>Returns frame <paramref name="index"/>, clamped into range. Null for an empty clip.</summary>
        public Sprite GetFrame(int index)
        {
            if (FrameCount == 0)
            {
                return null;
            }

            return _frames[Mathf.Clamp(index, 0, FrameCount - 1)];
        }

#if UNITY_EDITOR
        /// <summary>Editor tooling: fills the clip in one call.</summary>
        public void EditorSet(Sprite[] frames, float framesPerSecond, bool loop, bool mirrored)
        {
            _frames = frames;
            _framesPerSecond = Mathf.Max(0.01f, framesPerSecond);
            _loop = loop;
            _mirrored = mirrored;
        }
#endif
    }
}
