namespace Brawler.Core
{
    /// <summary>
    /// Marker interface for event-bus payloads. Implement on <c>readonly struct</c>s only;
    /// the bus constrains <c>T : struct, IEvent</c> so events are never boxed.
    /// </summary>
    public interface IEvent
    {
    }
}
