using UnityEngine;

namespace Brawler.Level
{
    /// <summary>
    /// A red seal across a doorway (Devil May Cry style): rises out of the floor when an <see cref="Encounter"/> or
    /// <see cref="BossArena"/> locks the room and burns away once it is cleared, so the player reads "kill everything
    /// here first". The collider blocks while closed and drops the moment it starts opening.
    /// </summary>
    /// <remarks>
    /// Drives <c>_Reveal</c> (0 = gone, 1 = full wall) on its <c>Brawler/RedSeal</c> material through a property block,
    /// plus an optional pulsing red light. Gates without this component are simply toggled with SetActive
    /// (see <see cref="Set"/>).
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SealBarrier : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [SerializeField] private Renderer _renderer;

        [Tooltip("Solid while sealed. Defaults to the Collider on this object.")]
        [SerializeField] private Collider _collider;

        [Tooltip("Optional red glow that pulses with the seal.")]
        [SerializeField] private Light _light;

        [SerializeField, Min(0.01f)] private float _riseSeconds = 0.35f;

        [SerializeField, Min(0.01f)] private float _fallSeconds = 0.6f;

        [SerializeField, Min(0f)] private float _lightIntensity = 3f;

        // ============ State ============

        private static readonly int RevealId = Shader.PropertyToID("_Reveal");

        private MaterialPropertyBlock _block;
        private float _reveal;
        private bool _closed;

        // ============ API ============

        /// <summary>Seals (<paramref name="closed"/>) or opens <paramref name="gate"/>: animated when it has a SealBarrier, else SetActive.</summary>
        public static void Set(GameObject gate, bool closed, bool instant = false)
        {
            if (gate == null)
            {
                return;
            }

            if (gate.TryGetComponent(out SealBarrier seal))
            {
                seal.SetClosed(closed, instant);
                return;
            }

            gate.SetActive(closed);
        }

        /// <summary>Starts rising (closed) or burning away (open). <paramref name="instant"/> skips the animation.</summary>
        public void SetClosed(bool closed, bool instant = false)
        {
            // May run before our Awake (another object's Awake seals or opens us), so resolve references here too.
            ResolveReferences();
            _closed = closed;
            if (_collider != null)
            {
                _collider.enabled = closed;
            }

            if (closed)
            {
                gameObject.SetActive(true);
            }

            if (instant)
            {
                _reveal = closed ? 1f : 0f;
                Apply();
                if (!closed)
                {
                    gameObject.SetActive(false);
                }
            }
        }

        // ============ Lifecycle ============

        private void Awake()
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (_collider == null)
            {
                _collider = GetComponent<Collider>();
            }

            if (_renderer == null)
            {
                _renderer = GetComponent<Renderer>();
            }
        }

        private void Update()
        {
            float goal = _closed ? 1f : 0f;
            float rate = 1f / (_closed ? _riseSeconds : _fallSeconds);
            _reveal = Mathf.MoveTowards(_reveal, goal, rate * Time.deltaTime);
            Apply();

            if (!_closed && _reveal <= 0f)
            {
                gameObject.SetActive(false);
            }
        }

        // ============ Helpers ============

        private void Apply()
        {
            if (_renderer != null)
            {
                _block ??= new MaterialPropertyBlock();
                _block.SetFloat(RevealId, _reveal);
                _renderer.SetPropertyBlock(_block);
            }

            if (_light != null)
            {
                float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 7f);
                _light.intensity = _lightIntensity * _reveal * pulse;
            }
        }
    }
}
