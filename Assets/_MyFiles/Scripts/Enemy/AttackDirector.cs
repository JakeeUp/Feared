using System.Collections.Generic;

namespace Brawler.Enemy
{
    /// <summary>
    /// Turn-taking for a group of enemies (the beat 'em up "attack director"). Enemies that are ready to attack
    /// <see cref="Enlist"/> in a queue; only the front <c>MaxAttackers</c> may close in, and an attack may only start
    /// when a token is free and at least <c>MinInterval</c> seconds have passed since the last one started.
    /// After attacking (or being hit) an enemy leaves the queue and rejoins at the back, so everyone takes turns.
    /// </summary>
    /// <remarks>Pure C#, time is passed in, so it is EditMode-testable. The queue is preallocated.</remarks>
    public sealed class AttackDirector
    {
        // ============ State ============

        private readonly AttackTokenPool _tokens;
        private readonly List<object> _queue = new List<object>(16);
        private readonly float _minInterval;
        private float _nextAttackTime = float.NegativeInfinity;

        // ============ Construction ============

        /// <summary>Creates a director.</summary>
        /// <param name="maxAttackers">Enemies allowed to close in and attack at once (at least 1).</param>
        /// <param name="minInterval">Seconds between consecutive attack starts across the whole group.</param>
        public AttackDirector(int maxAttackers, float minInterval)
        {
            _tokens = new AttackTokenPool(maxAttackers);
            _minInterval = minInterval < 0f ? 0f : minInterval;
        }

        // ============ Properties ============

        /// <summary>Enemies allowed to attack at once.</summary>
        public int MaxAttackers => _tokens.Capacity;

        /// <summary>Enemies currently attacking.</summary>
        public int AttackingCount => _tokens.Count;

        /// <summary>Enemies waiting for a turn.</summary>
        public int QueueLength => _queue.Count;

        // ============ API ============

        /// <summary>Joins the back of the queue (no-op if already queued or attacking).</summary>
        public void Enlist(object enemy)
        {
            if (enemy == null || _queue.Contains(enemy) || _tokens.Holds(enemy))
            {
                return;
            }

            _queue.Add(enemy);
        }

        /// <summary>
        /// True if the enemy is attacking or among the first MaxAttackers in the queue. Front enemies close in (the next
        /// one approaches while the current attacker is still swinging); everyone else hangs back.
        /// </summary>
        public bool IsFront(object enemy)
        {
            if (_tokens.Holds(enemy))
            {
                return true;
            }

            int index = _queue.IndexOf(enemy);
            return index >= 0 && index < _tokens.Capacity;
        }

        /// <summary>Position in the queue (0 = next), or -1 when not queued.</summary>
        public int QueueIndex(object enemy)
        {
            return _queue.IndexOf(enemy);
        }

        /// <summary>
        /// Starts an attack if the enemy is at the front, a token is free and the group interval has passed.
        /// On success the enemy leaves the queue and holds a token until <see cref="EndAttack"/>.
        /// </summary>
        public bool TryBeginAttack(object enemy, float now)
        {
            if (enemy == null || _tokens.Holds(enemy))
            {
                return enemy != null;
            }

            if (now < _nextAttackTime || !IsFront(enemy) || !_tokens.TryAcquire(enemy))
            {
                return false;
            }

            _queue.Remove(enemy);
            _nextAttackTime = now + _minInterval;
            return true;
        }

        /// <summary>Returns the enemy's token after its attack (it must <see cref="Enlist"/> again for another turn).</summary>
        public void EndAttack(object enemy)
        {
            _tokens.Release(enemy);
        }

        /// <summary>Removes the enemy from the queue and returns any token (hit, killed, despawned).</summary>
        public void Withdraw(object enemy)
        {
            _queue.Remove(enemy);
            _tokens.Release(enemy);
        }

        /// <summary>Forgets everyone.</summary>
        public void Clear()
        {
            _queue.Clear();
            _tokens.Clear();
            _nextAttackTime = float.NegativeInfinity;
        }
    }
}
