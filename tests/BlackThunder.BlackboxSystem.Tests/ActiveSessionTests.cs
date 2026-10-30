using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    internal sealed class ActiveSessionTests : BlackboxTestBase
    {
        [Test]
        public void BeginAndDisposeTrackExplicitSessionOnce()
        {
            Assert.That(BlackboxHandle.ActiveSessionCount, Is.Zero);
            Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.Empty);
            var previous = BlackboxHandle.CurrentSessionKey;
            using var session = BlackboxHandle.BeginSession();
            Assert.That(BlackboxHandle.ActiveSessionCount, Is.EqualTo(1));
            Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.EqualTo(new[] { session.SessionKey }));
            session.Dispose();
            Assert.That(BlackboxHandle.ActiveSessionCount, Is.Zero);
            Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.Empty);
            Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(previous));
            session.Dispose();
            Assert.That(BlackboxHandle.ActiveSessionCount, Is.Zero);
            Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(previous));
        }

        [Test]
        public void NestedSessionsKeepStartOrderAndRemoveOnlyDisposedKey()
        {
            using var outer = BlackboxHandle.BeginSession();
            using var inner = BlackboxHandle.BeginSession();
            Assert.That(BlackboxHandle.ActiveSessionCount, Is.EqualTo(2));
            Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.EqualTo(new[] { outer.SessionKey, inner.SessionKey }));
            inner.Dispose();
            inner.Dispose();
            Assert.That(BlackboxHandle.ActiveSessionCount, Is.EqualTo(1));
            Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.EqualTo(new[] { outer.SessionKey }));
            using var next = BlackboxHandle.BeginSession();
            Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.EqualTo(new[] { outer.SessionKey, next.SessionKey }));
        }

        [Test]
        public void KeySnapshotIsIndependentOfCallerMutationsAndLaterSessions()
        {
            using var outer = BlackboxHandle.BeginSession();
            var snapshot = BlackboxHandle.GetActiveSessionKeys();
            using var inner = BlackboxHandle.BeginSession();
            Assert.That(snapshot, Is.EqualTo(new[] { outer.SessionKey }));
            snapshot[0] = Guid.Empty;
            Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.EqualTo(new[] { outer.SessionKey, inner.SessionKey }));
            var beforeDispose = BlackboxHandle.GetActiveSessionKeys();
            inner.Dispose();
            Assert.That(beforeDispose, Is.EqualTo(new[] { outer.SessionKey, inner.SessionKey }));
            Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.EqualTo(new[] { outer.SessionKey }));
        }

        [Test]
        public void DefaultSessionAndItsReplacementsAreNeverCountedOrBulkDisposed()
        {
            var firstDefault = BlackboxHandle.DefaultSessionKey;
            BlackboxHandle.RestartDefaultSession();
            Assert.That(BlackboxHandle.DefaultSessionKey, Is.Not.EqualTo(firstDefault));
            Assert.That(BlackboxHandle.ActiveSessionCount, Is.Zero);
            Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.Empty);
            var handle = BlackboxHandle.Of(new object());
            var defaultKey = BlackboxHandle.DefaultSessionKey;
            BlackboxHandle.DisposeActiveSessions();
            Assert.That(BlackboxHandle.DefaultSessionKey, Is.EqualTo(defaultKey));
            Assert.That(handle.IsValid, Is.True);
            using var session = BlackboxHandle.BeginSession();
            BlackboxHandle.RestartDefaultSession();
            Assert.That(BlackboxHandle.ActiveSessionCount, Is.EqualTo(1));
            Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.EqualTo(new[] { session.SessionKey }));
            Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(session.SessionKey));
        }

        [Test]
        public void AbandonedSessionSurvivesGcUntilBulkDisposalReclaimsItsHandles()
        {
            var scopes = HandleManager<ScopeHandle>.LiveCountForTests;
            var exerts = HandleManager<ExertHandle>.LiveCountForTests;
            var weak = AbandonSession(out var key);
            Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(BlackboxHandle.DefaultSessionKey));
            BlackboxTestDoubles.ForceFullCollection();
            Assert.That(weak.IsAlive, Is.True);
            Assert.That(BlackboxHandle.ActiveSessionCount, Is.EqualTo(1));
            Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.EqualTo(new[] { key }));
            Assert.That(HandleManager<ScopeHandle>.LiveCountForTests, Is.EqualTo(scopes + 1));
            Assert.That(HandleManager<ExertHandle>.LiveCountForTests, Is.EqualTo(exerts + 1));
            BlackboxHandle.DisposeActiveSessions();
            Assert.That(BlackboxHandle.ActiveSessionCount, Is.Zero);
            Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.Empty);
            Assert.That(HandleManager<ScopeHandle>.LiveCountForTests, Is.EqualTo(scopes));
            Assert.That(HandleManager<ExertHandle>.LiveCountForTests, Is.EqualTo(exerts));
            BlackboxTestDoubles.ForceFullCollection();
            Assert.That(weak.IsAlive, Is.False);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference AbandonSession(out Guid key)
        {
            Guid sessionKey = Guid.Empty;
            WeakReference weak = null;
            // Restore the caller's ExecutionContext so Ambient is not another root of the abandoned session.
            ExecutionContext.Run(ExecutionContext.Capture(), _ =>
            {
                var session = BlackboxHandle.BeginSession();
                sessionKey = session.SessionKey;
                weak = new WeakReference(session);
                var handle = BlackboxHandle.Of(new object());
                handle.Scope("abandoned scope");
                handle.Exert(new object(), "abandoned exert");
            }, null);
            key = sessionKey;
            return weak;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisposalFromAnotherFlowLeavesCallerBoundUntilCallerDisposes(bool useThread)
        {
            using var session = BlackboxHandle.BeginSession();
            var handle = BlackboxHandle.Of(new object());
            var before = BlackboxHandle.DefaultSessionKey;
            Guid disposingFlowKey = Guid.Empty;
            void Close()
            {
                session.Dispose();
                disposingFlowKey = BlackboxHandle.CurrentSessionKey;
            }
            if (useThread)
            {
                var thread = new Thread(Close) { IsBackground = true };
                thread.Start();
                Assert.That(thread.Join(10000), Is.True);
            }
            else Task.Run(Close).GetAwaiter().GetResult();
            Assert.That(disposingFlowKey, Is.EqualTo(before));
            Assert.That(BlackboxHandle.ActiveSessionCount, Is.Zero);
            Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.Empty);
            Assert.That(handle.IsValid, Is.False);
            Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(session.SessionKey));
            // The owner's own Dispose still unbinds its flow, without removing or retiring twice.
            session.Dispose();
            Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(before));
            Assert.That(BlackboxHandle.ActiveSessionCount, Is.Zero);
        }

        [Test]
        public async Task ConcurrentDuplicateDisposalsRemoveSessionOnce()
        {
            using var session = BlackboxHandle.BeginSession();
            await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < 100; i++) session.Dispose();
            })));
            Assert.That(BlackboxHandle.ActiveSessionCount, Is.Zero);
            Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.Empty);
        }

        [Test]
        public void ConcurrentBeginsDisposalsAndSnapshotsLeaveNoSessions()
        {
            using var start = new ManualResetEventSlim();
            var errors = new ConcurrentQueue<Exception>();
            var threads = Enumerable.Range(0, 8).Select(_ => new Thread(() =>
            {
                try
                {
                    if (!start.Wait(10000)) throw new TimeoutException();
                    for (int i = 0; i < 100; i++)
                    {
                        using var outer = BlackboxHandle.BeginSession();
                        using var inner = BlackboxHandle.BeginSession();
                        var keys = BlackboxHandle.GetActiveSessionKeys();
                        Assert.That(Array.IndexOf(keys, outer.SessionKey), Is.GreaterThanOrEqualTo(0));
                        Assert.That(Array.IndexOf(keys, inner.SessionKey), Is.GreaterThan(Array.IndexOf(keys, outer.SessionKey)));
                        Assert.That(keys.Distinct().Count(), Is.EqualTo(keys.Length));
                        Assert.That(BlackboxHandle.ActiveSessionCount, Is.GreaterThanOrEqualTo(2));
                        inner.Dispose();
                        inner.Dispose();
                        Assert.That(BlackboxHandle.GetActiveSessionKeys(), Does.Not.Contain(inner.SessionKey));
                    }
                }
                catch (Exception error) { errors.Enqueue(error); }
            }) { IsBackground = true }).ToArray();
            foreach (var thread in threads) thread.Start();
            start.Set();
            foreach (var thread in threads) Assert.That(thread.Join(15000), Is.True);
            Assert.That(errors, Is.Empty);
            Assert.That(BlackboxHandle.ActiveSessionCount, Is.Zero);
            Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.Empty);
        }

        [Test]
        public async Task BulkDisposalInvalidatesHandlesAndRestoresOnlyCallingFlow()
        {
            var defaultHandle = BlackboxHandle.Of(new object());
            var before = BlackboxHandle.CurrentSessionKey;
            var scopes = HandleManager<ScopeHandle>.LiveCountForTests;
            var exerts = HandleManager<ExertHandle>.LiveCountForTests;
            using var outer = BlackboxHandle.BeginSession();
            var outerHandle = BlackboxHandle.Of(new object());
            using var outerScope = outerHandle.Scope("outer scope");
            using var outerExert = outerHandle.Exert(new object(), "outer exert");
            using var inner = BlackboxHandle.BeginSession();
            var innerHandle = BlackboxHandle.Of(new object());
            using var innerScope = innerHandle.Scope("inner scope");
            using var innerExert = innerHandle.Exert(new object(), "inner exert");
            using var ready = new ManualResetEventSlim();
            using var go = new ManualResetEventSlim();
            var child = Task.Run(() =>
            {
                ready.Set();
                Assert.That(go.Wait(10000), Is.True);
                Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(inner.SessionKey));
                var sequence = inner.State.CurrentSequence;
                innerHandle.Write("rejected old handle");
                var fresh = BlackboxHandle.Of(new object());
                fresh.Write("rejected new handle");
                Assert.That(fresh.IsValid, Is.False);
                Assert.That(inner.State.CurrentSequence, Is.EqualTo(sequence));
                // The child's own Dispose unwinds its binding to the parent, which was also bulk-disposed.
                inner.Dispose();
                Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(outer.SessionKey));
                Assert.That(BlackboxHandle.Of(new object()).IsValid, Is.False);
            });
            try
            {
                Assert.That(ready.Wait(10000), Is.True);
                BlackboxHandle.DisposeActiveSessions();
                Assert.That(BlackboxHandle.ActiveSessionCount, Is.Zero);
                Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.Empty);
                Assert.That(outerHandle.IsValid || innerHandle.IsValid, Is.False);
                Assert.That(outerScope.IsAlive || innerScope.IsAlive || outerExert.IsAlive || innerExert.IsAlive, Is.False);
                Assert.That(HandleManager<ScopeHandle>.LiveCountForTests, Is.EqualTo(scopes));
                Assert.That(HandleManager<ExertHandle>.LiveCountForTests, Is.EqualTo(exerts));
                Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(before));
                Assert.That(defaultHandle.IsValid, Is.True);
                BlackboxHandle.DisposeActiveSessions();
                Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(before));
            }
            finally { go.Set(); await child; }
        }

        [Test]
        public async Task BulkDisposalAlsoFindsSessionsStartedInOtherFlows()
        {
            using var ready = new ManualResetEventSlim();
            using var go = new ManualResetEventSlim();
            Guid key = Guid.Empty;
            var child = Task.Run(() =>
            {
                using var session = BlackboxHandle.BeginSession();
                key = session.SessionKey;
                var handle = BlackboxHandle.Of(new object());
                ready.Set();
                Assert.That(go.Wait(10000), Is.True);
                Assert.That(handle.IsValid, Is.False);
                Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(key));
                Assert.That(BlackboxHandle.Of(new object()).IsValid, Is.False);
            });
            try
            {
                Assert.That(ready.Wait(10000), Is.True);
                Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.EqualTo(new[] { key }));
                BlackboxHandle.DisposeActiveSessions();
                Assert.That(BlackboxHandle.ActiveSessionCount, Is.Zero);
                Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(BlackboxHandle.DefaultSessionKey));
            }
            finally { go.Set(); await child; }
        }

        [Test]
        public void DiagnosticsAreOnlyExposedOnBlackboxHandle()
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
            var count = typeof(BlackboxHandle).GetProperty(nameof(BlackboxHandle.ActiveSessionCount), flags);
            Assert.That(count.PropertyType, Is.EqualTo(typeof(int)));
            Assert.That(count.CanWrite, Is.False);
            var keys = typeof(BlackboxHandle).GetMethod(nameof(BlackboxHandle.GetActiveSessionKeys), flags);
            Assert.That(keys.ReturnType, Is.EqualTo(typeof(Guid[])));
            Assert.That(keys.GetParameters(), Is.Empty);
            var dispose = typeof(BlackboxHandle).GetMethod(nameof(BlackboxHandle.DisposeActiveSessions), flags);
            Assert.That(dispose.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(dispose.GetParameters(), Is.Empty);
            Assert.That(typeof(BlackboxSession).GetMembers(flags), Is.Empty);
        }
    }
}
