// Most private fields aren't touched until the TODOs below are filled in. Drop this pragma once they are.
#pragma warning disable CS0169, CS0649, CS0414

namespace Brawler.Core
{
    /// <summary>
    /// Fixed-capacity, allocation-free ring buffer of recently pressed <see cref="BufferedCommand"/>s.
    /// Lets the player press Attack/Dash slightly early (e.g. during recovery or hitstop) and still have it
    /// count when the state becomes able to act. Input-agnostic: time is passed in, which keeps it testable.
    /// </summary>
    /// <remarks>
    /// An entry is "live" when <c>now - time &lt;= WindowSeconds</c> (inclusive). Entries stamped in the future
    /// (<c>time &gt; now</c>) also count as live. Only the array allocated in the constructor is ever used.
    /// </remarks>
    public sealed class InputBuffer
    {
        // ============ Types ============

        /// <summary>One buffered press: what was pressed and when (seconds, same clock as <c>now</c>).</summary>
        private struct Entry
        {
            /// <summary>The pressed command (never <see cref="BufferedCommand.None"/> for a stored entry).</summary>
            public BufferedCommand Command;

            /// <summary>Timestamp of the press in seconds (e.g. <c>Time.time</c>).</summary>
            public float Time;
        }

        // ============ State ============

        /// <summary>Ring storage, allocated once in the constructor.</summary>
        private readonly Entry[] _entries;

        /// <summary>Index of the OLDEST stored entry within <see cref="_entries"/>.</summary>
        private int _head;

        /// <summary>Number of stored entries (live or expired-but-not-pruned).</summary>
        private int _count;

        /// <summary>Backing field for <see cref="WindowSeconds"/>.</summary>
        private float _windowSeconds;

        // ============ Construction ============

        /// <summary>Creates an empty buffer.</summary>
        /// <param name="windowSeconds">Seconds a press stays live (see <c>InputBufferSettings.BufferWindowSeconds</c>).</param>
        /// <param name="capacity">Maximum stored entries; values below 1 are clamped to 1. The only allocation happens here.</param>
        public InputBuffer(float windowSeconds, int capacity)
        {
            _windowSeconds = windowSeconds;
            _entries = new Entry[capacity < 1 ? 1 : capacity];
        }

        // ============ Properties ============

        /// <summary>Seconds a buffered press stays live. Settable so tuning can change at runtime.</summary>
        public float WindowSeconds
        {
            get => _windowSeconds;
            set
            {
                // TODO: store it. Probably clamp negatives to 0 - a negative window means nothing ever buffers.
            }
        }

        /// <summary>Fixed maximum number of stored entries.</summary>
        public int Capacity => _entries.Length;

        /// <summary>Number of stored entries. May include expired entries until something prunes them.</summary>
        public int Count
        {
            get
            {
                // TODO: return _count.
                return 0;
            }
        }

        // ============ API ============

        /// <summary>Records a press of <paramref name="command"/> at <paramref name="time"/>.</summary>
        /// <param name="command">Command pressed. <see cref="BufferedCommand.None"/> is ignored.</param>
        /// <param name="time">Press timestamp in seconds (same clock as the <c>now</c> passed to queries).</param>
        public void Enqueue(BufferedCommand command, float time)
        {
            // TODO: write at the ring's tail. When full, drop the oldest - a press from 8 inputs ago is worth less than the
            // one that just came in. Ignore None. Presses already arrive in time order, so no sorting.
        }

        /// <summary>
        /// Consumes the OLDEST live entry matching <paramref name="command"/>. Other commands are untouched.
        /// </summary>
        /// <param name="command">Command to look for.</param>
        /// <param name="now">Current time in seconds.</param>
        /// <returns>True if a live matching entry existed and was removed.</returns>
        public bool TryConsume(BufferedCommand command, float now)
        {
            // TODO: walk oldest -> newest, remove the first live match, return true.
            // - Only ever remove ONE. Mashing attack 3x during recovery should feed three combo steps, not one.
            // - Keep the rest in order when removing (shift down; capacity is ~8 so the cost is nothing).
            // - Expired entries at the head can never come back (now only goes up), so pruning them here keeps Count honest.
            // Context: fighting games tend to buffer somewhere in the 3-10 frame range. The window lives in
            // InputBufferSettings so it gets tuned by feel instead of guessed here.
            return false;
        }

        /// <summary>True if a live entry matching <paramref name="command"/> exists. Does not remove anything.</summary>
        /// <param name="command">Command to look for.</param>
        /// <param name="now">Current time in seconds.</param>
        /// <returns>True when at least one live matching entry is stored.</returns>
        public bool Peek(BufferedCommand command, float now)
        {
            // TODO: same scan as TryConsume but read-only - Count and ordering can't change.
            return false;
        }

        /// <summary>Removes every stored entry (e.g. on Hitstun enter so the player can't mash out).</summary>
        public void Clear()
        {
            // TODO: reset head/count. No need to wipe the array, stale slots are unreachable anyway.
        }

        // ============ Helpers ============

        // TODO: IsLive(pressTime, now) => now - pressTime <= window.
        // That one comparison covers both the inclusive boundary and future-stamped presses (negative age).
        // Careful with float precision in the tests - use exactly-representable values or a small epsilon.
    }
}
