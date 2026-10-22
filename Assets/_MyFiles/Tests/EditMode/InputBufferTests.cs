using Brawler.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Brawler.Tests
{
    /// <summary>
    /// Expected behavior of <see cref="InputBuffer"/>: window boundaries (inclusive), oldest-first consumption,
    /// one-entry-per-consume, command isolation, capacity overflow and Clear.
    /// Times use exactly representable floats (1.0, 1.25, ...) so boundary checks are deterministic.
    /// </summary>
    public sealed class InputBufferTests
    {
        // ============ Constants ============

        private const float Window = 0.25f;
        private const int Capacity = 4;

        // ============ Fixture ============

        private InputBuffer _buffer;

        [SetUp]
        public void SetUp()
        {
            _buffer = new InputBuffer(Window, Capacity);
        }

        // ============ Construction ============

        [Test]
        public void Constructor_SetsWindowAndCapacity()
        {
            Assert.AreEqual(Window, _buffer.WindowSeconds);
            Assert.AreEqual(Capacity, _buffer.Capacity);
        }

        [Test]
        public void Constructor_ClampsCapacityToAtLeastOne()
        {
            var buffer = new InputBuffer(Window, 0);

            Assert.AreEqual(1, buffer.Capacity);
        }

        [Test]
        public void NewBuffer_IsEmpty()
        {
            Assert.AreEqual(0, _buffer.Count);
            Assert.IsFalse(_buffer.Peek(BufferedCommand.Attack, 0f));
            Assert.IsFalse(_buffer.TryConsume(BufferedCommand.Attack, 0f));
        }

        [Test]
        public void Constructor_FromInputBufferSettingsDefaults_UsesSettingsValues()
        {
            var settings = ScriptableObject.CreateInstance<InputBufferSettings>();
            try
            {
                var buffer = new InputBuffer(settings.BufferWindowSeconds, settings.Capacity);

                Assert.AreEqual(settings.BufferWindowSeconds, buffer.WindowSeconds);
                Assert.AreEqual(settings.Capacity, buffer.Capacity);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        // ============ Enqueue ============

        [Test]
        public void Enqueue_IncrementsCount()
        {
            _buffer.Enqueue(BufferedCommand.Attack, 1f);
            _buffer.Enqueue(BufferedCommand.Dash, 1f);

            Assert.AreEqual(2, _buffer.Count);
        }

        [Test]
        public void Enqueue_None_IsIgnored()
        {
            _buffer.Enqueue(BufferedCommand.None, 1f);

            Assert.AreEqual(0, _buffer.Count);
            Assert.IsFalse(_buffer.Peek(BufferedCommand.None, 1f));
        }

        [Test]
        public void Enqueue_WhenFull_CountStaysAtCapacity()
        {
            for (int i = 0; i < Capacity + 3; i++)
            {
                _buffer.Enqueue(BufferedCommand.Attack, 1f);
            }

            Assert.AreEqual(Capacity, _buffer.Count);
        }

        [Test]
        public void Enqueue_WhenFull_OverwritesOldestEntry()
        {
            _buffer.Enqueue(BufferedCommand.Dash, 1f); // oldest — should be dropped
            for (int i = 0; i < Capacity; i++)
            {
                _buffer.Enqueue(BufferedCommand.Attack, 1f);
            }

            Assert.IsFalse(_buffer.Peek(BufferedCommand.Dash, 1f), "The oldest entry (Dash) was overwritten.");
            Assert.IsTrue(_buffer.Peek(BufferedCommand.Attack, 1f));
        }

        [Test]
        public void EnqueueLatest_KeepsOnePressPerCommand()
        {
            _buffer.EnqueueLatest(BufferedCommand.Attack, 1f);
            _buffer.Enqueue(BufferedCommand.Dash, 1.05f);
            _buffer.EnqueueLatest(BufferedCommand.Attack, 1.1f);
            _buffer.EnqueueLatest(BufferedCommand.Attack, 1.2f);

            Assert.AreEqual(2, _buffer.Count);
            Assert.IsTrue(_buffer.Peek(BufferedCommand.Dash, 1.2f), "Other commands are kept.");
            Assert.IsTrue(_buffer.TryConsume(BufferedCommand.Attack, 1.4f), "The newest press is the one kept.");
            Assert.IsFalse(_buffer.TryConsume(BufferedCommand.Attack, 1.4f));
        }

        // ============ TryConsume: window ============

        [Test]
        public void TryConsume_ReturnsTrue_WhenPressIsWithinWindow()
        {
            _buffer.Enqueue(BufferedCommand.Attack, 1f);

            Assert.IsTrue(_buffer.TryConsume(BufferedCommand.Attack, 1.125f));
        }

        [Test]
        public void TryConsume_ReturnsTrue_OnSameTimeAsPress()
        {
            _buffer.Enqueue(BufferedCommand.Attack, 1f);

            Assert.IsTrue(_buffer.TryConsume(BufferedCommand.Attack, 1f));
        }

        [Test]
        public void TryConsume_ReturnsTrue_WhenPressIsExactlyWindowOld()
        {
            _buffer.Enqueue(BufferedCommand.Attack, 1f);

            Assert.IsTrue(_buffer.TryConsume(BufferedCommand.Attack, 1.25f), "Window boundary is inclusive.");
        }

        [Test]
        public void TryConsume_ReturnsFalse_WhenPressIsOlderThanWindow()
        {
            _buffer.Enqueue(BufferedCommand.Attack, 1f);

            Assert.IsFalse(_buffer.TryConsume(BufferedCommand.Attack, 1.5f));
        }

        [Test]
        public void TryConsume_ReturnsTrue_ForFutureStampedPress()
        {
            _buffer.Enqueue(BufferedCommand.Attack, 2f);

            Assert.IsTrue(_buffer.TryConsume(BufferedCommand.Attack, 1f), "time > now counts as live.");
        }

        [Test]
        public void TryConsume_ReturnsFalse_ForNone()
        {
            _buffer.Enqueue(BufferedCommand.Attack, 1f);

            Assert.IsFalse(_buffer.TryConsume(BufferedCommand.None, 1f));
            Assert.AreEqual(1, _buffer.Count);
        }

        [Test]
        public void WindowSeconds_Setter_ChangesWhatIsLive()
        {
            _buffer.Enqueue(BufferedCommand.Attack, 1f);

            _buffer.WindowSeconds = 1f;

            Assert.AreEqual(1f, _buffer.WindowSeconds);
            Assert.IsTrue(_buffer.TryConsume(BufferedCommand.Attack, 1.5f), "0.5s old press is live with a 1s window.");
        }

        // ============ TryConsume: removal semantics ============

        [Test]
        public void TryConsume_RemovesOnlyOneMatchingEntry()
        {
            _buffer.Enqueue(BufferedCommand.Attack, 1f);
            _buffer.Enqueue(BufferedCommand.Attack, 1f);

            Assert.IsTrue(_buffer.TryConsume(BufferedCommand.Attack, 1f));
            Assert.AreEqual(1, _buffer.Count);
            Assert.IsTrue(_buffer.TryConsume(BufferedCommand.Attack, 1f));
            Assert.IsFalse(_buffer.TryConsume(BufferedCommand.Attack, 1f));
        }

        [Test]
        public void TryConsume_SameEntry_CannotBeConsumedTwice()
        {
            _buffer.Enqueue(BufferedCommand.Dash, 1f);

            Assert.IsTrue(_buffer.TryConsume(BufferedCommand.Dash, 1f));
            Assert.IsFalse(_buffer.TryConsume(BufferedCommand.Dash, 1f));
        }

        [Test]
        public void TryConsume_ConsumesOldestMatchingEntryFirst()
        {
            _buffer.Enqueue(BufferedCommand.Attack, 1f);     // older
            _buffer.Enqueue(BufferedCommand.Attack, 1.125f); // newer
            Assert.IsTrue(_buffer.TryConsume(BufferedCommand.Attack, 1.125f));

            // At 1.375 the older press (0.375 old) would be dead, the newer one (0.25 old) is still live.
            // If the OLDEST was consumed above, the survivor is the newer one → true.
            Assert.IsTrue(_buffer.TryConsume(BufferedCommand.Attack, 1.375f));
        }

        [Test]
        public void TryConsume_SkipsExpiredEntries_AndConsumesLiveMatch()
        {
            _buffer.Enqueue(BufferedCommand.Attack, 0f); // long expired
            _buffer.Enqueue(BufferedCommand.Attack, 1f); // live

            Assert.IsTrue(_buffer.TryConsume(BufferedCommand.Attack, 1.125f));
        }

        [Test]
        public void TryConsume_ReturnsFalse_ForDifferentCommand_AndLeavesItBuffered()
        {
            _buffer.Enqueue(BufferedCommand.Dash, 1f);

            Assert.IsFalse(_buffer.TryConsume(BufferedCommand.Attack, 1f));
            Assert.AreEqual(1, _buffer.Count);
            Assert.IsTrue(_buffer.Peek(BufferedCommand.Dash, 1f));
        }

        [Test]
        public void TryConsume_LeavesOtherCommandsUntouched()
        {
            _buffer.Enqueue(BufferedCommand.Attack, 1f);
            _buffer.Enqueue(BufferedCommand.Dash, 1f);
            _buffer.Enqueue(BufferedCommand.Attack, 1f);

            Assert.IsTrue(_buffer.TryConsume(BufferedCommand.Dash, 1f));

            Assert.AreEqual(2, _buffer.Count);
            Assert.IsFalse(_buffer.Peek(BufferedCommand.Dash, 1f));
            Assert.IsTrue(_buffer.TryConsume(BufferedCommand.Attack, 1f));
            Assert.IsTrue(_buffer.TryConsume(BufferedCommand.Attack, 1f));
        }

        [Test]
        public void EnqueueAndConsume_ManyTimesPastCapacity_KeepsWorking()
        {
            // Exercises ring wrap-around of head/tail indices.
            for (int i = 0; i < Capacity * 5; i++)
            {
                float t = i;
                _buffer.Enqueue(BufferedCommand.Attack, t);
                _buffer.Enqueue(BufferedCommand.Dash, t);
                Assert.IsTrue(_buffer.TryConsume(BufferedCommand.Dash, t), "iteration " + i);
                Assert.IsTrue(_buffer.TryConsume(BufferedCommand.Attack, t), "iteration " + i);
                Assert.AreEqual(0, _buffer.Count, "iteration " + i);
            }
        }

        // ============ Peek ============

        [Test]
        public void Peek_ReturnsTrue_ForLiveEntry_WithoutRemovingIt()
        {
            _buffer.Enqueue(BufferedCommand.Attack, 1f);

            Assert.IsTrue(_buffer.Peek(BufferedCommand.Attack, 1.125f));
            Assert.AreEqual(1, _buffer.Count);
            Assert.IsTrue(_buffer.TryConsume(BufferedCommand.Attack, 1.125f), "Peek must not consume.");
        }

        [Test]
        public void Peek_ReturnsTrue_AtExactWindowBoundary()
        {
            _buffer.Enqueue(BufferedCommand.Dash, 1f);

            Assert.IsTrue(_buffer.Peek(BufferedCommand.Dash, 1.25f));
        }

        [Test]
        public void Peek_ReturnsFalse_WhenEntryExpired()
        {
            _buffer.Enqueue(BufferedCommand.Attack, 1f);

            Assert.IsFalse(_buffer.Peek(BufferedCommand.Attack, 2f));
        }

        [Test]
        public void Peek_ReturnsFalse_ForDifferentCommand()
        {
            _buffer.Enqueue(BufferedCommand.Attack, 1f);

            Assert.IsFalse(_buffer.Peek(BufferedCommand.Dash, 1f));
        }

        // ============ Clear ============

        [Test]
        public void Clear_RemovesAllEntries()
        {
            _buffer.Enqueue(BufferedCommand.Attack, 1f);
            _buffer.Enqueue(BufferedCommand.Dash, 1f);

            _buffer.Clear();

            Assert.AreEqual(0, _buffer.Count);
            Assert.IsFalse(_buffer.Peek(BufferedCommand.Attack, 1f));
            Assert.IsFalse(_buffer.TryConsume(BufferedCommand.Dash, 1f));
        }

        [Test]
        public void Clear_DoesNotChangeCapacityOrWindow()
        {
            _buffer.Clear();

            Assert.AreEqual(Capacity, _buffer.Capacity);
            Assert.AreEqual(Window, _buffer.WindowSeconds);
        }

        [Test]
        public void Enqueue_AfterClear_Works()
        {
            _buffer.Enqueue(BufferedCommand.Attack, 1f);
            _buffer.Clear();

            _buffer.Enqueue(BufferedCommand.Dash, 2f);

            Assert.AreEqual(1, _buffer.Count);
            Assert.IsTrue(_buffer.TryConsume(BufferedCommand.Dash, 2f));
        }
    }
}
