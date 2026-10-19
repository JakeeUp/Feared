using Brawler.Core;
using UnityEngine;

namespace Brawler.Combat
{
    /// <summary>Published once when an enemy is defeated. XP orbs, score and future quest logic hang off it.</summary>
    public readonly struct EnemyDefeatedEvent : IEvent
    {
        // ============ Fields ============

        /// <summary>Where the enemy fell (world space).</summary>
        public readonly Vector3 Position;

        /// <summary>Experience the enemy is worth.</summary>
        public readonly int Xp;

        // ============ Construction ============

        /// <summary>Creates the event.</summary>
        public EnemyDefeatedEvent(Vector3 position, int xp)
        {
            Position = position;
            Xp = xp;
        }
    }
}
