using System.Collections.Generic;
using Brawler.Core;
using UnityEngine;

namespace Brawler.Combat
{
    /// <summary>
    /// Attack volume. Its transform is the hitbox origin (a child at the attacker's feet). The owning state calls
    /// <see cref="BeginAttack"/> on attack start, <see cref="Sweep"/> once per Active simulation frame, and
    /// <see cref="EndAttack"/> on exit. Each target can be hit at most once per activation.
    /// </summary>
    /// <remarks>
    /// The box is axis-aligned (no rotation): X width mirrored by facing, Y height, Z = 2 * <see cref="CombatSettings.DepthTolerance"/>
    /// (the beat 'em up "same lane" check). All buffers are preallocated in Awake — <see cref="Sweep"/> never allocates.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class Hitbox : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [Header("Settings")]
        [Tooltip("Shared combat query settings (hurtbox layers, overlap buffer size, Z depth tolerance).")]
        [SerializeField] private CombatSettings _combatSettings;

        [Tooltip("Shared event bus asset. HitEvent is published here once per connected target per activation.")]
        [SerializeField] private EventBusAsset _eventBus;

        [Header("Owner")]
        [Tooltip("Attacker root transform. Used as DamageInfo.Source (knockback direction, future AI aggro).")]
        [SerializeField] private Transform _owner;

        [Tooltip("The attacker's own hurtbox; skipped by Sweep so you can never hit yourself.")]
        [SerializeField] private Hurtbox _ownerHurtbox;

        [Header("Debug")]
        [Tooltip("Draw the hitbox volume in the Scene view (red while active).")]
        [SerializeField] private bool _drawGizmos = true;

        // ============ Buffers (allocated once in Awake) ============

        private Collider[] _results;
        private HashSet<Hurtbox> _hitThisActivation;

        // ============ Runtime State ============

        private AttackData _attack;
        private int _facingSign = 1;
        private bool _isActive;

        // ============ Properties ============

        /// <summary>True between <see cref="BeginAttack"/> and <see cref="EndAttack"/>.</summary>
        public bool IsActive => _isActive;

        // ============ Lifecycle ============

        private void Awake()
        {
            // Wiring: preallocate the overlap buffer and the per-activation dedupe set (never resized at runtime).
            int capacity = _combatSettings != null ? Mathf.Max(1, _combatSettings.MaxOverlapResults) : 16;
            _results = new Collider[capacity];
            _hitThisActivation = new HashSet<Hurtbox>();

            if (_owner == null)
            {
                _owner = transform.root;
            }
        }

        // ============ API ============

        /// <summary>Arms the hitbox for a new attack activation.</summary>
        /// <param name="attack">Attack providing hitbox shape, damage and game-feel values.</param>
        /// <param name="facingSign">Attacker facing (+1 right, -1 left) — mirrors the X offset and knockback. Locked for the activation.</param>
        public void BeginAttack(AttackData attack, int facingSign)
        {
            _attack = attack;
            _facingSign = facingSign < 0 ? -1 : 1;

            // Fresh set per activation lets the next combo link hit the same target again.
            _hitThisActivation.Clear();
            _isActive = attack != null;
        }

        /// <summary>Runs one overlap query for the current simulation frame and applies hits.</summary>
        /// <returns>Number of NEW targets hit by this call (0 if inactive or nothing connected).</returns>
        public int Sweep()
        {
            if (!_isActive || _attack == null || _combatSettings == null)
            {
                return 0;
            }

            Vector3 center = GetWorldCenter();
            int count = Physics.OverlapBoxNonAlloc(
                center,
                GetHalfExtents(),
                _results,
                Quaternion.identity,
                _combatSettings.HurtboxLayers,
                QueryTriggerInteraction.Collide);

            int hits = 0;
            for (int i = 0; i < count; i++)
            {
                if (!_results[i].TryGetComponent(out Hurtbox hurtbox)
                    || hurtbox == _ownerHurtbox
                    || hurtbox.Damageable == null
                    || _hitThisActivation.Contains(hurtbox))
                {
                    continue;
                }

                // Not marked as hit, so the target can still be clipped once its i-frames end.
                if (hurtbox.IsInvulnerable)
                {
                    continue;
                }

                Vector3 hitPoint = hurtbox.Collider != null ? hurtbox.Collider.ClosestPoint(center) : hurtbox.transform.position;
                DamageInfo info = _attack.CreateDamageInfo(_facingSign, hitPoint, _owner);

                // Damage before knockback: hitstun Enter zeroes velocity.
                if (!hurtbox.Damageable.TryApplyDamage(info))
                {
                    continue;
                }

                hurtbox.Knockbackable?.ApplyKnockback(info.Knockback);
                _hitThisActivation.Add(hurtbox);
                hits++;

                if (_eventBus != null)
                {
                    _eventBus.Publish(new HitEvent(info, hurtbox.OwnerRoot, _attack.HitStopFrames, _attack.ScreenShake));
                }
            }

            return hits;
        }

        /// <summary>Disarms the hitbox and clears per-activation state.</summary>
        public void EndAttack()
        {
            _isActive = false;
            _attack = null;
            _hitThisActivation?.Clear();
        }

        /// <summary>World-space center of the hitbox for the current attack and facing.</summary>
        /// <returns>transform.position + (offset.x * facing, offset.y, 0); transform.position if no attack.</returns>
        public Vector3 GetWorldCenter()
        {
            if (_attack == null)
            {
                return transform.position;
            }

            Vector2 offset = _attack.HitboxOffset;
            return transform.position + new Vector3(offset.x * _facingSign, offset.y, 0f);
        }

        /// <summary>Half extents of the axis-aligned hitbox box.</summary>
        /// <returns>(size.x / 2, size.y / 2, CombatSettings.DepthTolerance); zero if no attack/settings.</returns>
        public Vector3 GetHalfExtents()
        {
            if (_attack == null || _combatSettings == null)
            {
                return Vector3.zero;
            }

            Vector2 size = _attack.HitboxSize;
            return new Vector3(size.x * 0.5f, size.y * 0.5f, _combatSettings.DepthTolerance);
        }

        // ============ Validation ============

        private void OnValidate()
        {
            if (_ownerHurtbox != null && _owner != null && _ownerHurtbox.transform.root != _owner.root)
            {
                Debug.LogWarning($"{nameof(Hitbox)} on '{name}': owner hurtbox is not under the owner hierarchy — self-hit skip may target the wrong hurtbox.", this);
            }
        }

        // ============ Debug ============

        private void OnDrawGizmos()
        {
            if (!_drawGizmos || _attack == null)
            {
                return;
            }

            Gizmos.color = _isActive ? new Color(1f, 0.15f, 0.15f, 0.9f) : new Color(1f, 0.6f, 0.1f, 0.5f);
            Gizmos.DrawWireCube(GetWorldCenter(), GetHalfExtents() * 2f);
        }
    }
}
