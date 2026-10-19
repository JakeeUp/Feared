using System.Collections.Generic;
using UnityEngine;

namespace Brawler.Combat
{
    /// <summary>How something reacts to the player's chain.</summary>
    public enum ChainWeight
    {
        /// <summary>Gets yanked to the player (grunts, casters, fliers, which are pulled out of the sky).</summary>
        Light = 0,

        /// <summary>Too heavy to move: the player zips to it instead (elites, the boss).</summary>
        Heavy = 1,
    }

    /// <summary>
    /// Something the chain can hook. Implementers register in <see cref="ChainTargets"/> while they can be hooked, so
    /// targeting never searches the scene.
    /// </summary>
    public interface IChainTarget
    {
        /// <summary>Yank or zip.</summary>
        ChainWeight ChainWeight { get; }

        /// <summary>Where the hook lands (about chest height), in world space.</summary>
        Vector3 ChainPoint { get; }

        /// <summary>False while dead, dormant, destroyed or otherwise untouchable. Safe to ask a destroyed target.</summary>
        bool CanBeChained { get; }

        /// <summary>Hooked by a light pull: take <paramref name="damage"/>, get stunned and dragged to <paramref name="destination"/>.</summary>
        /// <param name="destination">Where it should end up (just in front of the player).</param>
        /// <param name="stunFrames">Hitstun frames.</param>
        /// <param name="damage">Damage of the hook itself.</param>
        /// <param name="source">The player (DamageInfo source).</param>
        /// <returns>True if it was yanked.</returns>
        bool Yank(Vector3 destination, int stunFrames, int damage, Transform source);
    }

    /// <summary>Registry of hookable targets (enabled enemies, the active boss).</summary>
    public static class ChainTargets
    {
        private static readonly List<IChainTarget> Targets = new List<IChainTarget>();

        /// <summary>
        /// Clears the list on entering Play Mode. The project disables domain reload, so statics would otherwise keep
        /// destroyed objects from the previous session.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Targets.Clear();
        }

        /// <summary>Every registered target. Read-only use; filter with <see cref="IChainTarget.CanBeChained"/>.</summary>
        public static IReadOnlyList<IChainTarget> All => Targets;

        /// <summary>Adds a target (no duplicates).</summary>
        public static void Register(IChainTarget target)
        {
            if (target != null && !Targets.Contains(target))
            {
                Targets.Add(target);
            }
        }

        /// <summary>Removes a target.</summary>
        public static void Unregister(IChainTarget target)
        {
            Targets.Remove(target);
        }
    }
}
