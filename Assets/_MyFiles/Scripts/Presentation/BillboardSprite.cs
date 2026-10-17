// Most private fields aren't touched until the TODOs below are filled in. Drop this pragma once they are.
#pragma warning disable CS0169, CS0649, CS0414

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
        [Tooltip("True: copy the full camera rotation (sprite tilts back with the camera pitch — the classic Octopath look, no foreshortening). False: yaw only (sprite stays upright; looks shorter under high pitch but never leans into the floor).")]
        [SerializeField] private bool _matchCameraPitch = true;

        // ============ State ============

        /// <summary>Cached cast of <see cref="_facingSource"/>; null when not wired or invalid.</summary>
        private IFacingSource _facing;

        // ============ Properties ============

        /// <summary>The facing source in use, or null.</summary>
        public IFacingSource FacingSource => _facing;

        // ============ Lifecycle ============

        private void Awake()
        {
            // Wiring: Unity can't serialize interfaces, so cast the MonoBehaviour once here.
            _facing = _facingSource as IFacingSource;
        }

        private void LateUpdate()
        {
            // TODO: billboard + flip.
            // - Rotation: copy the camera's rotation (or yaw-only if _matchCameraPitch is off - guard the straight-down case).
            //   Copying rotation instead of LookAt(camera) is deliberate: LookAt makes sprites near the screen edges twist
            //   differently from ones in the middle. With a fixed camera you want every sprite parallel to the view plane,
            //   which is how the Paper Mario / Octopath look works. Real-Time Rendering's billboarding section covers the
            //   difference (view-plane aligned vs viewpoint oriented).
            // - Flip: SpriteRenderer.flipX from FacingSign rather than negative scale - doesn't mirror children or the shadow,
            //   and doesn't mess with batching. Only write it when it actually changes.
            // - Placeholder art faces right; if the real art faces left, invert the check.
            // - Needs to run after CameraRig's LateUpdate (handled by DefaultExecutionOrder) or sprites lag a frame.
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
