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
                // TODO: return _currentIndex >= 0.
                return false;
            }
        }

        /// <summary>True when in a combo and another link follows the current one.</summary>
        public bool HasNext
        {
            get
            {
                // TODO: in a combo and there's another link after this one.
                // AttackState checks this BEFORE consuming Attack, which is what makes the finisher dash-cancel only - an attack
                // press during the finisher's window stays in the buffer instead of getting eaten.
                return false;
            }
        }

        /// <summary>True when the current link is the last one in the chain.</summary>
        public bool IsFinisher
        {
            get
            {
                // TODO: current link is the last one. Empty chain -> never true. A 1-hit chain: that hit is the finisher.
                return false;
            }
        }

        // ============ API ============

        /// <summary>Starts (or restarts) the combo at link 0.</summary>
        /// <returns>0, or -1 if the chain is empty (tracker stays not-in-combo).</returns>
        public int Begin()
        {
            // TODO: start at link 0 and return 0. Empty chain -> stay at -1 and return -1 so the caller stays put.
            // Calling Begin mid-combo just restarts it, no error.
            return -1;
        }

        /// <summary>Advances to the next link if one exists.</summary>
        /// <param name="nextIndex">The new index on success; the unchanged <see cref="CurrentIndex"/> on failure.</param>
        /// <returns>True if the chain advanced.</returns>
        public bool TryAdvance(out int nextIndex)
        {
            // TODO: step to the next link if there is one. On failure don't touch anything - nextIndex is just the
            // current index (-1 when not in a combo, the finisher's index when at the end).
            nextIndex = _currentIndex;
            return false;
        }

        /// <summary>Ends the combo (<see cref="CurrentIndex"/> = -1).</summary>
        public void Reset()
        {
            // TODO: back to -1. Safe to call when not in a combo.
        }
    }
}
