namespace Brawler.Combat
{
    /// <summary>Who may hit whom. A <see cref="Hitbox"/> never hits a <see cref="Hurtbox"/> on its own team; Neutral is hit by everyone.</summary>
    public enum CombatTeam
    {
        /// <summary>Hit by every team (training dummies, breakables).</summary>
        Neutral = 0,

        /// <summary>The player and allies.</summary>
        Player = 1,

        /// <summary>Enemies (no friendly fire between them).</summary>
        Enemy = 2,
    }
}
