using UnityEngine;

namespace Brawler.Presentation
{
    /// <summary>
    /// Projects a cheap blob shadow (a flat quad using <c>M_BlobShadow</c>) onto the ground below an actor and
    /// shrinks it with height, so jumps/launches read clearly in a 2.5D view. Lives on the actor root;
    /// <see cref="_shadowTransform"/> is a child quad.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlobShadow : MonoBehaviour
    {
        // ============ Constants ============

        /// <summary>Ray start height above the actor's pivot (feet), so a pivot resting exactly on the floor still hits it.</summary>
        private const float RayStartHeight = 0.1f;

        // ============ Serialized Fields ============

        [Header("References")]
        [Tooltip("The shadow quad (child, rotated flat, no collider). Moved to the ground hit point and scaled each LateUpdate.")]
        [SerializeField] private Transform _shadowTransform;

        [Header("Ground Probe")]
        [Tooltip("Layers the shadow can land on. Default Everything; triggers (hurtboxes) are always ignored.")]
        [SerializeField] private LayerMask _groundLayers = ~0;

        [Tooltip("Max downward ray length in units. Beyond this the shadow is hidden.")]
        [SerializeField, Min(0.01f)] private float _maxDistance = 5f;

        [Header("Size")]
        [Tooltip("Uniform shadow scale when the actor is standing on the ground.")]
        [SerializeField, Min(0f)] private float _baseScale = 1f;

        [Tooltip("Scale multiplier at Max Distance (lerped by height). 0.4 = shrinks to 40% at the top of the probe.")]
        [SerializeField, Range(0f, 1f)] private float _minScaleAtMaxDistance = 0.4f;

        [Tooltip("Height above the hit point to place the quad (units). Small, just to help depth precision; the shader also uses polygon offset.")]
        [SerializeField, Min(0f)] private float _groundOffset = 0.01f;

        // ============ State ============

        /// <summary>Last visibility written to the shadow (avoids SetActive every frame).</summary>
        private bool _isShadowVisible = true;

        /// <summary>The quad's renderer, cached on first toggle.</summary>
        private Renderer _shadowRenderer;

        // ============ Properties ============

        /// <summary>True while the shadow quad is shown (ground found within <see cref="_maxDistance"/>).</summary>
        public bool IsShadowVisible => _isShadowVisible;

        // ============ Lifecycle ============

        private void LateUpdate()
        {
            if (_shadowTransform == null)
            {
                return;
            }

            // Starts inside our own capsule, which raycasts never report, so we don't shadow onto ourselves.
            Vector3 origin = transform.position + Vector3.up * RayStartHeight;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, _maxDistance + RayStartHeight, _groundLayers, QueryTriggerInteraction.Ignore))
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            _shadowTransform.position = hit.point + Vector3.up * _groundOffset;

            float height01 = Mathf.Clamp01((hit.distance - RayStartHeight) / _maxDistance);
            float scale = _baseScale * Mathf.Lerp(1f, _minScaleAtMaxDistance, height01);
            _shadowTransform.localScale = new Vector3(scale, scale, scale);
        }

        // ============ Helpers ============

        /// <summary>Shows/hides the shadow quad only when the state changes.</summary>
        /// <param name="visible">Desired visibility.</param>
        private void SetVisible(bool visible)
        {
            if (visible == _isShadowVisible)
            {
                return;
            }

            if (_shadowRenderer == null)
            {
                _shadowRenderer = _shadowTransform.GetComponent<Renderer>();
            }

            if (_shadowRenderer != null)
            {
                _shadowRenderer.enabled = visible;
            }

            _isShadowVisible = visible;
        }
    }
}
