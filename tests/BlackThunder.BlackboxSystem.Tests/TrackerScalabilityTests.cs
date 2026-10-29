using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    internal sealed class TrackerScalabilityTests : BlackboxTestBase
    {
        [Test]
        public void DefaultSessionReusesTrackersAcrossThousandsOfContexts()
        {
            Assert.That(BlackboxSession.CurrentKey, Is.EqualTo(BlackboxSession.DefaultKey));
            BlackboxHandle.MaxLogCount = 1;
            var initial = CountTrackersUsedByContexts(1);
            Assert.That(initial, Is.EqualTo(2));
            Assert.That(CountTrackersUsedByContexts(20000), Is.EqualTo(initial));
        }

        [Test]
        public void DefaultSessionTrackerCountIsBoundedAcrossManyThreads()
        {
            BlackboxHandle.MaxLogCount = 1;
            var state = BlackboxSession.CurrentState;
            var seen = new ConcurrentDictionary<object, byte>();
            RunThreads(48, () =>
            {
                var owner = new object();
                var context = state.Subjects.GetOrAdd(owner).CurrentContext;
                context.EnqueueLog("one", -1, "Run");
                seen.TryAdd(context.ScopeHandles, 0);
                seen.TryAdd(context.ExertHandles, 0);
                GC.KeepAlive(owner);
            });
            Assert.That(seen.Count, Is.InRange(2, 32));
        }

        [Test]
        public void DefaultSessionWriteOnlyChurnDoesNotAccumulateRetainedHeap()
        {
            Assert.That(BlackboxSession.CurrentKey, Is.EqualTo(BlackboxSession.DefaultKey));
            BlackboxHandle.MaxLogCount = 1;
            var state = BlackboxSession.CurrentState;
            var retained = new long[3];
            WriteAndForget(2048);
            long baseline = CollectAndMeasure(state);
            for (int batch = 0; batch < retained.Length; batch++)
            {
                WriteAndForget(20000);
                retained[batch] = CollectAndMeasure(state);
            }
            // Allow GC/test-runner noise, but reject the old ~700 bytes per context.
            // 60,000 discarded objects retained roughly 42 MB in that implementation.
            foreach (long bytes in retained)
                Assert.That(bytes - baseline, Is.LessThan(2 * 1024 * 1024));
            Assert.That(retained[2] - retained[0], Is.LessThan(1024 * 1024));
            Assert.That(BlackboxSession.CurrentState, Is.SameAs(state));
            Assert.That(state.Subjects.Count(), Is.Zero);
            TestContext.WriteLine($"Retained heap after 20k/40k/60k objects: {retained[0] - baseline}, {retained[1] - baseline}, {retained[2] - baseline} bytes");
            GC.KeepAlive(state);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SharedTrackerSupportsConcurrentRegistrationAndReuse(bool exert)
        {
            using var session = BlackboxSession.Begin();
            var state = BlackboxSession.CurrentState;
            var context = new LogContext(state.Subjects.GetOrAdd(new object()), 1);
            if (exert) ExerciseConcurrentReuse(context.ExertHandles, state);
            else ExerciseConcurrentReuse(context.ScopeHandles, state);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void SharedTrackerRetirementReturnsPendingAndPublishedIds(bool reset, bool exert)
        {
            using var session = BlackboxSession.Begin();
            var state = BlackboxSession.CurrentState;
            var context = new LogContext(state.Subjects.GetOrAdd(new object()), 1);
            Action retire = () => { if (reset) BlackboxHandle.ForceReset(); else session.Dispose(); };
            if (exert) ExerciseRetirement(context.ExertHandles, state, retire, c => c.ExertHandles);
            else ExerciseRetirement(context.ScopeHandles, state, retire, c => c.ScopeHandles);
        }

        private static void ExerciseConcurrentReuse<T>(HandleTracker<T> tracker, SessionState state)
        {
            int before = HandleManager<T>.LiveCountForTests;
            var active = new ConcurrentDictionary<long, byte>();
            RunThreads(8, () =>
            {
                var leases = new HandleLease<T>[40];
                for (int round = 0; round < 100; round++)
                {
                    try
                    {
                        for (int i = 0; i < leases.Length; i++)
                        {
                            leases[i] = tracker.Register(state);
                            if (leases[i].Id <= 0 || !active.TryAdd(leases[i].Id, 0))
                                throw new InvalidOperationException("Registration returned an invalid or duplicate live ID.");
                        }
                        foreach (var lease in leases)
                            if (!HandleManager<T>.IsAlive(lease.Id))
                                throw new InvalidOperationException("Another writer invalidated a live lease.");
                    }
                    finally
                    {
                        foreach (var lease in leases)
                        {
                            active.TryRemove(lease.Id, out _);
                            lease.Release();
                        }
                    }
                    var stale = leases[0];
                    var fresh = tracker.Register(state);
                    try
                    {
                        if (stale.Release() || !HandleManager<T>.IsAlive(fresh.Id))
                            throw new InvalidOperationException("A stale lease released a reused slot.");
                    }
                    finally { fresh.Release(); }
                }
            });
            Assert.That(active, Is.Empty);
            Assert.That(HandleManager<T>.LiveCountForTests, Is.EqualTo(before));
        }

        private static void ExerciseRetirement<T>(HandleTracker<T> tracker, SessionState state,
            Action retire, Func<LogContext, HandleTracker<T>> selectTracker)
        {
            const int workers = 8;
            int before = HandleManager<T>.LiveCountForTests;
            var published = Enumerable.Range(0, 80).Select(_ => tracker.Register(state)).ToArray();
            using var ready = new CountdownEvent(workers);
            using var release = new ManualResetEventSlim();
            var errors = new ConcurrentQueue<Exception>();
            state.BeforeHandleRegistration = () =>
            {
                ready.Signal();
                if (!release.Wait(10000)) throw new TimeoutException("Retirement registration gate timed out.");
            };
            var threads = Enumerable.Range(0, workers).Select(_ => new Thread(() =>
            {
                try
                {
                    var late = tracker.Register(state);
                    try
                    {
                        if (late.Id != 0) throw new InvalidOperationException("A retired tracker accepted a pending registration.");
                    }
                    finally { late.Release(); }
                }
                catch (Exception error) { errors.Enqueue(error); }
            }) { IsBackground = true }).ToArray();
            foreach (var thread in threads) thread.Start();
            try
            {
                Assert.That(ready.Wait(10000), Is.True);
                retire();
                Assert.That(published.All(lease => !HandleManager<T>.IsAlive(lease.Id)), Is.True);

                using var next = BlackboxSession.Begin();
                var nextState = BlackboxSession.CurrentState;
                var nextContext = new LogContext(nextState.Subjects.GetOrAdd(new object()), 1);
                var nextTracker = selectTracker(nextContext);
                var fresh = Enumerable.Range(0, 80).Select(_ => nextTracker.Register(nextState)).ToArray();
                try
                {
                    foreach (var stale in published) Assert.That(stale.Release(), Is.False);
                    release.Set();
                    JoinThreads(threads, errors);
                    Assert.That(fresh.All(lease => HandleManager<T>.IsAlive(lease.Id)), Is.True);
                    Assert.That(HandleManager<T>.LiveCountForTests, Is.EqualTo(before + fresh.Length));
                }
                finally { foreach (var lease in fresh) lease.Release(); }
            }
            finally
            {
                release.Set();
                foreach (var thread in threads) thread.Join(15000);
                foreach (var lease in published) lease.Release();
                state.BeforeHandleRegistration = null;
            }
            Assert.That(HandleManager<T>.LiveCountForTests, Is.EqualTo(before));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int CountTrackersUsedByContexts(int count)
        {
            var seen = new HashSet<object>();
            for (int i = 0; i < count; i++)
            {
                var owner = new object();
                var box = BlackboxRegistry.GetBlackbox(owner);
                box.Write("one", "Run");
                var context = box.CurrentContext;
                seen.Add(context.ScopeHandles);
                seen.Add(context.ExertHandles);
                GC.KeepAlive(owner);
            }
            return seen.Count;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteAndForget(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var owner = new object();
                BlackboxHandle.Of(owner).Write("one");
                GC.KeepAlive(owner);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long CollectAndMeasure(SessionState state)
        {
            BlackboxTestDoubles.ForceFullCollection();
            // Weak registry entries are cleaned independently of tracker ownership.
            state.Subjects.Cleanup();
            BlackboxTestDoubles.ForceFullCollection();
            return GC.GetTotalMemory(true);
        }

        private static void RunThreads(int count, Action action)
        {
            using var start = new ManualResetEventSlim();
            var errors = new ConcurrentQueue<Exception>();
            var threads = Enumerable.Range(0, count).Select(_ => new Thread(() =>
            {
                try
                {
                    if (!start.Wait(10000)) throw new TimeoutException();
                    action();
                }
                catch (Exception error) { errors.Enqueue(error); }
            }) { IsBackground = true }).ToArray();
            foreach (var thread in threads) thread.Start();
            start.Set();
            JoinThreads(threads, errors);
        }

        private static void JoinThreads(Thread[] threads, ConcurrentQueue<Exception> errors)
        {
            foreach (var thread in threads) Assert.That(thread.Join(15000), Is.True);
            Assert.That(errors, Is.Empty);
        }
    }
}
