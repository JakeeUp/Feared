using Brawler.Core;
using UnityEngine;

namespace Brawler.Presentation
{
    /// <summary>
    /// Keeps a <see cref="SpriteRenderer"/> facing the camera (Octopath / Paper Mario style billboard) and
    /// mirrors it to match an optional <see cref="IFacingSource"/>. Visual only: put it on the "Visual" child,
    /// never on the gameplay root (rotating the root would rotate the CharacterController/hitbox frame).
    /// </summary>
    /// <remarks>
    /// Runs late in LateUpdate (after <see cref="CameraRig"/>) so it uses this frame's final camera rotation.
    /// </remarks>
    [DefaultExecutionOrder(ExecutionOrder)]
    [DisallowMultipleComponent]
    public sealed class BillboardSprite : MonoBehaviour
    {
        // ============ Constants ============

        /// <summary>Script execution order: after <see cref="CameraRig"/> (which uses <see cref="CameraRig.ExecutionOrder"/>).</summary>
        public const int ExecutionOrder = CameraRig.ExecutionOrder + 10;

        // ============ Serialized Fields ============

        [Header("References")]
        [Tooltip("Camera transform to face (the Main Camera under CameraRig/Pivot/ShakeRoot). Wired in the Inspector — no Camera.main lookups.")]
        [SerializeField] private Transform _cameraTransform;

        [Tooltip("Sprite renderer to rotate and flip. Usually on this same GameObject.")]
        [SerializeField] private SpriteRenderer _spriteRenderer;

        [Tooltip("Optional. A component implementing Brawler.Core.IFacingSource (e.g. PlayerController). When set, the sprite flips to face its FacingSign. Leave empty for things that never turn.")]
        [SerializeField] private MonoBehaviour _facingSource;

        [Header("Billboard")]
        [Tooltip("True: tilt back with the camera pitch, scaled by Pitch Follow. False: yaw only (sprite stays upright; looks shorter under high pitch but never leans into what is behind it).")]
        [SerializeField] private bool _matchCameraPitch = true;

        [Tooltip("Fraction of the camera pitch (and any shake roll) to follow when Match Camera Pitch is on. 1 = parallel to the view plane (no foreshortening, but at 32 deg the top of a 1-unit sprite leans ~0.53 back into walls/platforms behind it). 0.5 = halves the lean (~0.28) for only ~4% vertical foreshortening. The sprite shader's _DepthBias covers the rest.")]
        [SerializeField, Range(0f, 1f)] private float _pitchFollow = 0.5f;

        // ============ State ============

        /// <summary>Cached cast of <see cref="_facingSource"/>; null when not wired or invalid.</summary>
        private IFacingSource _facing;

        // ============ Properties ============

        /// <summary>The facing source in use, or null.</summary>
        public IFacingSource FacingSource => _facing;

        /// <summary>Camera to face. Settable so spawned (prefab) objects can receive the scene camera at runtime.</summary>
        public Transform CameraTransform
        {
            get => _cameraTransform;
            set => _cameraTransform = value;
        }

        /// <summary>Fraction (0..1) of the camera pitch followed when <c>_matchCameraPitch</c> is on.</summary>
        public float PitchFollow
        {
            get => _pitchFollow;
            set => _pitchFollow = Mathf.Clamp01(value);
        }

        /// <summary>Inverts the facing flip, for clips whose art is drawn facing the other way (set by animation drivers).</summary>
        public bool Mirror { get; set; }

        // ============ Lifecycle ============

        private void Awake()
        {
            // Wiring: Unity can't serialize interfaces, so cast the MonoBehaviour once here.
            _facing = _facingSource as IFacingSource;
        }

        private void LateUpdate()
        {
            if (_cameraTransform != null)
            {
                // Derive from the camera rotation rather than LookAt so every sprite shares one orientation
                // (no per-sprite perspective skew). Pitch is only partly followed to limit lean-in (see Pitch Follow).
                Quaternion cameraRotation = _cameraTransform.rotation;
                Vector3 forward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up);
                if (forward.sqrMagnitude > 1e-6f)
                {
                    Quaternion yawOnly = Quaternion.LookRotation(forward, Vector3.up);
                    transform.rotation = _matchCameraPitch
                        ? Quaternion.Slerp(yawOnly, cameraRotation, _pitchFollow)
                        : yawOnly;
                }
                else
                {
                    // Camera looking straight up/down: no usable yaw, face the camera fully.
                    transform.rotation = cameraRotation;
                }
            }

            // Placeholder art faces right.
            if (_facing != null && _spriteRenderer != null)
            {
                bool flip = (_facing.FacingSign < 0) != Mirror;
                if (_spriteRenderer.flipX != flip)
                {
                    _spriteRenderer.flipX = flip;
                }
            }
        }

        // ============ Validation ============

        private void OnValidate()
        {
            // Wiring helpers (editor only): auto-fill the renderer and reject non-IFacingSource components.
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (_facingSource != null && !(_facingSource is IFacingSource))
            {
                Debug.LogWarning($"{name}: '{_facingSource.GetType().Name}' does not implement IFacingSource; clearing Facing Source.", this);
                _facingSource = null;
            }

            _facing = _facingSource as IFacingSource;
        }
    }
}
