using Brawler.Core;
using UnityEngine;

namespace Brawler.Combat
{
    /// <summary>
    /// Published when a hit is parried. Consumed by feedback (hitstop, shake, spark) the same way as
    /// <see cref="HitEvent"/>, but it never counts as a hit (no damage numbers, no combo).
    /// </summary>
    public readonly struct ParryEvent : IEvent
    {
        // ============ Fields ============

        /// <summary>Where the blades met (world space).</summary>
        public readonly Vector3 Point;

        /// <summary>The defender (who parried).</summary>
        public readonly Transform Defender;

        /// <summary>The attacker that was parried. May be null.</summary>
        public readonly Transform Attacker;

        /// <summary>Hitstop in 60 Hz frames.</summary>
        public readonly int HitStopFrames;

        /// <summary>Screen shake trauma, 0..1.</summary>
        public readonly float ScreenShake;

        // ============ Construction ============

        /// <summary>Creates a parry event.</summary>
        public ParryEvent(Vector3 point, Transform defender, Transform attacker, int hitStopFrames, float screenShake)
        {
            Point = point;
            Defender = defender;
            Attacker = attacker;
            HitStopFrames = hitStopFrames;
            ScreenShake = screenShake;
        }
    }
}
