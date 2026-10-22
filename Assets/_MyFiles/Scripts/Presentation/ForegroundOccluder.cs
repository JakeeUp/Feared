using UnityEngine;

namespace Brawler.Presentation
{
    /// <summary>
    /// Foreground occluder (pillar, arch, railing between the camera and the play plane). Each frame it tests whether
    /// any of its renderers' bounds cross the segment from the camera to the player's chest and, if so, eases the
    /// <c>_Fade</c> of its <c>Brawler/OccluderDither</c> materials down to <see cref="_blockingFade"/> so the player
    /// shows through a screen-space dither; otherwise back to 1 (solid).
    /// </summary>
    /// <remarks>
    /// Physics-free (Bounds.IntersectRay) and allocation-free (one cached MaterialPropertyBlock, renderers cached in
    /// Awake). The fade eases on unscaled time so it keeps moving during hitstop. The camera and target are shared
    /// statics, published by <see cref="DepthOfFieldFocus"/> (or anyone else); the camera falls back to
    /// <see cref="Camera.main"/>.
    /// </remarks>
    [DefaultExecutionOrder(CameraRig.ExecutionOrder + 20)]
    [DisallowMultipleComponent]
    public sealed class ForegroundOccluder : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [Header("Fade")]
        [Tooltip("_Fade while blocking the player (0 = invisible, 1 = solid). Dithered, so depth/DoF still work.")]
        [SerializeField, Range(0f, 1f)] private float _blockingFade = 0.35f;

        [Tooltip("How fast the fade eases toward its goal (1/s, exponential, unscaled time).")]
        [SerializeField, Min(0.1f)] private float _fadeRate = 10f;

        [Header("Test")]
        [Tooltip("Height above the target's pivot the sight line aims at (units). ~chest of a 1-unit hero.")]
        [SerializeField] private float _targetHeight = 0.5f;

        [Tooltip("Bounds are grown by this (units) so the test acts like a capsule around the sight line, not a thin ray.")]
        [SerializeField, Min(0f)] private float _padding = 0.4f;

        // ============ Statics ============

        /// <summary>The player (sight-line end). Shared by all occluders; set by <see cref="DepthOfFieldFocus"/>.</summary>
        public static Transform Target { get; set; }

        /// <summary>The camera (sight-line start). Null = <see cref="Camera.main"/>.</summary>
        public static Camera ViewCamera { get; set; }

        private static readonly int FadeId = Shader.PropertyToID("_Fade");

        /// <summary>
        /// Clears the shared references on entering Play Mode. The project disables domain reload, so statics would
        /// otherwise keep destroyed objects from the previous session.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Target = null;
            ViewCamera = null;
        }

        // ============ State ============

        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private float _fade = 1f;

        /// <summary>Last value pushed to the renderers (skip SetPropertyBlock while settled). Starts solid = no block.</summary>
        private float _appliedFade = 1f;

        // ============ Properties ============

        /// <summary>Current fade (1 = solid).</summary>
        public float Fade => _fade;

        // ============ Lifecycle ============

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            _fade = 1f;
            Apply();
        }

        private void LateUpdate()
        {
            float goal = IsBlocking() ? _blockingFade : 1f;
            float t = 1f - Mathf.Exp(-_fadeRate * Time.unscaledDeltaTime);
            _fade = Mathf.Abs(goal - _fade) < 0.002f ? goal : Mathf.Lerp(_fade, goal, t);
            Apply();
        }

        // ============ Helpers ============

        private bool IsBlocking()
        {
            Transform target = Target;
            if (target == null)
            {
                return false;
            }

            Camera cam = ViewCamera != null ? ViewCamera : Camera.main;
            if (cam == null)
            {
                return false;
            }

            Vector3 from = cam.transform.position;
            Vector3 to = target.position + Vector3.up * _targetHeight;
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.001f)
            {
                return false;
            }

            var ray = new Ray(from, delta / length);
            Vector3 grow = Vector3.one * (_padding * 2f);
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null || !r.enabled)
                {
                    continue;
                }

                Bounds bounds = r.bounds;
                bounds.size += grow;
                // Stop short of the target so geometry right behind/under the player never counts.
                if (bounds.IntersectRay(ray, out float hit) && hit < length - _padding)
                {
                    return true;
                }
            }

            return false;
        }

        private void Apply()
        {
            if (_renderers == null || Mathf.Approximately(_fade, _appliedFade))
            {
                return;
            }

            _appliedFade = _fade;
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null)
                {
                    continue;
                }

                if (_fade >= 1f)
                {
                    // Solid: drop the block so the renderer goes back into the SRP Batcher.
                    r.SetPropertyBlock(null);
                    continue;
                }

                _block.SetFloat(FadeId, _fade);
                r.SetPropertyBlock(_block);
            }
        }
    }
}
