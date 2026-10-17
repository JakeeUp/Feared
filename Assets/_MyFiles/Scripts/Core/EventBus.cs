// Most private fields aren't touched until the TODOs below are filled in. Drop this pragma once they are.
#pragma warning disable CS0169, CS0649, CS0414

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
            // TODO: get-or-create the List<Action<T>> for typeof(T) and append the listener.
            // - Ignore null and duplicates. Delegate equality compares target + method, so this only works if callers
            //   cache their handler in a field (a fresh lambda every OnEnable would never match on Unsubscribe).
            // - Append, don't insert - that's what gives "subscribed mid-publish -> first called on the next publish",
            //   since Publish only walks the count it saw when it started.
        }

        /// <inheritdoc />
        public void Unsubscribe<T>(Action<T> listener) where T : struct, IEvent
        {
            // TODO: remove the listener, keep everyone else's order.
            // - Unknown type/listener is a no-op, never an exception (OnDisable order is unpredictable on scene unload).
            // - Inside a Publish (_publishDepth > 0) don't RemoveAt - it shifts the list under the loop and someone gets
            //   skipped or called twice. Null the slot and flag it for compaction instead.
        }

        /// <inheritdoc />
        public void Publish<T>(in T evt) where T : struct, IEvent
        {
            // TODO: call every live listener for T in subscription order.
            // - No list for T -> return. This path can't allocate, it runs on every hit.
            // - Snapshot Count before the loop and use a plain for (foreach throws if a listener unsubscribes); skip nulls.
            // - try/catch + Debug.LogException around each call so one broken listener doesn't eat the rest.
            // - Bump _publishDepth around the loop (decrement in a finally) so a listener publishing something else is
            //   safe; compact once the outermost publish unwinds.
            // Ref: Nystrom, Game Programming Patterns, "Observer" - the bit on removing observers mid-notify is exactly this.
        }

        // ============ API ============

        /// <summary>Removes every listener of every event type. Called by <see cref="EventBusAsset"/> on disable.</summary>
        public void Clear()
        {
            // TODO: drop everything (clearing the dictionary is enough - an in-flight Publish holds its own list ref
            // and just finishes). Reset _needsCompaction too.
            // Matters more than it looks: Enter Play Mode Options has domain reload OFF in this project, so the bus asset
            // survives between play sessions and would otherwise keep last run's listeners around.
        }

        /// <summary>Number of live listeners currently subscribed to <typeparamref name="T"/> (test hook).</summary>
        /// <typeparam name="T">Event struct type.</typeparam>
        /// <returns>Count of non-null listeners for <typeparamref name="T"/>; 0 if the type was never subscribed.</returns>
        public int GetListenerCount<T>() where T : struct, IEvent
        {
            // TODO: count non-null entries only (slots nulled mid-publish shouldn't count). 0 if T was never seen.
            return 0;
        }

        // ============ Helpers ============

        // TODO: compaction for the nulled slots.
        // List.RemoveAll(predicate) is the obvious one-liner but allocates a delegate per call. A read/write cursor pass
        // followed by RemoveRange on the tail is allocation-free. Easiest spot is the end of Publish<T> where T is known;
        // stray nulls left in other types' lists are harmless and get cleaned on that type's next publish.
    }
}
