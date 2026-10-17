using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Brawler.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Brawler.Tests
{
    /// <summary>
    /// Expected behavior of <see cref="EventBus"/> (and its <see cref="EventBusAsset"/> handle): typed
    /// publish/subscribe, ordering, duplicate handling, isolation between event types, and safety when the
    /// listener list changes during a publish.
    /// </summary>
    public sealed class EventBusTests
    {
        // ============ Test events ============

        private readonly struct TestEvent : IEvent
        {
            public readonly int Value;

            public TestEvent(int value)
            {
                Value = value;
            }
        }

        private readonly struct OtherEvent : IEvent
        {
            public readonly float Amount;

            public OtherEvent(float amount)
            {
                Amount = amount;
            }
        }

        // ============ Fixture ============

        private EventBus _bus;
        private List<string> _log;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _log = new List<string>();
        }

        // ============ Basics ============

        [Test]
        public void EventBus_ImplementsIEventBus()
        {
            Assert.IsInstanceOf<IEventBus>(_bus);
        }

        [Test]
        public void Publish_InvokesSubscribedListener_WithPayload()
        {
            int received = 0;
            Action<TestEvent> listener = e => received = e.Value;
            _bus.Subscribe(listener);

            _bus.Publish(new TestEvent(42));

            Assert.AreEqual(42, received);
        }

        [Test]
        public void Publish_WithNoListeners_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _bus.Publish(new TestEvent(1)));
        }

        [Test]
        public void Publish_InvokesListeners_InSubscriptionOrder()
        {
            _bus.Subscribe<TestEvent>(e => _log.Add("first"));
            _bus.Subscribe<TestEvent>(e => _log.Add("second"));
            _bus.Subscribe<TestEvent>(e => _log.Add("third"));

            _bus.Publish(new TestEvent(0));

            CollectionAssert.AreEqual(new[] { "first", "second", "third" }, _log);
        }

        [Test]
        public void Publish_EachCall_InvokesListenerAgain()
        {
            int calls = 0;
            _bus.Subscribe<TestEvent>(e => calls++);

            _bus.Publish(new TestEvent(0));
            _bus.Publish(new TestEvent(0));

            Assert.AreEqual(2, calls);
        }

        // ============ Subscribe ============

        [Test]
        public void Subscribe_SameListenerTwice_IsInvokedOnce()
        {
            int calls = 0;
            Action<TestEvent> listener = e => calls++;
            _bus.Subscribe(listener);
            _bus.Subscribe(listener);

            _bus.Publish(new TestEvent(0));

            Assert.AreEqual(1, calls);
            Assert.AreEqual(1, _bus.GetListenerCount<TestEvent>());
        }

        [Test]
        public void Subscribe_Null_IsIgnored()
        {
            Assert.DoesNotThrow(() => _bus.Subscribe<TestEvent>(null));
            Assert.AreEqual(0, _bus.GetListenerCount<TestEvent>());
            Assert.DoesNotThrow(() => _bus.Publish(new TestEvent(0)));
        }

        [Test]
        public void GetListenerCount_ReturnsZero_ForNeverSubscribedType()
        {
            Assert.AreEqual(0, _bus.GetListenerCount<TestEvent>());
        }

        [Test]
        public void GetListenerCount_CountsDistinctListeners()
        {
            _bus.Subscribe<TestEvent>(e => { });
            _bus.Subscribe<TestEvent>(e => { });

            Assert.AreEqual(2, _bus.GetListenerCount<TestEvent>());
        }

        // ============ Unsubscribe ============

        [Test]
        public void Unsubscribe_StopsFurtherInvocations()
        {
            int calls = 0;
            Action<TestEvent> listener = e => calls++;
            _bus.Subscribe(listener);
            _bus.Publish(new TestEvent(0));

            _bus.Unsubscribe(listener);
            _bus.Publish(new TestEvent(0));

            Assert.AreEqual(1, calls);
            Assert.AreEqual(0, _bus.GetListenerCount<TestEvent>());
        }

        [Test]
        public void Unsubscribe_OnlyRemovesThatListener_AndKeepsOrder()
        {
            Action<TestEvent> a = e => _log.Add("a");
            Action<TestEvent> b = e => _log.Add("b");
            Action<TestEvent> c = e => _log.Add("c");
            _bus.Subscribe(a);
            _bus.Subscribe(b);
            _bus.Subscribe(c);

            _bus.Unsubscribe(b);
            _bus.Publish(new TestEvent(0));

            CollectionAssert.AreEqual(new[] { "a", "c" }, _log);
        }

        [Test]
        public void Unsubscribe_UnknownListener_DoesNotThrow()
        {
            _bus.Subscribe<TestEvent>(e => { });

            Assert.DoesNotThrow(() => _bus.Unsubscribe<TestEvent>(e => { }));
            Assert.AreEqual(1, _bus.GetListenerCount<TestEvent>());
        }

        [Test]
        public void Unsubscribe_ForNeverSubscribedType_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _bus.Unsubscribe<OtherEvent>(e => { }));
        }

        [Test]
        public void Unsubscribe_Null_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _bus.Unsubscribe<TestEvent>(null));
        }

        [Test]
        public void Resubscribe_AfterUnsubscribe_IsInvokedAgain()
        {
            int calls = 0;
            Action<TestEvent> listener = e => calls++;
            _bus.Subscribe(listener);
            _bus.Unsubscribe(listener);
            _bus.Subscribe(listener);

            _bus.Publish(new TestEvent(0));

            Assert.AreEqual(1, calls);
        }

        // ============ Type isolation ============

        [Test]
        public void Publish_OfOneType_DoesNotInvokeListenersOfAnotherType()
        {
            int testCalls = 0;
            int otherCalls = 0;
            _bus.Subscribe<TestEvent>(e => testCalls++);
            _bus.Subscribe<OtherEvent>(e => otherCalls++);

            _bus.Publish(new TestEvent(0));

            Assert.AreEqual(1, testCalls);
            Assert.AreEqual(0, otherCalls);
        }

        [Test]
        public void GetListenerCount_IsTrackedPerType()
        {
            _bus.Subscribe<TestEvent>(e => { });
            _bus.Subscribe<TestEvent>(e => { });
            _bus.Subscribe<OtherEvent>(e => { });

            Assert.AreEqual(2, _bus.GetListenerCount<TestEvent>());
            Assert.AreEqual(1, _bus.GetListenerCount<OtherEvent>());
        }

        [Test]
        public void Unsubscribe_FromOneType_DoesNotAffectAnotherType()
        {
            Action<TestEvent> testListener = e => _log.Add("test");
            _bus.Subscribe(testListener);
            _bus.Subscribe<OtherEvent>(e => _log.Add("other"));

            _bus.Unsubscribe(testListener);
            _bus.Publish(new OtherEvent(1f));

            CollectionAssert.AreEqual(new[] { "other" }, _log);
        }

        // ============ Mutation during publish ============

        [Test]
        public void UnsubscribeDuringPublish_LaterListenerIsNotInvokedInThatPublish()
        {
            Action<TestEvent> second = e => _log.Add("second");
            Action<TestEvent> first = e =>
            {
                _log.Add("first");
                _bus.Unsubscribe(second);
            };
            _bus.Subscribe(first);
            _bus.Subscribe(second);

            _bus.Publish(new TestEvent(0));

            CollectionAssert.AreEqual(new[] { "first" }, _log);
            Assert.AreEqual(1, _bus.GetListenerCount<TestEvent>());
        }

        [Test]
        public void UnsubscribeSelfDuringPublish_OtherListenersStillInvoked()
        {
            Action<TestEvent> self = null;
            self = e =>
            {
                _log.Add("self");
                _bus.Unsubscribe(self);
            };
            _bus.Subscribe(self);
            _bus.Subscribe<TestEvent>(e => _log.Add("after"));

            _bus.Publish(new TestEvent(0));
            _bus.Publish(new TestEvent(0));

            CollectionAssert.AreEqual(new[] { "self", "after", "after" }, _log,
                "Removing yourself must not skip the next listener, and you're gone on the next publish.");
        }

        [Test]
        public void UnsubscribeEarlierListenerDuringPublish_DoesNotSkipOrRepeatOthers()
        {
            Action<TestEvent> a = e => _log.Add("a");
            Action<TestEvent> b = e =>
            {
                _log.Add("b");
                _bus.Unsubscribe(a);
            };
            Action<TestEvent> c = e => _log.Add("c");
            _bus.Subscribe(a);
            _bus.Subscribe(b);
            _bus.Subscribe(c);

            _bus.Publish(new TestEvent(0));
            _log.Add("|");
            _bus.Publish(new TestEvent(0));

            CollectionAssert.AreEqual(new[] { "a", "b", "c", "|", "b", "c" }, _log);
        }

        [Test]
        public void SubscribeDuringPublish_IsInvokedOnNextPublishOnly()
        {
            Action<TestEvent> late = e => _log.Add("late");
            bool added = false;
            _bus.Subscribe<TestEvent>(e =>
            {
                _log.Add("early");
                if (!added)
                {
                    added = true;
                    _bus.Subscribe(late);
                }
            });

            _bus.Publish(new TestEvent(0));
            _log.Add("|");
            _bus.Publish(new TestEvent(0));

            CollectionAssert.AreEqual(new[] { "early", "|", "early", "late" }, _log);
        }

        [Test]
        public void NestedPublish_OfAnotherType_InvokesBothSetsOfListeners()
        {
            _bus.Subscribe<TestEvent>(e =>
            {
                _log.Add("test");
                _bus.Publish(new OtherEvent(1f));
            });
            _bus.Subscribe<OtherEvent>(e => _log.Add("other"));

            _bus.Publish(new TestEvent(0));

            CollectionAssert.AreEqual(new[] { "test", "other" }, _log);
        }

        [Test]
        public void ThrowingListener_IsLogged_AndRemainingListenersStillRun()
        {
            _bus.Subscribe<TestEvent>(e => throw new InvalidOperationException("boom"));
            _bus.Subscribe<TestEvent>(e => _log.Add("survivor"));
            LogAssert.Expect(LogType.Exception, new Regex("boom"));

            _bus.Publish(new TestEvent(0));

            CollectionAssert.AreEqual(new[] { "survivor" }, _log);
        }

        // ============ Clear ============

        [Test]
        public void Clear_RemovesAllListenersOfAllTypes()
        {
            _bus.Subscribe<TestEvent>(e => _log.Add("test"));
            _bus.Subscribe<OtherEvent>(e => _log.Add("other"));

            _bus.Clear();
            _bus.Publish(new TestEvent(0));
            _bus.Publish(new OtherEvent(0f));

            CollectionAssert.IsEmpty(_log);
            Assert.AreEqual(0, _bus.GetListenerCount<TestEvent>());
            Assert.AreEqual(0, _bus.GetListenerCount<OtherEvent>());
        }

        [Test]
        public void Subscribe_AfterClear_Works()
        {
            _bus.Subscribe<TestEvent>(e => { });
            _bus.Clear();
            _bus.Subscribe<TestEvent>(e => _log.Add("fresh"));

            _bus.Publish(new TestEvent(0));

            CollectionAssert.AreEqual(new[] { "fresh" }, _log);
        }

        // ============ EventBusAsset (ScriptableObject handle) ============

        [Test]
        public void EventBusAsset_ForwardsSubscribeAndPublish()
        {
            var asset = ScriptableObject.CreateInstance<EventBusAsset>();
            try
            {
                int received = 0;
                Action<TestEvent> listener = e => received = e.Value;
                IEventBus bus = asset;

                bus.Subscribe(listener);
                bus.Publish(new TestEvent(7));
                bus.Unsubscribe(listener);
                bus.Publish(new TestEvent(99));

                Assert.AreEqual(7, received);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}
