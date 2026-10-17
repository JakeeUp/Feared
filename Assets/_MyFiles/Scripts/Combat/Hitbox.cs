// Most private fields aren't touched until the TODOs below are filled in. Drop this pragma once they are.
#pragma warning disable CS0169, CS0649, CS0414

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
            // TODO: arm the hitbox for this attack - store attack + facing (force +/-1, never 0) and clear the hit set
            // (Clear keeps capacity, so no alloc).
            // Clearing on every BeginAttack is what lets the next link of a combo hit the same dummy again.
        }

        /// <summary>Runs one overlap query for the current simulation frame and applies hits.</summary>
        /// <returns>Number of NEW targets hit by this call (0 if inactive or nothing connected).</returns>
        public int Sweep()
        {
            // TODO: the actual hit check. Returns how many targets got hit this call.
            // - OverlapBoxNonAlloc into _results with QueryTriggerInteraction.Collide - hurtboxes are triggers, so the default
            //   setting can skip them depending on project physics settings. Anything past _results.Length is dropped silently
            //   (see the docs), so size MaxOverlapResults with some headroom.
            // - Per result: needs a Hurtbox, isn't ours, wasn't already hit this activation, has a damageable owner.
            // - Invulnerable (i-frames) -> skip WITHOUT marking as hit. If the active window outlasts the dodge, the target
            //   can still get clipped at the end, which is how it should feel.
            // - Damage first, then knockback. Hitstun's Enter zeroes velocity, so doing knockback first would get wiped.
            // - Only on a successful TryApplyDamage: mark hit, then publish HitEvent (hitstop/shake/numbers all hang off it).
            // - Plain for loop, no LINQ/lambdas - this runs every active frame.
            return 0;
        }

        /// <summary>Disarms the hitbox and clears per-activation state.</summary>
        public void EndAttack()
        {
            // TODO: disarm and clear. Has to be safe when already inactive - AttackState.Exit and HitstunState.Enter both
            // call it on the same transition sometimes.
        }

        /// <summary>World-space center of the hitbox for the current attack and facing.</summary>
        /// <returns>transform.position + (offset.x * facing, offset.y, 0); transform.position if no attack.</returns>
        public Vector3 GetWorldCenter()
        {
            // TODO: owner position + the attack's offset, with X flipped by facing. World axes rather than transform.right -
            // characters never actually rotate, facing is just a sign.
            return default;
        }

        /// <summary>Half extents of the axis-aligned hitbox box.</summary>
        /// <returns>(size.x / 2, size.y / 2, CombatSettings.DepthTolerance); zero if no attack/settings.</returns>
        public Vector3 GetHalfExtents()
        {
            // TODO: half the attack's size on X/Y; Z comes from CombatSettings.DepthTolerance, which is already a half-depth.
            // That Z tolerance is the beat 'em up "same lane" check - how far apart on depth two characters can be and still
            // connect.
            return default;
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
