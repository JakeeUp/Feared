namespace Brawler.Enemy
{
    /// <summary>
    /// Limits how many enemies may attack at once (the classic beat 'em up "attack token"): an enemy must hold a token
    /// to start an attack and returns it when the attack ends or is interrupted. Everyone else hangs back.
    /// Pure C#, allocation-free after construction, so it is EditMode-testable.
    /// </summary>
    public sealed class AttackTokenPool
    {
        // ============ State ============

        private readonly object[] _holders;
        private int _count;

        // ============ Construction ============

        /// <summary>Creates a pool with <paramref name="capacity"/> tokens (values below 1 become 1).</summary>
        public AttackTokenPool(int capacity)
        {
            _holders = new object[capacity < 1 ? 1 : capacity];
        }

        // ============ Properties ============

        /// <summary>Total tokens.</summary>
        public int Capacity => _holders.Length;

        /// <summary>Tokens currently held.</summary>
        public int Count => _count;

        // ============ API ============

        /// <summary>True if <paramref name="holder"/> already holds a token.</summary>
        public bool Holds(object holder)
        {
            return IndexOf(holder) >= 0;
        }

        /// <summary>True if <paramref name="holder"/> holds a token or one is free.</summary>
        public bool CanAcquire(object holder)
        {
            return holder != null && (Holds(holder) || _count < _holders.Length);
        }

        /// <summary>Takes a token for <paramref name="holder"/>. Idempotent: holding one already returns true.</summary>
        /// <returns>True if <paramref name="holder"/> now holds a token.</returns>
        public bool TryAcquire(object holder)
        {
            if (holder == null)
            {
                return false;
            }

            if (Holds(holder))
            {
                return true;
            }

            if (_count >= _holders.Length)
            {
                return false;
            }

            _holders[_count++] = holder;
            return true;
        }

        /// <summary>Returns <paramref name="holder"/>'s token. Safe to call when it holds none.</summary>
        public void Release(object holder)
        {
            int index = IndexOf(holder);
            if (index < 0)
            {
                return;
            }

            // Order doesn't matter: swap the last holder into the hole.
            _count--;
            _holders[index] = _holders[_count];
            _holders[_count] = null;
        }

        /// <summary>Returns every token.</summary>
        public void Clear()
        {
            for (int i = 0; i < _count; i++)
            {
                _holders[i] = null;
            }

            _count = 0;
        }

        // ============ Helpers ============

        private int IndexOf(object holder)
        {
            if (holder == null)
            {
                return -1;
            }

            for (int i = 0; i < _count; i++)
            {
                if (ReferenceEquals(_holders[i], holder))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
