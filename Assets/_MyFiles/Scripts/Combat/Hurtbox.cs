using System.Collections.Generic;
using UnityEngine;

namespace Brawler.Combat
{
    /// <summary>
    /// Damage receiver volume. Lives on a child of a damageable character with a <b>trigger</b> collider.
    /// <see cref="Hitbox.Sweep"/> finds it via <c>Physics.OverlapBoxNonAlloc</c> + <c>TryGetComponent&lt;Hurtbox&gt;</c>
    /// and routes damage/knockback to <see cref="Damageable"/> / <see cref="Knockbackable"/>.
    /// </summary>
    /// <remarks>
    /// <c>_owner</c> is serialized as <see cref="MonoBehaviour"/> because Unity cannot serialize interfaces;
    /// it is cast once in Awake. The owner must implement <see cref="IDamageable"/> and may implement
    /// <see cref="IKnockbackable"/> and <see cref="IInvulnerable"/> (the player does, for dash i-frames).
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class Hurtbox : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [Header("Volume")]
        [Tooltip("Trigger collider defining this hurtbox volume. Must have isTrigger = true (Hitbox queries with QueryTriggerInteraction.Collide). Defaults to a Collider on this GameObject.")]
        [SerializeField] private Collider _collider;

        [Header("Owner")]
        [Tooltip("Component receiving hits. Must implement IDamageable; optionally IKnockbackable and IInvulnerable (e.g. PlayerController, TrainingDummy).")]
        [SerializeField] private MonoBehaviour _owner;

        [Tooltip("Team of the owner. Hitboxes skip hurtboxes on their own team (Neutral is hit by everyone).")]
        [SerializeField] private CombatTeam _team = CombatTeam.Neutral;

        // ============ Registry ============

        private static readonly List<Hurtbox> EnemyList = new List<Hurtbox>(32);

        /// <summary>Enabled hurtboxes on the Enemy team (enemies and bosses), for HUD indicators.</summary>
        public static IReadOnlyList<Hurtbox> Enemies => EnemyList;

        /// <summary>Domain reload is off: drop hurtboxes left over from the previous play session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            EnemyList.Clear();
        }

        // ============ Cached ============

        private IDamageable _damageable;
        private IKnockbackable _knockbackable;
        private IInvulnerable _invulnerable;

        // ============ Properties ============

        /// <summary>Damage receiver resolved from the owner in Awake. Null if the owner is missing/invalid.</summary>
        public IDamageable Damageable => _damageable;

        /// <summary>Knockback receiver resolved from the owner in Awake. May be null (immovable targets).</summary>
        public IKnockbackable Knockbackable => _knockbackable;

        /// <summary>True while the owner reports invulnerability (e.g. dash i-frames). False if the owner has no <see cref="IInvulnerable"/>.</summary>
        public bool IsInvulnerable => _invulnerable != null && _invulnerable.IsInvulnerable;

        /// <summary>Root transform of the owner (used as <see cref="HitEvent.Target"/>). Null if no owner.</summary>
        public Transform OwnerRoot => _owner != null ? _owner.transform : null;

        /// <summary>Team of the owner.</summary>
        public CombatTeam Team => _team;

        /// <summary>The hurtbox trigger collider (used for <c>ClosestPoint</c> hit points).</summary>
        public Collider Collider => _collider;

        // ============ Lifecycle ============

        private void Awake()
        {
            // Wiring: cache components and cast the serialized owner to its interfaces once.
            if (_collider == null)
            {
                _collider = GetComponent<Collider>();
            }

            _damageable = _owner as IDamageable;
            _knockbackable = _owner as IKnockbackable;
            _invulnerable = _owner as IInvulnerable;

            if (_damageable == null)
            {
                Debug.LogWarning($"{nameof(Hurtbox)} on '{name}': owner is missing or does not implement {nameof(IDamageable)}; hits will be ignored.", this);
            }
        }

        private void OnEnable()
        {
            if (_team == CombatTeam.Enemy && !EnemyList.Contains(this))
            {
                EnemyList.Add(this);
            }
        }

        private void OnDisable()
        {
            EnemyList.Remove(this);
        }

        // ============ Validation ============

        private void OnValidate()
        {
            if (_collider == null)
            {
                _collider = GetComponent<Collider>();
            }

            if (_collider != null && !_collider.isTrigger)
            {
                Debug.LogWarning($"{nameof(Hurtbox)} on '{name}': collider should be a trigger so it doesn't block CharacterControllers.", this);
            }

            if (_owner != null && !(_owner is IDamageable))
            {
                Debug.LogWarning($"{nameof(Hurtbox)} on '{name}': owner '{_owner.GetType().Name}' does not implement {nameof(IDamageable)}.", this);
            }
        }

        // ============ Debug ============

        private void OnDrawGizmosSelected()
        {
            if (_collider == null)
            {
                return;
            }

            Bounds bounds = _collider.bounds;
            Gizmos.color = IsInvulnerable ? new Color(0.3f, 0.6f, 1f, 0.8f) : new Color(0.2f, 1f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
