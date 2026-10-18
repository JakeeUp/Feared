namespace Brawler.Combat
{
    /// <summary>
    /// Pure combo-chain bookkeeping: tracks which link of a combo is playing and decides whether the chain
    /// can advance. Knows only indices (never <see cref="AttackData"/>), so it is EditMode-testable without assets.
    /// The owner maps <see cref="CurrentIndex"/> to an attack via <see cref="ComboData.GetAttack"/>.
    /// </summary>
    /// <remarks>
    /// Lifecycle used by the player: Idle/Move consume Attack -&gt; <see cref="Begin"/>; AttackState cancel window +
    /// <see cref="HasNext"/> + consume Attack -&gt; <see cref="TryAdvance"/>; attack finished / dash-cancel / hitstun -&gt;
    /// <see cref="Reset"/>.
    /// </remarks>
    public sealed class ComboTracker
    {
        // ============ State ============

        private readonly int _comboLength;
        private int _currentIndex;

        // ============ Construction ============

        /// <summary>Creates a tracker for a chain of <paramref name="comboLength"/> attacks. Starts not in a combo.</summary>
        /// <param name="comboLength">Number of links in the chain (&gt;= 0; negative is treated as 0).</param>
        public ComboTracker(int comboLength)
        {
            // Wiring: store the fixed length and start "not in a combo".
            _comboLength = comboLength < 0 ? 0 : comboLength;
            _currentIndex = -1;
        }

        // ============ Properties ============

        /// <summary>Number of links in the chain.</summary>
        public int ComboLength => _comboLength;

        /// <summary>Index of the link currently playing, or -1 when not in a combo.</summary>
        public int CurrentIndex => _currentIndex;

        /// <summary>True while a combo is in progress (<see cref="CurrentIndex"/> &gt;= 0).</summary>
        public bool IsInCombo
        {
            get
            {
                return _currentIndex >= 0;
            }
        }

        /// <summary>True when in a combo and another link follows the current one.</summary>
        public bool HasNext
        {
            get
            {
                // Checked BEFORE consuming Attack, so a press during the finisher stays buffered.
                return IsInCombo && _currentIndex + 1 < _comboLength;
            }
        }

        /// <summary>True when the current link is the last one in the chain.</summary>
        public bool IsFinisher
        {
            get
            {
                return IsInCombo && _currentIndex == _comboLength - 1;
            }
        }

        // ============ API ============

        /// <summary>Starts (or restarts) the combo at link 0.</summary>
        /// <returns>0, or -1 if the chain is empty (tracker stays not-in-combo).</returns>
        public int Begin()
        {
            _currentIndex = _comboLength > 0 ? 0 : -1;
            return _currentIndex;
        }

        /// <summary>Advances to the next link if one exists.</summary>
        /// <param name="nextIndex">The new index on success; the unchanged <see cref="CurrentIndex"/> on failure.</param>
        /// <returns>True if the chain advanced.</returns>
        public bool TryAdvance(out int nextIndex)
        {
            if (!HasNext)
            {
                nextIndex = _currentIndex;
                return false;
            }

            _currentIndex++;
            nextIndex = _currentIndex;
            return true;
        }

        /// <summary>Ends the combo (<see cref="CurrentIndex"/> = -1).</summary>
        public void Reset()
        {
            _currentIndex = -1;
        }
    }
}
