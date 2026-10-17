// Most private fields aren't touched until the TODOs below are filled in. Drop this pragma once they are.
#pragma warning disable CS0169, CS0649, CS0414

using UnityEngine;

namespace Brawler.Combat
{
    /// <summary>
    /// Punching bag for testing combos. Takes damage (clamped at 0, never dies), slides with knockback, falls with
    /// gravity, and resets to full health after <see cref="TrainingDummySettings.ResetDelaySeconds"/> without hits.
    /// Wire a child <see cref="Hurtbox"/> with <c>_owner</c> = this component.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class TrainingDummy : MonoBehaviour, IDamageable, IKnockbackable
    {
        // ============ Serialized Fields ============

        [Header("Settings")]
        [Tooltip("Health, reset delay, knockback scale/deceleration and gravity.")]
        [SerializeField] private TrainingDummySettings _settings;

        [Header("Components")]
        [Tooltip("Motor used for knockback slides and gravity. Defaults to the CharacterController on this GameObject.")]
        [SerializeField] private CharacterController _characterController;

        // ============ Runtime State ============

        private int _currentHealth;
        private Vector3 _knockbackVelocity;   // planar X/Z, units/s
        private float _verticalVelocity;      // Y, units/s
        private float _timeSinceLastHit;

        // ============ Properties ============

        /// <summary>Current health (0..<see cref="MaxHealth"/>).</summary>
        public int CurrentHealth => _currentHealth;

        /// <summary>Health the dummy resets to (from settings; 0 if unassigned).</summary>
        public int MaxHealth => _settings != null ? _settings.MaxHealth : 0;

        // ============ Lifecycle ============

        private void Awake()
        {
            // Wiring: cache the motor.
            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
            }

            // TODO: full health, zero timers and velocities.
        }

        private void Update()
        {
            // TODO: per-frame upkeep on scaled time (dt is 0 during hitstop, so the dummy freezes with everything else).
            // - Refill health once it's gone ResetDelaySeconds without being hit.
            // - Bleed off knockback with MoveTowards using KnockbackDeceleration (planar only; vertical is handled below).
            // - Gravity, with a small constant downward push while grounded - isGrounded flickers without it.
            // - One CharacterController.Move per frame with everything combined. The docs are pretty clear isGrounded is
            //   only updated by Move, so calling it more than once a frame confuses it.
        }

        // ============ IDamageable ============

        /// <summary>Applies damage. Always connects (the dummy is never invulnerable).</summary>
        /// <param name="info">Incoming hit.</param>
        /// <returns>True (hit accepted).</returns>
        public bool TryApplyDamage(in DamageInfo info)
        {
            // TODO: subtract damage (floor at 0), restart the reset timer, return true.
            // Never dies or disables - at 0 it just stands there until the timer refills it.
            // Might be worth zeroing the knockback velocity here so a new hit replaces the old slide instead of stacking.
            return false;
        }

        // ============ IKnockbackable ============

        /// <summary>Sets knockback velocity (scaled by <see cref="TrainingDummySettings.KnockbackScale"/>).</summary>
        /// <param name="velocity">World-space knockback velocity in units/s (Y = launch).</param>
        public void ApplyKnockback(Vector3 velocity)
        {
            // TODO: scale by KnockbackScale, SET planar velocity (don't add - that's the contract), and use positive Y as a
            // launch (the finisher pops them up a bit).
        }

        // ============ Validation ============

        private void OnValidate()
        {
            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
            }

            if (_settings == null)
            {
                Debug.LogWarning($"{nameof(TrainingDummy)} on '{name}': {nameof(TrainingDummySettings)} not assigned.", this);
            }
        }
    }
}
