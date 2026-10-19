namespace Brawler.Core
{
    /// <summary>Receives experience (implemented by the player's progression). Lets pickups stay ignorant of the player.</summary>
    public interface IXpCollector
    {
        /// <summary>Adds <paramref name="amount"/> experience.</summary>
        void AddXp(int amount);
    }
}
