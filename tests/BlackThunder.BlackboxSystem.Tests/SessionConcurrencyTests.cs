using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BlackThunder.BlackboxSystem.Exporters;
using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    internal sealed class SessionConcurrencyTests : BlackboxTestBase
    {
        [Test]
        public async Task ParallelSessionsIsolateStorageSettingsAndCrash()
        {
            var owner = new object();
            using var ready = new CountdownEvent(2);
            using var go = new ManualResetEventSlim();
            async Task<Blackbox> Run(bool crash)
            {
                return await Task.Run(() =>
                {
                    using var session = BlackboxSession.Begin();
                    BlackboxHandle.MaxLogCount = crash ? 2 : 5;
                    var box = BlackboxRegistry.GetBlackbox(owner);
                    ready.Signal();
                    Assert.That(go.Wait(10000), Is.True);
                    if (crash) box.State.TryFreeze(new CrashInfo("boom", "stack"));
                    box.Write("after", "Run");
                    Assert.That(BlackboxHandle.MaxLogCount, Is.EqualTo(crash ? 2 : 5));
                    return box;
                });
            }
            var a = Run(true);
            var b = Run(false);
            try { Assert.That(ready.Wait(10000), Is.True); }
            finally { go.Set(); }
            var boxes = await Task.WhenAll(a, b);
            Assert.That(boxes[0], Is.Not.SameAs(boxes[1]));
            Assert.That(boxes[0].GetLogs(), Is.Empty);
            Assert.That(boxes[1].GetLogs().Single().Message, Is.EqualTo("after"));
            Assert.That(BlackboxHandle.MaxLogCount, Is.EqualTo(100));
        }

        [Test]
        public async Task NestedTasksAndThreadsInheritStateAndNestedDisposeRestoresParent()
        {
            var before = BlackboxSession.CurrentState;
            using (var session = BlackboxSession.Begin())
            {
                var state = BlackboxSession.CurrentState;
                Assert.That(await Task.Run(async () => await Task.Run(() => BlackboxSession.CurrentState)), Is.SameAs(state));
                SessionState threadState = null;
                var thread = new Thread(() => threadState = BlackboxSession.CurrentState);
                thread.Start();
                Assert.That(thread.Join(10000), Is.True);
                Assert.That(threadState, Is.SameAs(state));
                using (BlackboxSession.Begin()) Assert.That(BlackboxSession.CurrentState, Is.Not.SameAs(state));
                Assert.That(BlackboxSession.CurrentState, Is.SameAs(state));
            }
            Assert.That(BlackboxSession.CurrentState, Is.SameAs(before));
        }

        [Test]
        public async Task ResetDoesNotRebindExistingChildExecutionContexts()
        {
            using var session = BlackboxSession.Begin();
            var old = BlackboxSession.CurrentState;
            var owner = new object();
            var handle = BlackboxHandle.Of(owner);
            var box = old.Subjects.GetOrAdd(owner);
            using var ready = new ManualResetEventSlim();
            using var go = new ManualResetEventSlim();
            var task = Task.Run(() =>
            {
                ready.Set();
                Assert.That(go.Wait(10000), Is.True);
                handle.Write("old handle");
                BlackboxHandle.Of(new object()).Write("new handle in old flow");
                Assert.That(BlackboxSession.CurrentState, Is.SameAs(old));
            });
            try
            {
                Assert.That(ready.Wait(10000), Is.True);
                BlackboxHandle.ForceReset();
                BlackboxHandle.Of(owner).Write("new generation");
            }
            finally { go.Set(); }
            await task;
            Assert.That(box.GetLogs(), Is.Empty);
            Assert.That(BlackboxRegistry.GetBlackbox(owner).GetLogs().Single().Message, Is.EqualTo("new generation"));
            Assert.That(BlackboxTestDoubles.WarningLogs.Count(s => s.Contains("reset generation")), Is.EqualTo(1));
        }

        [Test]
        public async Task UnboundDefaultCallersObserveSharedResetAndSettings()
        {
            var old = BlackboxSession.CurrentState;
            using var go = new ManualResetEventSlim();
            var task = Task.Run(() =>
            {
                Assert.That(go.Wait(10000), Is.True);
                return (BlackboxSession.CurrentState, BlackboxHandle.MaxLogCount);
            });
            try { BlackboxHandle.ForceReset(); BlackboxHandle.MaxLogCount = 7; }
            finally { go.Set(); }
            var result = await task;
            Assert.That(result.Item1, Is.SameAs(BlackboxSession.CurrentState).And.Not.SameAs(old));
            Assert.That(result.Item2, Is.EqualTo(7));
        }

        [Test]
        public void RestoringDisposedParentNeverFallsBackToDefault()
        {
            var outer = BlackboxSession.Begin();
            var old = BlackboxSession.CurrentState;
            var inner = BlackboxSession.Begin();
            outer.Dispose();
            inner.Dispose();
            try
            {
                Assert.That(BlackboxSession.CurrentState, Is.SameAs(old));
                Assert.That(BlackboxHandle.Of(new object()).IsValid, Is.False);
            }
            finally { outer.Dispose(); }
        }

        [Test]
        public void CollisionHeavyRegistrationCleanupAndContextCreationRemainUnique()
        {
            using var session = BlackboxSession.BeginForTests(1);
            var state = BlackboxSession.CurrentState;
            var owners = Enumerable.Range(0, 24).Select(_ => new object()).ToArray();
            var boxes = new ConcurrentBag<Blackbox>();
            RunThreads(8, () =>
            {
                for (int i = 0; i < 200; i++)
                {
                    var box = state.Subjects.GetOrAdd(owners[0]);
                    boxes.Add(box);
                    box.Write("record", "Run");
                    state.Subjects.GetOrAdd(owners[i % owners.Length]);
                    if (i % 13 == 0) state.Subjects.Cleanup();
                }
            });
            Assert.That(boxes.Distinct().Count(), Is.EqualTo(1));
            Assert.That(boxes.First().LogContexts.Count, Is.EqualTo(8));
            Assert.That(state.Subjects.Count(), Is.EqualTo(owners.Length));
            GC.KeepAlive(owners);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public async Task RetirementRacingHandleRegistrationReturnsSlots(bool reset, bool exert)
        {
            using var session = BlackboxSession.Begin();
            var state = BlackboxSession.CurrentState;
            var owner = new object();
            var handle = BlackboxHandle.Of(owner);
            int scopeBefore = HandleManager<ScopeHandle>.LiveCountForTests;
            int exertBefore = HandleManager<ExertHandle>.LiveCountForTests;
            using var ready = new ManualResetEventSlim();
            using var go = new ManualResetEventSlim();
            state.BeforeHandleRegistration = () => { ready.Set(); Assert.That(go.Wait(10000), Is.True); };
            var task = Task.Run(() =>
            {
                if (exert) { var h = handle.Exert(owner, "call"); Assert.That(h.IsAlive, Is.False); h.Dispose(); }
                else { var h = handle.Scope("scope"); Assert.That(h.IsAlive, Is.False); h.Dispose(); }
            });
            try
            {
                Assert.That(ready.Wait(10000), Is.True);
                if (reset) BlackboxHandle.ForceReset(); else session.Dispose();
            }
            finally { go.Set(); }
            await task;
            Assert.That(HandleManager<ScopeHandle>.LiveCountForTests, Is.EqualTo(scopeBefore));
            Assert.That(HandleManager<ExertHandle>.LiveCountForTests, Is.EqualTo(exertBefore));
            using var fresh = BlackboxSession.Begin();
            var live = BlackboxHandle.Of(new object()).Scope("fresh");
            handle.Scope("rejected").Dispose();
            Assert.That(live.IsAlive, Is.True);
            live.Dispose();
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task DelayedPublicationStaysInRetiredGeneration(bool reset)
        {
            using var session = BlackboxSession.Begin();
            var owner = new object();
            var old = BlackboxRegistry.GetBlackbox(owner);
            using var ready = new ManualResetEventSlim();
            using var go = new ManualResetEventSlim();
            old.State.BeforeLogPublication = _ => { ready.Set(); Assert.That(go.Wait(10000), Is.True); };
            var task = Task.Run(() => old.Write("in flight", "Run"));
            try
            {
                Assert.That(ready.Wait(10000), Is.True);
                if (reset) BlackboxHandle.ForceReset(); else session.Dispose();
                BlackboxHandle.Of(owner).Write("new");
            }
            finally { go.Set(); }
            await task;
            old.Write("rejected", "Run");
            Assert.That(old.GetLogs().Select(l => l.Message), Is.EqualTo(new[] { "in flight" }));
            Assert.That(BlackboxRegistry.GetBlackbox(owner).GetLogs().Select(l => l.Message), Is.EqualTo(new[] { "new" }));
        }

        [Test]
        public async Task UnpublishedReservationCannotExposePreviousLapOrOverwriteNewerEntry()
        {
            using var session = BlackboxSession.Begin();
            var state = BlackboxSession.CurrentState;
            var context = new LogContext(BlackboxRegistry.GetBlackbox(new object()), 1);
            context.EnqueueLog("previous lap", 0, "Run");
            using var ready = new ManualResetEventSlim();
            using var go = new ManualResetEventSlim();
            state.BeforeLogPublication = i => { if (i == 1) { ready.Set(); Assert.That(go.Wait(10000), Is.True); } };
            var task = Task.Run(() => context.EnqueueLog("delayed", 1, "Run"));
            try
            {
                Assert.That(ready.Wait(10000), Is.True);
                Assert.That(context.GetLogs(), Is.Empty);
                context.EnqueueLog("newest", 2, "Run");
            }
            finally { go.Set(); }
            await task;
            Assert.That(context.GetLogs().Single().Message, Is.EqualTo("newest"));
        }

        [Test]
        public void SequenceCutoffSkipsRatherThanTruncatesNonMonotonicEntries()
        {
            using var session = BlackboxSession.Begin();
            var context = new LogContext(BlackboxRegistry.GetBlackbox(new object()), 5);
            context.EnqueueLog("later", 9, "Run");
            context.EnqueueLog("earlier", 1, "Run");
            Assert.That(context.GetLogs(3).Single().Message, Is.EqualTo("earlier"));
        }

        [Test]
        public void ScopeIsUpdatedBeforeInteractionIsInvalidated()
        {
            using var session = BlackboxSession.Begin();
            var state = BlackboxSession.CurrentState;
            var context = new LogContext(BlackboxRegistry.GetBlackbox(new object()), 5);
            context.EnqueueLog("call", 0, "Run", interactionId: 7);
            context.OpenScope("scope", 1, "Run", 4);
            bool observed = false;
            state.AfterScopeMerge = () =>
            {
                var logs = context.GetLogs().ToArray();
                Assert.That(logs.Length, Is.EqualTo(2));
                Assert.That(logs[1].InteractionId, Is.EqualTo(7));
                observed = true;
            };
            Assert.That(context.TryMergeScope(7), Is.True);
            Assert.That(observed, Is.True);
            Assert.That(context.GetLogs().Single().ScopeType, Is.EqualTo(ScopeType.Open));
        }

        [Test]
        public void ForeignThreadClosesAndExertsShareOneWarningBudget()
        {
            using var session = BlackboxSession.Begin();
            var warnings = new ConcurrentQueue<string>();
            BlackboxHandle.WarningLogger = warnings.Enqueue;
            var owner = new object();
            var handle = BlackboxHandle.Of(owner);
            var scopes = Enumerable.Range(0, 16).Select(_ => handle.Scope("scope")).ToArray();
            var exerts = Enumerable.Range(0, 16).Select(_ => handle.Exert(owner, "call")).ToArray();
            RunThreads(4, () => { foreach (var h in scopes) h.Dispose(); foreach (var h in exerts) h.Dispose(); });
            Assert.That(warnings.Count(s => s.Contains("different thread")), Is.EqualTo(1));
            Assert.That(BlackboxRegistry.GetBlackbox(owner).GetLogs().Count(l => l.ScopeType == ScopeType.Close), Is.EqualTo(16));
        }

        [Test]
        public void WarningLoggerCanReenterAfterRetirementWithoutRecursing()
        {
            using var session = BlackboxSession.Begin();
            var h = BlackboxHandle.Of(new object());
            int count = 0;
            BlackboxHandle.WarningLogger = _ => { count++; h.Write("reentered"); };
            session.Dispose();
            h.Write("rejected"); h.Write("rejected again");
            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void ResetReclaimsAbandonedHandlesWithoutInvalidatingAnotherSession()
        {
            using var outer = BlackboxSession.Begin();
            var live = BlackboxHandle.Of(new object()).Scope("outer");
            int before = HandleManager<ScopeHandle>.LiveCountForTests;
            for (int round = 0; round < 30; round++)
            {
                using var inner = BlackboxSession.Begin();
                var handle = BlackboxHandle.Of(new object());
                for (int i = 0; i < 70; i++) handle.Scope("abandoned");
                BlackboxHandle.ForceReset();
            }
            Assert.That(live.IsAlive, Is.True);
            Assert.That(HandleManager<ScopeHandle>.LiveCountForTests, Is.EqualTo(before));
            live.Dispose();
        }

        [Test]
        public void OrdinaryExportFailureDoesNotFreezeAndCrashCanBeRetriedNormally()
        {
            using var session = BlackboxSession.Begin();
            var owner = new object();
            var handle = BlackboxHandle.Of(owner);
            var box = BlackboxRegistry.GetBlackbox(owner);
            handle.Write("before");
            Assert.Throws<InvalidOperationException>(() => handle.Export());
            handle.Write("after failed export");
            Assert.That(box.GetLogs().Count, Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => handle.CrashExport("boom", openLog: OpenLogOption.Never));
            handle.Write("frozen");
            BlackboxHandle.LogDirectory = BlackboxTestDoubles.CreateTempDirectory();
            handle.Export(format: ExportFormat.Txt, openLogOption: OpenLogOption.Never);
            var text = File.ReadAllText(Directory.GetFiles(BlackboxHandle.LogDirectory).Single());
            Assert.That(text, Does.Contain("boom").And.Contain("after failed export").And.Not.Contain("frozen"));
            Assert.That(box.State.Crash, Is.Not.Null);
            BlackboxHandle.ForceReset();
            Assert.That(BlackboxHandle.Of(owner).IsValid, Is.True);
        }

        [Test]
        public void ConcurrentCrashExportsOnlyOnceAndOrdinaryExportsNeverOverwrite()
        {
            using var session = BlackboxSession.Begin();
            var directory = BlackboxTestDoubles.CreateTempDirectory();
            BlackboxHandle.Configure(directory, _ => { }, openLogOption: OpenLogOption.Never);
            var handle = BlackboxHandle.Of(new object());
            RunThreads(8, () => handle.CrashExport("boom", format: ExportFormat.Txt, openLog: OpenLogOption.Never));
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
            RunThreads(8, () => handle.Export(format: ExportFormat.Txt, openLogOption: OpenLogOption.Never));
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(9));
        }

        [Test]
        public void TruncatedScopeHistoryExportsWithNonnegativeDepth()
        {
            using var session = BlackboxSession.Begin();
            BlackboxHandle.MaxLogCount = 2;
            BlackboxHandle.LogDirectory = BlackboxTestDoubles.CreateTempDirectory();
            var handle = BlackboxHandle.Of(new object());
            var scope = handle.Scope("open");
            handle.Write("one"); handle.Write("two"); scope.Dispose();
            Assert.DoesNotThrow(() => handle.Export(format: ExportFormat.Html, openLogOption: OpenLogOption.Never));
            var text = File.ReadAllText(Directory.GetFiles(BlackboxHandle.LogDirectory).Single());
            Assert.That(text, Does.Not.Contain("data-depth='-"));
        }

        [Test]
        public void RegisteredLookupAndWarmedTrackingDoNotAllocate()
        {
            using var session = BlackboxSession.Begin();
            var state = BlackboxSession.CurrentState;
            var owner = new object();
            var box = state.Subjects.GetOrAdd(owner);
            var tracker = new HandleTracker<ScopeHandle>(state);
            for (int i = 0; i < 10000; i++) { state.Subjects.GetOrAdd(owner); tracker.Register(state).Release(); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) { state.Subjects.GetOrAdd(owner); tracker.Register(state).Release(); }
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
            GC.KeepAlive(box);
            GC.KeepAlive(owner);
        }

        [Test]
        public async Task LiveExportAndTagUpdatesRemainReadableAcrossRingWraps()
        {
            using var session = BlackboxSession.Begin();
            BlackboxHandle.MaxLogCount = 16;
            var owner = new object();
            var target = new object();
            var handle = BlackboxHandle.Of(owner);
            var box = BlackboxRegistry.GetBlackbox(owner);
            using var start = new ManualResetEventSlim();
            var writer = Task.Run(() =>
            {
                Assert.That(start.Wait(10000), Is.True);
                Parallel.For(0, 4, _ =>
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        using var scope = handle.Scope("scope");
                        handle.Write("record %0").With(target);
                    }
                });
            });
            start.Set();
            do
            {
                var graph = Tools.BuildExportGraph(box, 2, true, box.State.CurrentSequence);
                foreach (var node in graph)
                {
                    Assert.That(node.Logs.Select(log => log.Sequence), Is.Ordered);
                    Assert.That(node.Logs.All(log => log.ScopeDepth >= 0), Is.True);
                    foreach (var log in node.Logs) Assert.DoesNotThrow(() => LogFormatter.RenderTextLine(log, log.ScopeDepth));
                }
                await Task.Yield();
            } while (!writer.IsCompleted);
            await writer;
            GC.KeepAlive(owner);
            GC.KeepAlive(target);
        }

        [Test]
        public async Task DisposeInAnotherAsyncMethodDoesNotRestoreCallersBinding()
        {
            var original = BlackboxSession.CurrentState;
            using (var session = BlackboxSession.Begin())
            {
                var state = BlackboxSession.CurrentState;
                await CloseAsync(session);
                Assert.That(BlackboxSession.CurrentState, Is.SameAs(state));
                Assert.That(state.IsCurrent, Is.False);
            }
            Assert.That(BlackboxSession.CurrentState, Is.SameAs(original));

            static async Task CloseAsync(BlackboxSession session)
            {
                await Task.Yield();
                session.Dispose();
            }
        }

        private static void RunThreads(int count, Action action)
        {
            using var start = new ManualResetEventSlim();
            var errors = new ConcurrentQueue<Exception>();
            var threads = Enumerable.Range(0, count).Select(_ => new Thread(() =>
            {
                try { if (!start.Wait(10000)) throw new TimeoutException(); action(); }
                catch (Exception e) { errors.Enqueue(e); }
            }) { IsBackground = true }).ToArray();
            foreach (var thread in threads) thread.Start();
            start.Set();
            foreach (var thread in threads) Assert.That(thread.Join(15000), Is.True);
            Assert.That(errors, Is.Empty);
        }
    }
}
