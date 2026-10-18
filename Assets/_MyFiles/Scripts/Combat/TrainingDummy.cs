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
        // ============ Constants ============

        /// <summary>Downward velocity held while grounded so CharacterController.isGrounded stays stable.</summary>
        private const float GroundStickVelocity = -2f;

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

            _currentHealth = MaxHealth;
            _knockbackVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _timeSinceLastHit = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_settings == null || _characterController == null || dt <= 0f)
            {
                return;
            }

            if (_currentHealth < MaxHealth)
            {
                _timeSinceLastHit += dt;
                if (_timeSinceLastHit >= _settings.ResetDelaySeconds)
                {
                    _currentHealth = MaxHealth;
                }
            }

            _knockbackVelocity = Vector3.MoveTowards(_knockbackVelocity, Vector3.zero, _settings.KnockbackDeceleration * dt);

            // Small constant push while grounded keeps isGrounded from flickering.
            if (_characterController.isGrounded && _verticalVelocity <= 0f)
            {
                _verticalVelocity = GroundStickVelocity;
            }
            else
            {
                _verticalVelocity += _settings.Gravity * dt;
            }

            // One Move per frame: isGrounded is only refreshed by Move.
            _characterController.Move((_knockbackVelocity + Vector3.up * _verticalVelocity) * dt);
        }

        // ============ IDamageable ============

        /// <summary>Applies damage. Always connects (the dummy is never invulnerable).</summary>
        /// <param name="info">Incoming hit.</param>
        /// <returns>True (hit accepted).</returns>
        public bool TryApplyDamage(in DamageInfo info)
        {
            _currentHealth = Mathf.Max(0, _currentHealth - info.Amount);
            _timeSinceLastHit = 0f;
            _knockbackVelocity = Vector3.zero;
            return true;
        }

        // ============ IKnockbackable ============

        /// <summary>Sets knockback velocity (scaled by <see cref="TrainingDummySettings.KnockbackScale"/>).</summary>
        /// <param name="velocity">World-space knockback velocity in units/s (Y = launch).</param>
        public void ApplyKnockback(Vector3 velocity)
        {
            float scale = _settings != null ? _settings.KnockbackScale : 1f;
            _knockbackVelocity = new Vector3(velocity.x, 0f, velocity.z) * scale;

            if (velocity.y > 0f)
            {
                _verticalVelocity = velocity.y * scale;
            }
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
