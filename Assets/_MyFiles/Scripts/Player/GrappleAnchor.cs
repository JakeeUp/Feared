using System.Collections.Generic;
using UnityEngine;

namespace Brawler.Player
{
    /// <summary>
    /// A fixed point the player's grapple can latch onto. Enabled anchors register themselves in a static list, so
    /// targeting never searches the scene. The optional glow sprite brightens and pulses while the anchor is the
    /// current target, so the player always knows what the grapple button will grab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrappleAnchor : MonoBehaviour
    {
        // ============ Constants ============

        /// <summary>The anchor must be at least this far above the rope origin (units). No grappling sideways or down.</summary>
        public const float MinHeightAboveOrigin = 0.4f;

        /// <summary>Line-of-sight slack (units): geometry this close to the anchor itself doesn't block it.</summary>
        private const float SightSlack = 0.35f;

        private static readonly List<GrappleAnchor> Active = new List<GrappleAnchor>();

        // ============ Serialized Fields ============

        [Header("Visuals (optional)")]
        [Tooltip("Glow sprite. Dim when idle, bright and pulsing while targeted.")]
        [SerializeField] private SpriteRenderer _glow;

        [SerializeField] private Color _idleColor = new Color(0.55f, 0.75f, 1f, 0.45f);

        [SerializeField] private Color _targetColor = new Color(1f, 0.9f, 0.5f, 1f);

        [Tooltip("Pulse speed while targeted (cycles/s).")]
        [SerializeField, Min(0f)] private float _pulseSpeed = 3f;

        // ============ Runtime ============

        private float _blockedUntil;
        private bool _targeted;
        private Vector3 _baseScale = Vector3.one;

        // ============ Properties ============

        /// <summary>The point the rope attaches to.</summary>
        public Vector3 Position => transform.position;

        /// <summary>Every enabled anchor (targeting reads this; see ChainTargeting).</summary>
        public static IReadOnlyList<GrappleAnchor> All => Active;

        /// <summary>True for a moment after being let go, so the player can't hang off one ring forever.</summary>
        public bool IsBlocked => Time.time < _blockedUntil;

        /// <summary>True while the player is swinging from this anchor or it's the highlighted target.</summary>
        public bool IsTargeted
        {
            get => _targeted;
            set => _targeted = value;
        }

        // ============ Lifecycle ============

        /// <summary>
        /// Clears the list on entering Play Mode. The project disables domain reload, so statics would otherwise keep
        /// destroyed objects from the previous session.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active.Clear();
        }

        private void Awake()
        {
            if (_glow != null)
            {
                _baseScale = _glow.transform.localScale;
            }
        }

        private void OnEnable()
        {
            Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
            _targeted = false;
        }

        private void LateUpdate()
        {
            if (_glow == null)
            {
                return;
            }

            if (_targeted)
            {
                float pulse = 1f + 0.15f * Mathf.Sin(Time.unscaledTime * _pulseSpeed * Mathf.PI * 2f);
                _glow.color = _targetColor;
                _glow.transform.localScale = _baseScale * pulse;
            }
            else
            {
                _glow.color = _idleColor;
                _glow.transform.localScale = _baseScale;
            }
        }

        // ============ API ============

        /// <summary>Stops this anchor being picked for <paramref name="seconds"/> (scaled time).</summary>
        public void BlockFor(float seconds)
        {
            _blockedUntil = Time.time + seconds;
        }

        /// <summary>True when nothing solid (characters aside) lies between <paramref name="from"/> and just short of <paramref name="to"/>.</summary>
        public static bool HasLineOfSight(Vector3 from, Vector3 to, LayerMask mask)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude - SightSlack;
            if (length <= 0f)
            {
                return true;
            }

            if (!Physics.Raycast(from, delta.normalized, out RaycastHit hit, length, mask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            return hit.collider is CharacterController; // characters never block the rope
        }

        // ============ Debug ============

        private void OnDrawGizmos()
        {
            Gizmos.color = _targeted ? Color.yellow : new Color(0.5f, 0.8f, 1f);
            Gizmos.DrawWireSphere(transform.position, 0.25f);
        }
    }
}
