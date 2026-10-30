using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    internal sealed class SubjectLookupTests : BlackboxTestBase
    {
        [Test]
        public void DefaultAndExplicitSessionsPreserveTheirBucketCountsAfterDefaultRestart()
        {
            Assert.That(BucketCount(), Is.EqualTo(65536));
            BlackboxHandle.RestartDefaultSession();
            Assert.That(BucketCount(), Is.EqualTo(65536));
            using (BlackboxHandle.BeginSession())
            {
                Assert.That(BucketCount(), Is.EqualTo(256));
                BlackboxHandle.RestartDefaultSession();
                Assert.That(BucketCount(), Is.EqualTo(256));
            }
            Assert.That(BucketCount(), Is.EqualTo(65536));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RegisteredHitsRemainCanonicalAndAllocationFree(bool explicitSession)
        {
            using var session = explicitSession ? BlackboxHandle.BeginSession() : null;
            var store = BlackboxSessionRegistry.Current.Subjects;
            var owners = new object[20000];
            var boxes = new Blackbox[owners.Length];
            for (int i = 0; i < owners.Length; i++)
            {
                owners[i] = new object();
                boxes[i] = store.GetOrAdd(owners[i]);
            }
            for (int i = 0; i < 1000; i++) store.GetOrAdd(owners[i]);
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100000; i++)
            {
                int index = (i * 7919) % owners.Length;
                if (!ReferenceEquals(store.GetOrAdd(owners[index]), boxes[index]))
                    throw new InvalidOperationException("A registered subject lost its canonical Blackbox.");
            }
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Assert.That(allocated, Is.Zero);
            Assert.That(store.Count(), Is.EqualTo(owners.Length));
            GC.KeepAlive(owners);
        }

        [Test]
        public void HitKeepsDeadEntriesWhileMissPrunesThemWithoutReplacingLiveBox()
        {
            using var session = BlackboxSessionRegistry.Begin(1);
            var store = BlackboxSessionRegistry.Current.Subjects;
            var living = new object();
            var canonical = store.GetOrAdd(living);
            var deadBoxes = RegisterDiscardedSubjects(store);
            BlackboxTestDoubles.ForceFullCollection();
            Assert.That(store.Count(), Is.EqualTo(1));
            Assert.That(store.GetOrAdd(living), Is.SameAs(canonical));
            BlackboxTestDoubles.ForceFullCollection();
            Assert.That(CountAlive(deadBoxes), Is.EqualTo(deadBoxes.Length));

            var fresh = new object();
            store.GetOrAdd(fresh);
            BlackboxTestDoubles.ForceFullCollection();
            Assert.That(CountAlive(deadBoxes), Is.Zero);
            Assert.That(store.GetOrAdd(living), Is.SameAs(canonical));
            Assert.That(store.Count(), Is.EqualTo(2));
            GC.KeepAlive(living);
            GC.KeepAlive(fresh);
        }

        private static int BucketCount() => ((Array)typeof(SubjectStore)
            .GetField("_buckets", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(BlackboxSessionRegistry.Current.Subjects)).Length;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference[] RegisterDiscardedSubjects(SubjectStore store)
        {
            var boxes = new WeakReference[64];
            var owners = new object[boxes.Length];
            for (int i = 0; i < boxes.Length; i++)
            {
                owners[i] = new object();
                boxes[i] = new WeakReference(store.GetOrAdd(owners[i]));
            }
            GC.KeepAlive(owners);
            return boxes;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int CountAlive(WeakReference[] boxes)
        {
            int count = 0;
            foreach (var box in boxes) if (box.IsAlive) count++;
            return count;
        }
    }
}
