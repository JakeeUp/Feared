using UnityEngine;

namespace Brawler.Combat
{
    /// <summary>Global combat-query tuning shared by every <c>Hitbox</c>.</summary>
    [CreateAssetMenu(menuName = "Brawler/Combat/Combat Settings", fileName = "CombatSettings")]
    public sealed class CombatSettings : ScriptableObject
    {
        // ============ Serialized Fields ============

        [Header("Queries")]
        [Tooltip("Layers searched for Hurtbox colliders. Default Everything (no custom layer yet); non-Hurtbox colliders are filtered out by component check.")]
        [SerializeField] private LayerMask _hurtboxLayers = ~0;

        [Tooltip("Size of the preallocated Collider[] used by Physics.OverlapBoxNonAlloc. Extra overlaps beyond this are dropped.")]
        [SerializeField, Min(1)] private int _maxOverlapResults = 16;

        [Header("Depth (Z lane)")]
        [Tooltip("Half-depth of every hitbox along world Z, in units. Beat 'em up 'same lane' tolerance: targets further than this in Z are not hit even if X/Y overlap.")]
        [SerializeField, Min(0.01f)] private float _depthTolerance = 0.35f;

        // ============ Properties ============

        /// <summary>Layers searched for hurtboxes.</summary>
        public LayerMask HurtboxLayers => _hurtboxLayers;

        /// <summary>Overlap buffer size.</summary>
        public int MaxOverlapResults => _maxOverlapResults;

        /// <summary>Hitbox half-depth along Z.</summary>
        public float DepthTolerance => _depthTolerance;
    }
}
