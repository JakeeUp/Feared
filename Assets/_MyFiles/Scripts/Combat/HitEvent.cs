using Brawler.Core;
using UnityEngine;

namespace Brawler.Combat
{
    /// <summary>
    /// Published by <c>Hitbox</c> once per connected hit (after damage + knockback are applied).
    /// Consumed by Presentation feedback: <c>HitStop</c>, <c>ScreenShake</c>, <c>DamageNumberSpawner</c>.
    /// </summary>
    public readonly struct HitEvent : IEvent
    {
        // ============ Fields ============

        /// <summary>The damage that was applied (amount, knockback, hit point, source).</summary>
        public readonly DamageInfo Damage;

        /// <summary>Root transform of the target that was hit (the Hurtbox owner).</summary>
        public readonly Transform Target;

        /// <summary>Hitstop to apply, in 60 Hz frames (from <see cref="AttackData.HitStopFrames"/>).</summary>
        public readonly int HitStopFrames;

        /// <summary>Screen shake trauma to add, 0..1 (from <see cref="AttackData.ScreenShake"/>).</summary>
        public readonly float ScreenShake;

        // ============ Construction ============

        /// <summary>Creates a hit event.</summary>
        /// <param name="damage">Applied damage.</param>
        /// <param name="target">Hit target root.</param>
        /// <param name="hitStopFrames">Hitstop frames.</param>
        /// <param name="screenShake">Shake trauma 0..1.</param>
        public HitEvent(in DamageInfo damage, Transform target, int hitStopFrames, float screenShake)
        {
            Damage = damage;
            Target = target;
            HitStopFrames = hitStopFrames;
            ScreenShake = screenShake;
        }
    }
}
