namespace Brawler.Combat
{
    /// <summary>Phase of an attack at a given frame. See <see cref="AttackFrames.GetPhase"/>.</summary>
    public enum AttackPhase
    {
        /// <summary>Wind-up: frames [0, Startup). Hitbox off.</summary>
        Startup = 0,

        /// <summary>Hitbox live: frames [Startup, Startup + Active).</summary>
        Active = 1,

        /// <summary>Recovery: frames [Startup + Active, TotalFrames). Hitbox off.</summary>
        Recovery = 2,

        /// <summary>Frame &gt;= TotalFrames: the attack is over.</summary>
        Finished = 3,
    }
}
