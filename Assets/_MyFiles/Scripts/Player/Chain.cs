using Brawler.Combat;
using UnityEngine;

namespace Brawler.Player
{
    /// <summary>
    /// The player's chain while it's in the air. Throwing it is not a state: the hook flies out while the player keeps
    /// running, jumping, attacking, and only when it lands does something happen (swing, yank or zip, decided by
    /// PlayerController). Plain class owned by <see cref="PlayerContext"/>; no allocation after construction.
    /// </summary>
    public sealed class Chain
    {
        // ============ Constants ============

        /// <summary>The hook gives up if the player gets this much further than the range from it.</summary>
        private const float SnapRangeFactor = 1.4f;

        // ============ Properties ============

        /// <summary>True while the hook is flying toward a target.</summary>
        public bool IsFlying { get; private set; }

        /// <summary>World position of the hook tip.</summary>
        public Vector3 Tip { get; private set; }

        /// <summary>Ring being thrown at (or null).</summary>
        public GrappleAnchor Anchor { get; private set; }

        /// <summary>Enemy being thrown at (or null).</summary>
        public IChainTarget Enemy { get; private set; }

        /// <summary>Where the hook is headed.</summary>
        public Vector3 TargetPoint => Anchor != null ? Anchor.Position : Enemy != null ? Enemy.ChainPoint : Tip;

        // ============ API ============

        /// <summary>Throws the hook from <paramref name="origin"/> at a ring or an enemy (exactly one non-null).</summary>
        public void Fire(Vector3 origin, GrappleAnchor anchor, IChainTarget enemy)
        {
            Tip = origin;
            Anchor = anchor;
            Enemy = anchor != null ? null : enemy;
            IsFlying = anchor != null || enemy != null;
        }

        /// <summary>Moves the hook. Returns true on the frame it reaches its target.</summary>
        /// <param name="origin">The player's hand (the chain's other end).</param>
        /// <param name="speed">Hook speed (units/s).</param>
        /// <param name="deltaTime">Scaled delta time.</param>
        /// <param name="range">Chain range; the throw is cancelled if the player ends up far beyond it.</param>
        public bool Advance(Vector3 origin, float speed, float deltaTime, float range)
        {
            if (!IsFlying)
            {
                return false;
            }

            bool valid = Anchor != null ? Anchor.isActiveAndEnabled : Enemy != null && Enemy.CanBeChained;
            if (!valid || (origin - Tip).sqrMagnitude > range * range * SnapRangeFactor * SnapRangeFactor)
            {
                Cancel();
                return false;
            }

            Vector3 target = TargetPoint;
            Tip = Vector3.MoveTowards(Tip, target, speed * deltaTime);
            return (target - Tip).sqrMagnitude < 0.0001f;
        }

        /// <summary>Drops the hook (target gone, player hit, or it landed and was resolved).</summary>
        public void Cancel()
        {
            IsFlying = false;
            Anchor = null;
            Enemy = null;
        }
    }
}
