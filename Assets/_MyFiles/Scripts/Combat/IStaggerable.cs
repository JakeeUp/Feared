namespace Brawler.Combat
{
    /// <summary>Something that can be left reeling, e.g. an attacker whose swing was parried.</summary>
    public interface IStaggerable
    {
        /// <summary>Interrupts whatever the target is doing and stuns it for <paramref name="frames"/> (60 Hz).</summary>
        void Stagger(int frames);
    }
}
