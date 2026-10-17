// Most private fields aren't touched until the TODOs below are filled in. Drop this pragma once they are.
#pragma warning disable CS0169, CS0649, CS0414

using UnityEngine;

namespace Brawler.Presentation
{
    /// <summary>
    /// Fixed-angle follow camera (Octopath / Paper Mario style). Hierarchy:
    /// <c>CameraRig</c> (this, follows the target) → <c>Pivot</c> (pitch/yaw) → <c>ShakeRoot</c> (ScreenShake offsets
    /// its localPosition) → <c>Main Camera</c> (local position (0, 0, -Distance)).
    /// </summary>
    /// <remarks>
    /// Follow runs in LateUpdate with <b>scaled</b> time (during hitstop the target is frozen, so the rig is too).
    /// Runs before <see cref="BillboardSprite"/> via <see cref="ExecutionOrder"/>.
    /// </remarks>
    [DefaultExecutionOrder(ExecutionOrder)]
    [DisallowMultipleComponent]
    public sealed class CameraRig : MonoBehaviour
    {
        // ============ Constants ============

        /// <summary>Script execution order. Late so the target has finished moving; billboards run after this.</summary>
        public const int ExecutionOrder = 100;

        // ============ Serialized Fields ============

        [Header("References")]
        [Tooltip("Transform to follow (the Player root). Null = the rig stays where it is.")]
        [SerializeField] private Transform _target;

        [Tooltip("Angle, distance, FOV and follow tuning.")]
        [SerializeField] private CameraRigSettings _settings;

        [Tooltip("Child that carries the pitch/yaw rotation. The camera sits under it (via ShakeRoot) at local (0, 0, -Distance).")]
        [SerializeField] private Transform _pivot;

        [Tooltip("The rig's camera. Its FOV is driven by the settings.")]
        [SerializeField] private Camera _camera;

        // ============ State ============

        /// <summary>SmoothDamp velocity (ref parameter); reset on snap.</summary>
        private Vector3 _followVelocity;

        // ============ Properties ============

        /// <summary>The rig camera (for systems that need projection, e.g. future screen-space UI).</summary>
        public Camera Camera => _camera;

        /// <summary>Current SmoothDamp velocity (debug/inspection).</summary>
        public Vector3 FollowVelocity => _followVelocity;

        // ============ Lifecycle ============

        private void Awake()
        {
            ApplySettings();
        }

        private void Start()
        {
            // TODO: snap to the target on the first frame so the camera doesn't swoop in from wherever it was placed.
        }

        private void LateUpdate()
        {
            // TODO: smooth follow of target + offset. SmoothDamp works; frame-rate independent exponential decay
            // (1 - exp(-k * dt)) is the other common option - Freya Holmer's "Lerp smoothing is broken" talk explains why a
            // plain Lerp(a, b, t) per frame isn't it.
            // - Scaled deltaTime, so the camera holds still during hitstop (that sells the impact).
            // - Only move this transform. Angle lives on the pivot and shake on ShakeRoot, so the three never fight.
            // - Later: softer Y follow and arena bounds clamping.
        }

        private void OnValidate()
        {
            // Editor preview: lets you tweak the settings asset / rig in edit mode and see the result.
            ApplySettings();
        }

        // ============ API ============

        /// <summary>Teleports the rig to the target (plus offset) and clears follow velocity. Use on spawn / respawn / scene load.</summary>
        public void SnapToTarget()
        {
            // TODO: jump straight to target + offset and zero the SmoothDamp velocity.
        }

        /// <summary>
        /// Applies <see cref="CameraRigSettings"/> to the hierarchy: pivot rotation (pitch, yaw), camera local position
        /// (0, 0, -Distance) and field of view. Called from Awake and OnValidate.
        /// </summary>
        public void ApplySettings()
        {
            // TODO: push the settings onto the rig - pitch/yaw on the pivot, distance as the camera's local -Z, FOV.
            // - Leave ShakeRoot at local zero, ScreenShake owns it.
            // - This also runs from OnValidate, so null-check everything and don't snap the rig from here.
            // - Editing the settings asset itself won't call this - a [ContextMenu] "Apply Settings" would be handy.
            // - Input isn't camera-relative yet, so keep yaw at 0 until it is.
            // Narrow FOV + far distance is what gives the flatter, almost-orthographic HD-2D look.
        }
    }
}
