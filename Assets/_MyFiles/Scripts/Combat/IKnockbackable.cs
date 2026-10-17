using UnityEngine;

namespace Brawler.Combat
{
    /// <summary>Something that can be physically displaced by a hit. Optional: immovable targets skip it.</summary>
    public interface IKnockbackable
    {
        /// <summary>
        /// Sets (does not add to) the target's knockback velocity. Called by <c>Hitbox</c> right AFTER a
        /// successful <see cref="IDamageable.TryApplyDamage"/>, so a hitstun state entered there is not overwritten.
        /// </summary>
        /// <param name="velocity">World-space velocity in units/second, already mirrored by attacker facing (X) — Y may launch.</param>
        void ApplyKnockback(Vector3 velocity);
    }
}
