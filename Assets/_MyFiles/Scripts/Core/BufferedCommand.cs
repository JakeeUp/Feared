namespace Brawler.Core
{
    /// <summary>Input-agnostic gameplay commands stored by <c>InputBuffer</c>.</summary>
    public enum BufferedCommand
    {
        /// <summary>Default/empty value. Never enqueued (Enqueue ignores it).</summary>
        None = 0,

        /// <summary>Light attack / combo continue.</summary>
        Attack = 1,

        /// <summary>Dash (grants i-frames).</summary>
        Dash = 2,

        /// <summary>Jump (also wall jump while on a wall).</summary>
        Jump = 3,

        /// <summary>Parry: deflects a hit that lands inside the parry window.</summary>
        Parry = 4,

        /// <summary>Grapple: latch onto the best anchor in range and swing; pressed again while swinging, lets go.</summary>
        Grapple = 5,
    }
}
