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
                _windowSeconds = value < 0f ? 0f : value;
            }
        }

        /// <summary>Fixed maximum number of stored entries.</summary>
        public int Capacity => _entries.Length;

        /// <summary>Number of stored entries. May include expired entries until something prunes them.</summary>
        public int Count
        {
            get
            {
                return _count;
            }
        }

        // ============ API ============

        /// <summary>Records a press of <paramref name="command"/> at <paramref name="time"/>.</summary>
        /// <param name="command">Command pressed. <see cref="BufferedCommand.None"/> is ignored.</param>
        /// <param name="time">Press timestamp in seconds (same clock as the <c>now</c> passed to queries).</param>
        public void Enqueue(BufferedCommand command, float time)
        {
            if (command == BufferedCommand.None)
            {
                return;
            }

            // Full: drop the oldest so the newest press always fits.
            if (_count == _entries.Length)
            {
                _head = (_head + 1) % _entries.Length;
                _count--;
            }

            int tail = (_head + _count) % _entries.Length;
            _entries[tail].Command = command;
            _entries[tail].Time = time;
            _count++;
        }

        /// <summary>
        /// Records a press and drops any older stored press of the same command, so mashing holds one pending
        /// press (with the newest timestamp) instead of queueing a burst that plays out back to back.
        /// </summary>
        /// <param name="command">Command pressed. <see cref="BufferedCommand.None"/> is ignored.</param>
        /// <param name="time">Press timestamp in seconds.</param>
        public void EnqueueLatest(BufferedCommand command, float time)
        {
            if (command == BufferedCommand.None)
            {
                return;
            }

            int kept = 0;
            for (int i = 0; i < _count; i++)
            {
                Entry entry = _entries[(_head + i) % _entries.Length];
                if (entry.Command != command)
                {
                    _entries[(_head + kept) % _entries.Length] = entry;
                    kept++;
                }
            }

            _count = kept;
            Enqueue(command, time);
        }

        /// <summary>
        /// Consumes the OLDEST live entry matching <paramref name="command"/>. Other commands are untouched.
        /// </summary>
        /// <param name="command">Command to look for.</param>
        /// <param name="now">Current time in seconds.</param>
        /// <returns>True if a live matching entry existed and was removed.</returns>
        public bool TryConsume(BufferedCommand command, float now)
        {
            if (command == BufferedCommand.None)
            {
                return false;
            }

            // Expired entries at the head can never become live again (now only goes up).
            while (_count > 0 && !IsLive(_entries[_head].Time, now))
            {
                _head = (_head + 1) % _entries.Length;
                _count--;
            }

            int found = FindLive(command, now);
            if (found < 0)
            {
                return false;
            }

            // Shift later entries down one slot so ordering is preserved.
            for (int i = found; i < _count - 1; i++)
            {
                _entries[(_head + i) % _entries.Length] = _entries[(_head + i + 1) % _entries.Length];
            }

            _count--;
            return true;
        }

        /// <summary>True if a live entry matching <paramref name="command"/> exists. Does not remove anything.</summary>
        /// <param name="command">Command to look for.</param>
        /// <param name="now">Current time in seconds.</param>
        /// <returns>True when at least one live matching entry is stored.</returns>
        public bool Peek(BufferedCommand command, float now)
        {
            return command != BufferedCommand.None && FindLive(command, now) >= 0;
        }

        /// <summary>Removes every stored entry (e.g. on Hitstun enter so the player can't mash out).</summary>
        public void Clear()
        {
            _head = 0;
            _count = 0;
        }

        // ============ Helpers ============

        /// <summary>Inclusive window test; future-stamped presses have a negative age and count as live.</summary>
        private bool IsLive(float pressTime, float now)
        {
            return now - pressTime <= _windowSeconds;
        }

        /// <summary>Offset from <see cref="_head"/> of the oldest live entry matching <paramref name="command"/>, or -1.</summary>
        private int FindLive(BufferedCommand command, float now)
        {
            for (int i = 0; i < _count; i++)
            {
                Entry entry = _entries[(_head + i) % _entries.Length];
                if (entry.Command == command && IsLive(entry.Time, now))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
