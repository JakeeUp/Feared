using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Brawler.Presentation
{
    /// <summary>
    /// HD-2D focus puller: every LateUpdate sets the volume's bokeh <see cref="DepthOfField"/> focus distance to the
    /// camera-to-target distance, so the play plane (the player) stays sharp while near foreground and deep background
    /// blur (Octopath-style miniature look). Also publishes the camera/target to <see cref="ForegroundOccluder"/>.
    /// </summary>
    /// <remarks>
    /// Works on the volume's runtime profile copy (<see cref="Volume.profile"/>), so Play Mode never writes into the
    /// shared profile asset. Runs after <see cref="CameraRig"/> so the camera has already moved this frame.
    /// </remarks>
    [DefaultExecutionOrder(CameraRig.ExecutionOrder + 10)]
    [DisallowMultipleComponent]
    public sealed class DepthOfFieldFocus : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [Header("References")]
        [Tooltip("Global volume whose profile has (or gets) a DepthOfField override.")]
        [SerializeField] private Volume _volume;

        [Tooltip("Camera the distance is measured from. Null = the Camera on this GameObject.")]
        [SerializeField] private Camera _camera;

        [Tooltip("What stays in focus (the Player root).")]
        [SerializeField] private Transform _target;

        [Header("Lens")]
        [Tooltip("Height above the target's pivot that is focused on (units). ~chest of a 1-unit hero.")]
        [SerializeField] private float _targetHeight = 0.5f;

        [Tooltip("Bokeh focal length (mm). Longer = shallower focus band.")]
        [SerializeField, Range(1f, 300f)] private float _focalLength = 60f;

        [Tooltip("Bokeh aperture (f-stop). Lower = shallower focus band.")]
        [SerializeField, Range(1f, 32f)] private float _aperture = 4f;

        // ============ State ============

        /// <summary>Cached override on the runtime profile; null if the volume has none.</summary>
        private DepthOfField _dof;

        // ============ Properties ============

        /// <summary>The focus target. Settable at runtime (e.g. after a respawn spawns a new player).</summary>
        public Transform Target
        {
            get => _target;
            set
            {
                _target = value;
                ForegroundOccluder.Target = value;
            }
        }

        // ============ Lifecycle ============

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }

            if (_volume == null)
            {
                Debug.LogWarning($"[DepthOfFieldFocus] '{name}' has no Volume assigned.", this);
                return;
            }

            VolumeProfile profile = _volume.profile; // runtime copy, not the asset
            if (profile == null || !profile.TryGet(out _dof))
            {
                Debug.LogWarning($"[DepthOfFieldFocus] Volume '{_volume.name}' has no DepthOfField override.", this);
                return;
            }

            _dof.active = true;
            _dof.mode.Override(DepthOfFieldMode.Bokeh);
            _dof.focalLength.Override(_focalLength);
            _dof.aperture.Override(_aperture);
        }

        private void OnEnable()
        {
            ForegroundOccluder.ViewCamera = _camera;
            ForegroundOccluder.Target = _target;
        }

        private void LateUpdate()
        {
            if (_dof == null || _camera == null || _target == null)
            {
                return;
            }

            Vector3 focusPoint = _target.position + Vector3.up * _targetHeight;
            Transform cam = _camera.transform;
            // Distance along the view axis (what the DoF shader compares against), not the euclidean distance.
            float distance = Vector3.Dot(focusPoint - cam.position, cam.forward);
            _dof.focusDistance.Override(Mathf.Max(0.1f, distance));
        }
    }
}
