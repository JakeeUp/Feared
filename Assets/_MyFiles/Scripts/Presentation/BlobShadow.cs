// Most private fields aren't touched until the TODOs below are filled in. Drop this pragma once they are.
#pragma warning disable CS0169, CS0649, CS0414

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

        // ============ Properties ============

        /// <summary>True while the shadow quad is shown (ground found within <see cref="_maxDistance"/>).</summary>
        public bool IsShadowVisible => _isShadowVisible;

        // ============ Lifecycle ============

        private void LateUpdate()
        {
            // TODO: raycast down from a bit above the feet and put the shadow on the floor.
            // - Ignore triggers (hurtboxes). Hit -> place at hit point + small offset, keep it flat, shrink with height
            //   (lerp toward _minScaleAtMaxDistance). No hit -> hide it.
            // - The ray starts inside our own capsule; raycasts don't report a collider the origin is inside, so we never
            //   shadow onto ourselves.
            // - Known issue: it can land on another character's capsule (standing over a dummy). Fix later with an Actor layer
            //   excluded from _groundLayers.
            // - Optional: fade alpha with height via a cached MaterialPropertyBlock.
            // Blob shadows are mostly there to show where a character is on the depth axis - without them it's really hard
            // to tell who's in line with who in a 2.5D brawler.
        }

        // ============ Helpers ============

        /// <summary>Shows/hides the shadow quad only when the state changes.</summary>
        /// <param name="visible">Desired visibility.</param>
        private void SetVisible(bool visible)
        {
            // TODO: only toggle on change. Toggling the MeshRenderer is cheaper than SetActive.
        }
    }
}
