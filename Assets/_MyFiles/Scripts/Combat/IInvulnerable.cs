namespace Brawler.Combat
{
    /// <summary>
    /// I-frame query. <c>Hitbox</c> skips invulnerable hurtboxes WITHOUT marking them as hit, so the same
    /// activation can still connect once the i-frames end. Implemented by <c>PlayerController</c> (dash i-frames).
    /// </summary>
    public interface IInvulnerable
    {
        /// <summary>True while hits must be ignored entirely (no damage, no hitstun, no HitEvent).</summary>
        bool IsInvulnerable { get; }
    }
}
