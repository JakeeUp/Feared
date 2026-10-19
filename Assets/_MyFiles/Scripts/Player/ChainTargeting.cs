using System.Collections.Generic;
using Brawler.Combat;
using UnityEngine;

namespace Brawler.Player
{
    /// <summary>
    /// Picks what the chain button grabs: grapple rings and hookable enemies compete on one score, distance weighted by
    /// how well the target lines up with where the player is aiming (stick X, else facing; a firm push up aims up).
    /// Rings only count above the hand; enemies right on top of the player don't count (just hit them).
    /// </summary>
    public static class ChainTargeting
    {
        // ============ Constants ============

        /// <summary>Closest an enemy can be and still be worth hooking.</summary>
        private const float MinEnemyDistance = 1.4f;

        /// <summary>Depth (Z) counts this much less than X/Y: lanes are close together.</summary>
        private const float DepthWeight = 0.35f;

        /// <summary>How much aim matters: targets straight ahead score up to this many times better than behind.</summary>
        private const float AimWeight = 0.6f;

        /// <summary>Stick Y that tilts the aim upward.</summary>
        private const float AimUpThreshold = 0.7f;

        // ============ API ============

        /// <summary>The best target from the player's hand, or false when nothing is in reach.</summary>
        public static bool FindBest(PlayerContext context, out GrappleAnchor anchor, out IChainTarget enemy)
        {
            anchor = null;
            enemy = null;
            PlayerMovementSettings movement = context.Movement;
            if (movement == null)
            {
                return false;
            }

            Vector3 origin = context.RopeOrigin;
            Vector3 aim = AimDirection(context);
            float range = movement.GrappleRange;
            LayerMask mask = movement.WallLayers;
            float best = float.MaxValue;

            IReadOnlyList<GrappleAnchor> anchors = GrappleAnchor.All;
            for (int i = 0; i < anchors.Count; i++)
            {
                GrappleAnchor candidate = anchors[i];
                if (candidate.IsBlocked || candidate.Position.y - origin.y < GrappleAnchor.MinHeightAboveOrigin)
                {
                    continue;
                }

                float cost = Cost(origin, candidate.Position, aim, range);
                if (cost < best && GrappleAnchor.HasLineOfSight(origin, candidate.Position, mask))
                {
                    best = cost;
                    anchor = candidate;
                }
            }

            IReadOnlyList<IChainTarget> targets = ChainTargets.All;
            for (int i = 0; i < targets.Count; i++)
            {
                IChainTarget candidate = targets[i];
                if (!candidate.CanBeChained)
                {
                    continue;
                }

                Vector3 point = candidate.ChainPoint;
                if ((point - origin).sqrMagnitude < MinEnemyDistance * MinEnemyDistance)
                {
                    continue;
                }

                float cost = Cost(origin, point, aim, range);
                if (cost < best && GrappleAnchor.HasLineOfSight(origin, point, mask))
                {
                    best = cost;
                    enemy = candidate;
                    anchor = null;
                }
            }

            if (enemy != null)
            {
                anchor = null;
            }

            return anchor != null || enemy != null;
        }

        // ============ Helpers ============

        private static Vector3 AimDirection(PlayerContext context)
        {
            Vector2 stick = context.Input != null ? context.Input.Move : Vector2.zero;
            float x = Mathf.Abs(stick.x) > 0.3f ? Mathf.Sign(stick.x) : context.FacingSign;
            float y = stick.y > AimUpThreshold ? 1f : 0.35f; // a slight upward bias: rings and fliers are usually above
            return new Vector3(x, y, 0f).normalized;
        }

        /// <summary>Weighted distance, or MaxValue when out of range.</summary>
        private static float Cost(Vector3 origin, Vector3 point, Vector3 aim, float range)
        {
            Vector3 to = point - origin;
            Vector3 flat = new Vector3(to.x, to.y, to.z * DepthWeight);
            float distance = flat.magnitude;
            if (distance > range || distance < 0.001f)
            {
                return float.MaxValue;
            }

            float alignment = Vector3.Dot(new Vector3(to.x, to.y, 0f).normalized, aim); // -1 behind .. 1 dead ahead
            return distance * (1f + AimWeight * (1f - alignment));
        }
    }
}
