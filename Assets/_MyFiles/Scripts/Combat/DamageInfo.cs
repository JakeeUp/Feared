using UnityEngine;

namespace Brawler.Combat
{
    /// <summary>Immutable payload describing one hit. Built by <see cref="AttackData.CreateDamageInfo"/>.</summary>
    public readonly struct DamageInfo
    {
        // ============ Fields ============

        /// <summary>Damage dealt (whole points; shown by damage numbers).</summary>
        public readonly int Amount;

        /// <summary>World-space knockback velocity (units/s), already mirrored by attacker facing.</summary>
        public readonly Vector3 Knockback;

        /// <summary>Hitstun duration in 60 Hz simulation frames.</summary>
        public readonly int HitstunFrames;

        /// <summary>World-space contact point (closest point on the hurtbox to the hitbox center).</summary>
        public readonly Vector3 HitPoint;

        /// <summary>Attacker root transform. May be null (environmental damage).</summary>
        public readonly Transform Source;

        // ============ Construction ============

        /// <summary>Creates a damage payload.</summary>
        /// <param name="amount">Damage dealt.</param>
        /// <param name="knockback">World-space knockback velocity.</param>
        /// <param name="hitstunFrames">Hitstun in frames.</param>
        /// <param name="hitPoint">World-space contact point.</param>
        /// <param name="source">Attacker root transform, or null.</param>
        public DamageInfo(int amount, Vector3 knockback, int hitstunFrames, Vector3 hitPoint, Transform source)
        {
            Amount = amount;
            Knockback = knockback;
            HitstunFrames = hitstunFrames;
            HitPoint = hitPoint;
            Source = source;
        }
    }
}
