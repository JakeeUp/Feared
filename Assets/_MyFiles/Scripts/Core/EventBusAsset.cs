using System;
using UnityEngine;

namespace Brawler.Core
{
    /// <summary>
    /// ScriptableObject handle to the shared runtime <see cref="EventBus"/>. Components serialize a
    /// reference to this asset (<c>Data/Core/EventBus.asset</c>) and use it as an <see cref="IEventBus"/>;
    /// there is no static/global bus. Forwarding here is wiring, not gameplay, so it is implemented.
    /// </summary>
    [CreateAssetMenu(menuName = "Brawler/Core/Event Bus", fileName = "EventBus")]
    public sealed class EventBusAsset : ScriptableObject, IEventBus
    {
        // ============ State ============

        private EventBus _bus;

        /// <summary>The underlying bus, created lazily so it also works if OnEnable has not run yet.</summary>
        private EventBus Bus => _bus ??= new EventBus();

        // ============ Lifecycle ============

        private void OnEnable()
        {
            _bus = new EventBus();
        }

        private void OnDisable()
        {
            // Drop listeners from the previous play session (matters when domain reload is disabled).
            _bus?.Clear();
        }

        // ============ IEventBus ============

        /// <inheritdoc />
        public void Subscribe<T>(Action<T> listener) where T : struct, IEvent
        {
            Bus.Subscribe(listener);
        }

        /// <inheritdoc />
        public void Unsubscribe<T>(Action<T> listener) where T : struct, IEvent
        {
            Bus.Unsubscribe(listener);
        }

        /// <inheritdoc />
        public void Publish<T>(in T evt) where T : struct, IEvent
        {
            Bus.Publish(in evt);
        }
    }
}
