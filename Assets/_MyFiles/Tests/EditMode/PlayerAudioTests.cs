using System.Reflection;
using Brawler.Combat;
using Brawler.Core;
using Brawler.Player;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Brawler.Tests
{
    public sealed class PlayerAudioTests
    {
        private EventBusAsset _asset;
        private EventBus _bus;
        private GameObject _gameObject;
        private PlayerAudio _audio;

        [SetUp]
        public void SetUp()
        {
            _asset = ScriptableObject.CreateInstance<EventBusAsset>();
            _bus = (EventBus)typeof(EventBusAsset).GetProperty("Bus", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_asset);
            _gameObject = new GameObject("PlayerAudioTests");
            _audio = _gameObject.AddComponent<PlayerAudio>();
            Invoke(_audio, "Awake");
            typeof(PlayerAudio).GetField("_eventBus", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_audio, _asset);
            Invoke(_audio, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameObject);
            Object.DestroyImmediate(_asset);
        }

        [Test]
        public void EventListeners_AreRemovedWhenDisabled()
        {
            Assert.AreEqual(1, _bus.GetListenerCount<HitEvent>());
            Assert.AreEqual(1, _bus.GetListenerCount<ParryEvent>());

            Invoke(_audio, "OnDisable");

            Assert.AreEqual(0, _bus.GetListenerCount<HitEvent>());
            Assert.AreEqual(0, _bus.GetListenerCount<ParryEvent>());
        }

        private static void Invoke(PlayerAudio audio, string method)
        {
            typeof(PlayerAudio).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(audio, null);
        }
    }
}
