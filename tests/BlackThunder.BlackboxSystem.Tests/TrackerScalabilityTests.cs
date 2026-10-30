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
            Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(BlackboxHandle.DefaultSessionKey));
            BlackboxHandle.MaxLogCount = 1;
            var initial = CountTrackersUsedByContexts(1);
            Assert.That(initial, Is.EqualTo(2));
            Assert.That(CountTrackersUsedByContexts(20000), Is.EqualTo(initial));
        }

        [Test]
        public void DefaultSessionTrackerCountIsBoundedAcrossManyThreads()
        {
            BlackboxHandle.MaxLogCount = 1;
            var recordingSession = BlackboxSessionRegistry.Current;
            var seen = new ConcurrentDictionary<object, byte>();
            RunThreads(48, () =>
            {
                var owner = new object();
                var context = recordingSession.Subjects.GetOrAdd(owner).CurrentContext;
                context.EnqueueLog("one", -1, "Run");
                seen.TryAdd(recordingSession.GetScopeTracker(ThreadToken.Current), 0);
                seen.TryAdd(recordingSession.GetExertTracker(ThreadToken.Current), 0);
                GC.KeepAlive(owner);
            });
            Assert.That(seen.Count, Is.InRange(2, 32));
        }

        [Test]
        public void DefaultSessionWriteOnlyChurnDoesNotAccumulateRetainedHeap()
        {
            Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(BlackboxHandle.DefaultSessionKey));
            BlackboxHandle.MaxLogCount = 1;
            var recordingSession = BlackboxSessionRegistry.Current;
            var retained = new long[3];
            WriteAndForget(2048);
            long baseline = CollectAndMeasure(recordingSession);
            for (int batch = 0; batch < retained.Length; batch++)
            {
                WriteAndForget(20000);
                retained[batch] = CollectAndMeasure(recordingSession);
            }
            // Allow GC/test-runner noise, but reject the old ~700 bytes per context.
            // 60,000 discarded objects retained roughly 42 MB in that implementation.
            foreach (long bytes in retained)
                Assert.That(bytes - baseline, Is.LessThan(2 * 1024 * 1024));
            Assert.That(retained[2] - retained[0], Is.LessThan(1024 * 1024));
            Assert.That(BlackboxSessionRegistry.Current, Is.SameAs(recordingSession));
            Assert.That(recordingSession.Subjects.Count(), Is.Zero);
            TestContext.WriteLine($"Retained heap after 20k/40k/60k objects: {retained[0] - baseline}, {retained[1] - baseline}, {retained[2] - baseline} bytes");
            GC.KeepAlive(recordingSession);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SharedTrackerSupportsConcurrentRegistrationAndReuse(bool exert)
        {
            using var session = BlackboxHandle.BeginSession();
            var recordingSession = BlackboxSessionRegistry.Current;
            var context = new LogContext(recordingSession.Subjects.GetOrAdd(new object()), 1);
            if (exert) ExerciseConcurrentReuse(recordingSession.GetExertTracker(ThreadToken.Current), recordingSession);
            else ExerciseConcurrentReuse(recordingSession.GetScopeTracker(ThreadToken.Current), recordingSession);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void SharedTrackerRetirementReturnsPendingAndPublishedIds(bool restartDefault, bool exert)
        {
            using var session = restartDefault ? null : BlackboxHandle.BeginSession();
            var recordingSession = BlackboxSessionRegistry.Current;
            var context = new LogContext(recordingSession.Subjects.GetOrAdd(new object()), 1);
            Action retire = () => { if (restartDefault) BlackboxHandle.RestartDefaultSession(); else session.Dispose(); };
            if (exert) ExerciseRetirement(recordingSession.GetExertTracker(ThreadToken.Current), recordingSession, retire, s => s.GetExertTracker(ThreadToken.Current));
            else ExerciseRetirement(recordingSession.GetScopeTracker(ThreadToken.Current), recordingSession, retire, s => s.GetScopeTracker(ThreadToken.Current));
        }

        private static void ExerciseConcurrentReuse<T>(HandleTracker<T> tracker, BlackboxSession recordingSession)
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
                            leases[i] = tracker.Register(recordingSession.State);
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
                    var fresh = tracker.Register(recordingSession.State);
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

        private static void ExerciseRetirement<T>(HandleTracker<T> tracker, BlackboxSession recordingSession,
            Action retire, Func<BlackboxSession, HandleTracker<T>> selectTracker)
        {
            const int Workers = 8;
            int before = HandleManager<T>.LiveCountForTests;
            var published = Enumerable.Range(0, 80).Select(_ => tracker.Register(recordingSession.State)).ToArray();
            using var ready = new CountdownEvent(Workers);
            using var release = new ManualResetEventSlim();
            var errors = new ConcurrentQueue<Exception>();
            recordingSession.State.BeforeHandleRegistration = () =>
            {
                ready.Signal();
                if (!release.Wait(10000)) throw new TimeoutException("Retirement registration gate timed out.");
            };
            var threads = Enumerable.Range(0, Workers).Select(_ => new Thread(() =>
            {
                try
                {
                    var late = tracker.Register(recordingSession.State);
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

                using var next = BlackboxHandle.BeginSession();
                var nextSession = BlackboxSessionRegistry.Current;
                var nextContext = new LogContext(nextSession.Subjects.GetOrAdd(new object()), 1);
                var nextTracker = selectTracker(nextSession);
                var fresh = Enumerable.Range(0, 80).Select(_ => nextTracker.Register(nextSession.State)).ToArray();
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
                recordingSession.State.BeforeHandleRegistration = null;
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
                var box = BlackboxSessionRegistry.GetBlackbox(owner);
                box.Write("one", "Run");
                var context = box.CurrentContext;
                seen.Add(BlackboxSessionRegistry.Current.GetScopeTracker(ThreadToken.Current));
                seen.Add(BlackboxSessionRegistry.Current.GetExertTracker(ThreadToken.Current));
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
        private static long CollectAndMeasure(BlackboxSession recordingSession)
        {
            BlackboxTestDoubles.ForceFullCollection();
            // Weak registry entries are cleaned independently of tracker ownership.
            recordingSession.Subjects.Cleanup();
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
