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
        private int _busSession = -1;

        // Bumped on every Play Mode entry. With domain reload off, SO OnEnable/OnDisable don't run then,
        // so this is what drops listeners left over from the previous session.
        private static int s_session;

        /// <summary>The underlying bus, recreated lazily at the start of each play session.</summary>
        private EventBus Bus
        {
            get
            {
                if (_bus == null || _busSession != s_session)
                {
                    _bus = new EventBus();
                    _busSession = s_session;
                }

                return _bus;
            }
        }

        // ============ Lifecycle ============

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_session++;
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
