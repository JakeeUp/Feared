using Brawler.Combat;
using UnityEngine;

namespace Brawler.Presentation
{
    /// <summary>
    /// Small health bar floating above a character: a background quad and a fill quad (children of this transform)
    /// whose X scale follows <see cref="Health.Normalized"/>, left-anchored. Faces the camera like the sprites do.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldHealthBar : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [Tooltip("Health to display.")]
        [SerializeField] private Health _health;

        [Tooltip("Fill quad (child). Its X scale is the bar's full width at full health.")]
        [SerializeField] private Transform _fill;

        [Tooltip("Camera to face. Spawned enemies receive it at runtime.")]
        [SerializeField] private Transform _cameraTransform;

        [Tooltip("Hide while at full health (enemies); keep visible for the player.")]
        [SerializeField] private bool _hideWhenFull = true;

        [Tooltip("Units to pull the bar toward the camera along its view ray (same spot on screen, ~5% larger at distance 11) so it never sinks into walls/pillars beside or behind the character. 0 = off.")]
        [SerializeField, Min(0f)] private float _cameraPull = 0.75f;

        // ============ State ============

        private Renderer[] _renderers;
        private float _fullWidth;
        private bool _visible = true;
        private float _shown = -1f;

        /// <summary>Authored local position (above the head); the camera pull is re-applied on top of it each frame.</summary>
        private Vector3 _baseLocalPosition;

        // ============ Properties ============

        /// <summary>Camera to face.</summary>
        public Transform CameraTransform
        {
            get => _cameraTransform;
            set => _cameraTransform = value;
        }

        // ============ Lifecycle ============

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _fullWidth = _fill != null ? _fill.localScale.x : 1f;
            _baseLocalPosition = transform.localPosition;
        }

        private void LateUpdate()
        {
            if (_health == null || _fill == null)
            {
                return;
            }

            if (_cameraTransform != null)
            {
                transform.rotation = _cameraTransform.rotation;
                ApplyCameraPull();
            }

            float normalized = _health.Normalized;
            SetVisible(!_health.IsDead && !(_hideWhenFull && normalized >= 1f));
            if (Mathf.Approximately(normalized, _shown))
            {
                return;
            }

            // Left-anchored: shrink the fill and slide it so its left edge stays put.
            _shown = normalized;
            Vector3 scale = _fill.localScale;
            scale.x = _fullWidth * normalized;
            _fill.localScale = scale;
            Vector3 position = _fill.localPosition;
            position.x = -_fullWidth * (1f - normalized) * 0.5f;
            _fill.localPosition = position;
        }

        // ============ Helpers ============

        /// <summary>Moves the bar <see cref="_cameraPull"/> units from its authored spot toward the camera.</summary>
        private void ApplyCameraPull()
        {
            Transform parent = transform.parent;
            Vector3 basePosition = parent != null ? parent.TransformPoint(_baseLocalPosition) : _baseLocalPosition;

            // Along the ray to the camera so the on-screen position is unchanged (perspective camera).
            Vector3 toCamera = _cameraTransform.position - basePosition;
            float distance = toCamera.magnitude;
            float pull = distance > 1e-4f ? Mathf.Min(_cameraPull, distance * 0.5f) / distance : 0f;
            transform.position = basePosition + toCamera * pull;
        }

        private void SetVisible(bool visible)
        {
            if (visible == _visible)
            {
                return;
            }

            _visible = visible;
            foreach (Renderer r in _renderers)
            {
                r.enabled = visible;
            }
        }
    }
}
