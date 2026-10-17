namespace Brawler.Combat
{
    /// <summary>Something that can take damage from a <c>Hitbox</c> (reached through its <c>Hurtbox</c>).</summary>
    public interface IDamageable
    {
        /// <summary>
        /// Applies a hit. Called by <c>Hitbox</c> at most once per target per attack activation,
        /// and only if the target's hurtbox is not invulnerable.
        /// </summary>
        /// <param name="info">Damage payload (amount, knockback, hitstun, contact point, source).</param>
        /// <returns>True if the hit connected (Hitbox then applies knockback and publishes <c>HitEvent</c>); false if rejected (e.g. dead).</returns>
        bool TryApplyDamage(in DamageInfo info);
    }
}
