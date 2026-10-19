using System;
using UnityEngine;

namespace Brawler.Progression
{
    /// <summary>
    /// Score as currency. Combat adds to it (the combo counter), the shop spends it. Keeps the lifetime total
    /// separately so spending never lowers the "how well did I do" number.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScoreWallet : MonoBehaviour
    {
        // ============ State ============

        private int _balance;
        private int _lifetime;

        // ============ Events & Properties ============

        /// <summary>Raised whenever the balance changes.</summary>
        public event Action Changed;

        /// <summary>Spendable score.</summary>
        public int Balance => _balance;

        /// <summary>Total score ever earned this session.</summary>
        public int Lifetime => _lifetime;

        // ============ API ============

        /// <summary>Adds earned score.</summary>
        public void Add(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            _balance += amount;
            _lifetime += amount;
            Changed?.Invoke();
        }

        /// <summary>True if <paramref name="cost"/> can be paid.</summary>
        public bool CanAfford(int cost)
        {
            return cost <= _balance;
        }

        /// <summary>Pays <paramref name="cost"/> if affordable.</summary>
        /// <returns>True if paid.</returns>
        public bool TrySpend(int cost)
        {
            if (cost < 0 || cost > _balance)
            {
                return false;
            }

            _balance -= cost;
            Changed?.Invoke();
            return true;
        }
    }
}
