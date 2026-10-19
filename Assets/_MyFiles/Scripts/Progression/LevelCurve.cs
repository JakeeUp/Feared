using UnityEngine;

namespace Brawler.Progression
{
    /// <summary>Experience needed per level: <c>base * growth^(level - 1)</c>, rounded. Pure, EditMode-testable.</summary>
    public static class LevelCurve
    {
        /// <summary>XP needed to go from <paramref name="level"/> to the next.</summary>
        /// <param name="level">Current level (1-based; values below 1 count as 1).</param>
        /// <param name="baseXp">XP from level 1 to 2 (at least 1).</param>
        /// <param name="growth">Multiplier per level (at least 1).</param>
        public static int XpToNext(int level, int baseXp, float growth)
        {
            int steps = Mathf.Max(0, level - 1);
            float xp = Mathf.Max(1, baseXp) * Mathf.Pow(Mathf.Max(1f, growth), steps);
            return Mathf.Max(1, Mathf.RoundToInt(xp));
        }
    }
}
