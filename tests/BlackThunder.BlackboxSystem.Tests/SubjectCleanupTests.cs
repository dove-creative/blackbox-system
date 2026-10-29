using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    internal sealed class SubjectCleanupTests : BlackboxTestBase
    {
        private static readonly FieldInfo Cursor = typeof(SubjectStore)
            .GetField("_cleanupCursor", BindingFlags.Instance | BindingFlags.NonPublic);

        [TestCase(1, 0)]
        [TestCase(32, 0)]
        [TestCase(256, 0)]
        [TestCase(65536, 0)]
        [TestCase(256, int.MaxValue - 7)]
        public void InsertionsSweepTheWholeTableIncludingAcrossCursorOverflow(int capacity, int start)
        {
            using var session = BlackboxSession.BeginForTests(capacity);
            var store = BlackboxSession.CurrentState.Subjects;
            var living = new object();
            var canonical = store.GetOrAdd(living);
            canonical.Write("keep this log", "Test");
            var discarded = RegisterDiscarded(store);
            BlackboxTestDoubles.ForceFullCollection();
            Assert.That(Alive(discarded), Is.EqualTo(discarded.Length));

            Cursor.SetValue(store, start);
            for (int i = 0; i < 100; i++) Assert.That(store.GetOrAdd(living), Is.SameAs(canonical));
            Assert.That(Cursor.GetValue(store), Is.EqualTo(start), "Hits must not advance maintenance.");
            BlackboxTestDoubles.ForceFullCollection();
            Assert.That(Alive(discarded), Is.EqualTo(discarded.Length));

            var fresh = new object[Math.Max(1, capacity / 16)];
            for (int i = 0; i < fresh.Length; i++) store.GetOrAdd(fresh[i] = new object());
            BlackboxTestDoubles.ForceFullCollection();
            Assert.That(Alive(discarded), Is.Zero, "A completed uncontended lap must remove collected owners' entries.");
            Assert.That(store.GetOrAdd(living), Is.SameAs(canonical));
            Assert.That(canonical.GetLogs().Single().Message, Is.EqualTo("keep this log"));
            Assert.That(store.Count(), Is.EqualTo(fresh.Length + 1));
            GC.KeepAlive(living);
            GC.KeepAlive(fresh);
        }

        [Test]
        public async Task ConcurrentRegistrationsReserveOneCleanupRangePerNewSubject()
        {
            using var session = BlackboxSession.BeginForTests(256);
            var store = BlackboxSession.CurrentState.Subjects;
            var owners = Enumerable.Range(0, 64).Select(_ => new object()).ToArray();
            using var start = new Barrier(8);
            var workers = Enumerable.Range(0, 8).Select(_ => Task.Factory.StartNew(() =>
            {
                Assert.That(start.SignalAndWait(10000), Is.True);
                var boxes = new Blackbox[owners.Length];
                for (int i = 0; i < owners.Length; i++) boxes[i] = store.GetOrAdd(owners[i]);
                return boxes;
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
            var results = await Task.WhenAll(workers);
            foreach (var result in results)
                for (int i = 0; i < owners.Length; i++) Assert.That(result[i], Is.SameAs(results[0][i]));
            Assert.That(store.Count(), Is.EqualTo(owners.Length));
            Assert.That(Cursor.GetValue(store), Is.EqualTo(owners.Length * 16));
            GC.KeepAlive(owners);
        }

        [Test]
        public async Task StaleCleanupCannotOverwriteAConcurrentInsertion()
        {
            using var session = BlackboxSession.BeginForTests(32);
            var store = BlackboxSession.CurrentState.Subjects;
            var living = OwnerInBucket(0, 32);
            var canonical = store.GetOrAdd(living);
            RegisterDiscarded(store, 0, 32);
            BlackboxTestDoubles.ForceFullCollection();
            var trigger = OwnerInBucket(31, 32);
            var concurrent = OwnerInBucket(0, 32);
            using var ready = new ManualResetEventSlim();
            using var proceed = new ManualResetEventSlim();
            int attempts = 0;
            Cursor.SetValue(store, 0);
            store.BeforeCleanupPublish = bucket =>
            {
                if (bucket != 0) return;
                Interlocked.Increment(ref attempts);
                ready.Set();
                Assert.That(proceed.Wait(10000), Is.True);
            };
            var sweeping = Task.Run(() => store.GetOrAdd(trigger));
            Blackbox added = null;
            try
            {
                Assert.That(ready.Wait(10000), Is.True);
                added = store.GetOrAdd(concurrent);
            }
            finally { proceed.Set(); }
            await sweeping;
            Assert.That(attempts, Is.EqualTo(1), "A losing maintenance CAS must not retry.");
            Assert.That(store.GetOrAdd(concurrent), Is.SameAs(added));
            Assert.That(store.GetOrAdd(living), Is.SameAs(canonical));
            Assert.That(store.Count(), Is.EqualTo(3));
            GC.KeepAlive(living);
            GC.KeepAlive(trigger);
            GC.KeepAlive(concurrent);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task DelayedSweepAfterRetirementStaysInItsOriginalGeneration(bool reset)
        {
            using var session = BlackboxSession.BeginForTests(32);
            var oldState = BlackboxSession.CurrentState;
            var store = oldState.Subjects;
            RegisterDiscarded(store, 0, 32);
            BlackboxTestDoubles.ForceFullCollection();
            var owner = OwnerInBucket(31, 32);
            using var ready = new ManualResetEventSlim();
            using var proceed = new ManualResetEventSlim();
            Cursor.SetValue(store, 0);
            store.BeforeCleanupPublish = _ =>
            {
                ready.Set();
                Assert.That(proceed.Wait(10000), Is.True);
            };
            var sweeping = Task.Run(() => store.GetOrAdd(owner));
            try
            {
                Assert.That(ready.Wait(10000), Is.True);
                if (reset) BlackboxHandle.ForceReset(); else session.Dispose();
                var current = BlackboxSession.CurrentState.Subjects;
                var currentBox = current.GetOrAdd(owner);
                currentBox.Write("new generation", "Test");
                proceed.Set();
                var oldBox = await sweeping;
                Assert.That(oldBox.State, Is.SameAs(oldState));
                Assert.That(current.GetOrAdd(owner), Is.SameAs(currentBox));
                Assert.That(current.Count(), Is.EqualTo(1));
                Assert.That(currentBox.GetLogs().Single().Message, Is.EqualTo("new generation"));
            }
            finally { proceed.Set(); await sweeping; }
            GC.KeepAlive(owner);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference[] RegisterDiscarded(SubjectStore store, int bucket = -1, int capacity = 0)
        {
            var owners = new object[64];
            var boxes = new WeakReference[owners.Length];
            for (int i = 0; i < owners.Length; i++)
            {
                owners[i] = bucket < 0 ? new object() : OwnerInBucket(bucket, capacity);
                var box = store.GetOrAdd(owners[i]);
                box.Write("discarded", "Test");
                boxes[i] = new WeakReference(box);
            }
            GC.KeepAlive(owners);
            return boxes;
        }

        private static object OwnerInBucket(int bucket, int capacity)
        {
            for (int i = 0; i < 1_000_000; i++)
            {
                var owner = new object();
                if ((RuntimeHelpers.GetHashCode(owner) & (capacity - 1)) == bucket) return owner;
            }
            throw new InvalidOperationException("Could not find an owner for the collision test.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Alive(WeakReference[] boxes)
        {
            int count = 0;
            foreach (var box in boxes) if (box.IsAlive) count++;
            return count;
        }
    }
}
