using System;

namespace Brawler.Core
{
    /// <summary>
    /// Instance-based, strongly typed publish/subscribe bus. No strings, no statics, no boxing.
    /// The shared runtime instance is distributed through the <see cref="EventBusAsset"/> ScriptableObject.
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Registers <paramref name="listener"/> for events of type <typeparamref name="T"/>.
        /// Subscribing the same delegate twice is a no-op. Cache the delegate in a field so the
        /// matching <see cref="Unsubscribe{T}"/> call does not allocate.
        /// </summary>
        /// <typeparam name="T">Event struct type.</typeparam>
        /// <param name="listener">Callback; ignored if null.</param>
        void Subscribe<T>(Action<T> listener) where T : struct, IEvent;

        /// <summary>
        /// Removes <paramref name="listener"/>. Safe to call during <see cref="Publish{T}"/>; a listener removed
        /// mid-publish is not invoked for the remainder of that publish. Unknown listeners are ignored.
        /// </summary>
        /// <typeparam name="T">Event struct type.</typeparam>
        /// <param name="listener">Callback previously passed to <see cref="Subscribe{T}"/>.</param>
        void Unsubscribe<T>(Action<T> listener) where T : struct, IEvent;

        /// <summary>
        /// Synchronously invokes every listener of <typeparamref name="T"/> in subscription order.
        /// Listeners added during a publish are first invoked on the next publish. A throwing listener
        /// is logged and does not stop the others.
        /// </summary>
        /// <typeparam name="T">Event struct type.</typeparam>
        /// <param name="evt">Event payload (passed by readonly reference to avoid copies).</param>
        void Publish<T>(in T evt) where T : struct, IEvent;
    }
}
