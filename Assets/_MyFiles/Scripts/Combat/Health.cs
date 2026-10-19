using System;
using UnityEngine;

namespace Brawler.Combat
{
    /// <summary>
    /// Hit points for anything that can be worn down (player, enemies). Owners route damage here from their
    /// <see cref="IDamageable"/> implementation and react to <see cref="Died"/>; presentation reads it for health bars.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [Tooltip("Hit points at full health. Owners with a settings asset may override this through Initialize.")]
        [SerializeField, Min(1)] private int _maxHealth = 100;

        // ============ State ============

        private int _current = -1; // -1 = not initialised yet (lazy, so EditMode use works without Awake)

        // ============ Events & Properties ============

        /// <summary>Raised after damage is applied, with the amount taken.</summary>
        public event Action<int> Damaged;

        /// <summary>Raised once when health reaches 0.</summary>
        public event Action Died;

        /// <summary>Full health.</summary>
        public int Max => _maxHealth;

        /// <summary>Current health (0..Max).</summary>
        public int Current
        {
            get
            {
                EnsureInitialised();
                return _current;
            }
        }

        /// <summary>Current / Max, 0..1.</summary>
        public float Normalized => Max > 0 ? Current / (float)Max : 0f;

        /// <summary>True once health has reached 0 (until <see cref="ResetToMax"/>).</summary>
        public bool IsDead => Current <= 0;

        // ============ API ============

        /// <summary>Sets the maximum (e.g. from an enemy settings asset) and refills.</summary>
        public void Initialize(int maxHealth)
        {
            _maxHealth = Mathf.Max(1, maxHealth);
            _current = _maxHealth;
        }

        /// <summary>Applies damage, clamped at 0. Ignored when already dead or the amount is not positive.</summary>
        /// <returns>True if this damage was the killing blow.</returns>
        public bool TakeDamage(int amount)
        {
            if (amount <= 0 || IsDead)
            {
                return false;
            }

            _current = Mathf.Max(0, _current - amount);
            Damaged?.Invoke(amount);
            if (_current > 0)
            {
                return false;
            }

            Died?.Invoke();
            return true;
        }

        /// <summary>Refills to <see cref="Max"/> (respawn).</summary>
        public void ResetToMax()
        {
            _current = _maxHealth;
        }

        /// <summary>Raises (or lowers) the maximum by <paramref name="amount"/>; current health moves by the same amount.</summary>
        public void AddMax(int amount)
        {
            EnsureInitialised();
            _maxHealth = Mathf.Max(1, _maxHealth + amount);
            _current = Mathf.Clamp(_current + amount, 0, _maxHealth);
        }

        /// <summary>Restores up to <paramref name="amount"/> health (never above Max, never revives).</summary>
        public void Heal(int amount)
        {
            if (amount <= 0 || IsDead)
            {
                return;
            }

            _current = Mathf.Min(_maxHealth, _current + amount);
        }

        // ============ Helpers ============

        private void EnsureInitialised()
        {
            if (_current < 0)
            {
                _current = _maxHealth;
            }
        }
    }
}
