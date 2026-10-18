using System;
using System.Collections.Generic;
using UnityEngine;

namespace Brawler.Core
{
    /// <summary>
    /// Default <see cref="IEventBus"/> implementation: typed, instance-based, no strings, no statics.
    /// Listeners are stored per event type in a <c>List&lt;Action&lt;T&gt;&gt;</c> (held as <c>object</c> in a
    /// dictionary keyed by <see cref="Type"/> and cast back), so event structs are never boxed.
    /// </summary>
    /// <remarks>
    /// Distributed to components through <see cref="EventBusAsset"/>. Semantics (all tested):
    /// subscription-order invocation; duplicate subscribe ignored; unsubscribe during publish means the listener is
    /// not invoked later in that publish; subscribe during publish is first invoked on the next publish; a throwing
    /// listener is logged with <see cref="Debug.LogException(Exception)"/> and the rest still run; publishing with no
    /// listeners is a no-op that allocates nothing.
    /// </remarks>
    public sealed class EventBus : IEventBus
    {
        // ============ Constants ============

        /// <summary>Initial capacity for a new per-type listener list (avoids early regrowth).</summary>
        private const int InitialListenerCapacity = 4;

        /// <summary>Initial number of event types the dictionary is sized for.</summary>
        private const int InitialTypeCapacity = 8;

        // ============ State ============

        /// <summary>Event type → <c>List&lt;Action&lt;T&gt;&gt;</c> (boxed as object only once, at list creation).</summary>
        private readonly Dictionary<Type, object> _listenersByType;

        /// <summary>
        /// How many Publish calls are currently on the stack (publish can nest when a listener publishes).
        /// While &gt; 0, Unsubscribe must null the slot instead of removing it; compaction happens when it returns to 0.
        /// </summary>
        private int _publishDepth;

        /// <summary>True when some list has nulled slots that must be compacted once <see cref="_publishDepth"/> hits 0.</summary>
        private bool _needsCompaction;

        // ============ Construction ============

        /// <summary>Creates an empty bus. Called by <see cref="EventBusAsset"/>; also usable directly in tests.</summary>
        public EventBus()
        {
            _listenersByType = new Dictionary<Type, object>(InitialTypeCapacity);
        }

        // ============ IEventBus ============

        /// <inheritdoc />
        public void Subscribe<T>(Action<T> listener) where T : struct, IEvent
        {
            if (listener == null)
            {
                return;
            }

            List<Action<T>> listeners;
            if (_listenersByType.TryGetValue(typeof(T), out object boxed))
            {
                listeners = (List<Action<T>>)boxed;
            }
            else
            {
                listeners = new List<Action<T>>(InitialListenerCapacity);
                _listenersByType.Add(typeof(T), listeners);
            }

            if (listeners.Contains(listener))
            {
                return;
            }

            // Append: Publish only walks the count it snapshotted, so mid-publish subscribers start next publish.
            listeners.Add(listener);
        }

        /// <inheritdoc />
        public void Unsubscribe<T>(Action<T> listener) where T : struct, IEvent
        {
            if (listener == null || !_listenersByType.TryGetValue(typeof(T), out object boxed))
            {
                return;
            }

            var listeners = (List<Action<T>>)boxed;
            int index = listeners.IndexOf(listener);
            if (index < 0)
            {
                return;
            }

            if (_publishDepth > 0)
            {
                // Removing would shift the list under the running loop.
                listeners[index] = null;
                _needsCompaction = true;
            }
            else
            {
                listeners.RemoveAt(index);
            }
        }

        /// <inheritdoc />
        public void Publish<T>(in T evt) where T : struct, IEvent
        {
            if (!_listenersByType.TryGetValue(typeof(T), out object boxed))
            {
                return;
            }

            var listeners = (List<Action<T>>)boxed;
            int count = listeners.Count;

            _publishDepth++;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    Action<T> listener = listeners[i];
                    if (listener == null)
                    {
                        continue;
                    }

                    try
                    {
                        listener(evt);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }
            finally
            {
                _publishDepth--;
            }

            if (_publishDepth == 0 && _needsCompaction)
            {
                Compact(listeners);
                _needsCompaction = false;
            }
        }

        // ============ API ============

        /// <summary>Removes every listener of every event type. Called by <see cref="EventBusAsset"/> on disable.</summary>
        public void Clear()
        {
            _listenersByType.Clear();
            _needsCompaction = false;
        }

        /// <summary>Number of live listeners currently subscribed to <typeparamref name="T"/> (test hook).</summary>
        /// <typeparam name="T">Event struct type.</typeparam>
        /// <returns>Count of non-null listeners for <typeparamref name="T"/>; 0 if the type was never subscribed.</returns>
        public int GetListenerCount<T>() where T : struct, IEvent
        {
            if (!_listenersByType.TryGetValue(typeof(T), out object boxed))
            {
                return 0;
            }

            var listeners = (List<Action<T>>)boxed;
            int count = 0;
            for (int i = 0; i < listeners.Count; i++)
            {
                if (listeners[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        // ============ Helpers ============

        /// <summary>Allocation-free removal of slots nulled mid-publish, preserving order.</summary>
        private static void Compact<T>(List<Action<T>> listeners)
        {
            int write = 0;
            for (int read = 0; read < listeners.Count; read++)
            {
                if (listeners[read] != null)
                {
                    listeners[write++] = listeners[read];
                }
            }

            if (write < listeners.Count)
            {
                listeners.RemoveRange(write, listeners.Count - write);
            }
        }
    }
}
