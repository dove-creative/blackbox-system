using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    internal sealed class LogContextTests : BlackboxTestBase
    {
        [Test]
        public void EnqueueLogStoresLogData()
        {
            var blackbox = BlackboxTestDoubles.Blackbox("owner");
            var context = new LogContext(blackbox, 10);

            // Table 4-1 / Ready x EnqueueLog
            context.EnqueueLog("hello", 0, "Run");

            var log = context.GetLogs().Single();
            Assert.That(log.Message, Is.EqualTo("hello"));
            Assert.That(log.Owner, Is.SameAs(blackbox));
            Assert.That(log.MethodName, Is.EqualTo("Run"));
        }

        [Test]
        public void RingBufferKeepsRecentLogs()
        {
            var blackbox = BlackboxTestDoubles.Blackbox("owner");
            var context = new LogContext(blackbox, 2);

            // Table 4-1 / RingBufferFull x EnqueueLog
            context.EnqueueLog("one", 0, "Run");
            context.EnqueueLog("two", 1, "Run");
            context.EnqueueLog("three", 2, "Run");

            var logs = context.GetLogs().ToList();
            Assert.That(logs.Count, Is.EqualTo(2));
            Assert.That(logs[0].Message, Is.EqualTo("two"));
            Assert.That(logs[1].Message, Is.EqualTo("three"));
        }

        [Test]
        public void OpenAndCloseScopeStoresScopePair()
        {
            var blackbox = BlackboxTestDoubles.Blackbox("owner");
            var context = new LogContext(blackbox, 10);

            // Table 4-1 / Ready x OpenScope
            context.OpenScope("scope", 0, "Run", 3);

            // Table 4-1 / ScopeOpen x CloseScope.CloseNewest
            context.CloseScope(3, 1);

            var logs = context.GetLogs().ToList();
            Assert.That(logs.Count, Is.EqualTo(2));
            Assert.That(logs[0].ScopeType, Is.EqualTo(ScopeType.Open));
            Assert.That(logs[1].ScopeType, Is.EqualTo(ScopeType.Close));
            Assert.That(logs[0].ScopeId, Is.EqualTo(logs[1].ScopeId));
        }

        [Test]
        public void CloseMissingScopeWarns()
        {
            var blackbox = BlackboxTestDoubles.Blackbox("owner");
            var context = new LogContext(blackbox, 10);

            // Table 4-1 / Ready x CloseScope.CloseNewest
            context.CloseScope(99, 0);

            Assert.That(context.GetLogs().ToList(), Is.Empty);
            Assert.That(BlackboxTestDoubles.WarningLogs.Single(), Does.Contain("cannot be closed"));
        }

        [Test]
        public void CloseOuterFirstAutoClosesNewerScopes()
        {
            var blackbox = BlackboxTestDoubles.Blackbox("owner");
            var context = new LogContext(blackbox, 10);
            context.OpenScope("outer", 0, "Outer", 1);
            context.OpenScope("inner", 1, "Inner", 2);

            // Table 4-1 / ScopeOpen x CloseScope.CloseOuterFirst
            context.CloseScope(1, 2);

            var logs = context.GetLogs().ToList();
            Assert.That(logs.Count, Is.EqualTo(4));
            Assert.That(logs.Count(log => log.ScopeType == ScopeType.Close), Is.EqualTo(2));
            Assert.That(logs[2].Message, Does.Contain("Closed automatically"));
            Assert.That(BlackboxTestDoubles.WarningLogs.Single(), Does.Contain("Newer scopes"));
        }

        [Test]
        public void CloseFromDifferentThreadWarnsWithoutAutoClosingNewerScopes()
        {
            var blackbox = BlackboxTestDoubles.Blackbox("owner");
            var context = new LogContext(blackbox, 10);
            context.OpenScope("outer", 0, "Outer", 1);
            context.OpenScope("inner", 1, "Inner", 2);

            // Table 4-1 / ScopeOpen x CloseScope.CloseFromDifferentThread
            var thread = new Thread(() => context.CloseScope(1, 2));
            thread.Start();
            thread.Join();

            var logs = context.GetLogs().ToList();
            Assert.That(logs.Count, Is.EqualTo(3));
            Assert.That(logs.Count(log => log.ScopeType == ScopeType.Close), Is.EqualTo(1));
            Assert.That(BlackboxTestDoubles.WarningLogs.Single(), Does.Contain("different thread"));
            Assert.That(BlackboxTestDoubles.WarningLogs.Single(), Does.Contain("remain open"));
        }

        [Test]
        public void ResolveWithAddsSourceAndTargetTags()
        {
            var source = BlackboxTestDoubles.Blackbox("source");
            var target = BlackboxTestDoubles.Blackbox("target");
            var context = new LogContext(source, 10);
            var handle = context.EnqueueLog("hello %0", 0, "Run");

            // Table 4-1 / ScopeOpen x ResolveWith.ValidTarget
            handle.With(target.Owner, TargetTypes.Full);

            var sourceLog = context.GetLogs().Single();
            Assert.That(sourceLog.Tags.Length, Is.EqualTo(1));
            Assert.That(sourceLog.Tags[0].Target, Is.SameAs(target));
            Assert.That(BlackboxTestDoubles.Lines(target).Single(), Does.Contain("tagged by"));
        }

        [Test]
        public void TryMergeScopeMergesAdjacentInteractionAndOpenScope()
        {
            var blackbox = BlackboxTestDoubles.Blackbox("target");
            var source = BlackboxTestDoubles.Blackbox("source");
            var context = new LogContext(blackbox, 10);
            context.EnqueueLog("call", 0, "Send", -1, 4, null, source);
            context.OpenScope("scope", 1, "Receive", 2);

            // Table 4-1 / ScopeOpen x TryMergeScope.MergeAdjacent
            var merged = context.TryMergeScope(4);

            var logs = context.GetLogs().ToList();
            Assert.That(merged, Is.True);
            Assert.That(logs.Count, Is.EqualTo(1));
            Assert.That(logs[0].ScopeType, Is.EqualTo(ScopeType.Open));
            Assert.That(logs[0].InteractionId, Is.EqualTo(4));
            Assert.That(logs[0].Message, Is.EqualTo("scope (from: call)"));
        }

        [Test]
        public void TryMergeScopeRejectsNonAdjacentOrMissingTarget()
        {
            var blackbox = BlackboxTestDoubles.Blackbox("target");
            var source = BlackboxTestDoubles.Blackbox("source");
            var context = new LogContext(blackbox, 10);
            context.EnqueueLog("call", 0, "Send", -1, 4, null, source);
            context.EnqueueLog("middle", 1, "Run");
            context.OpenScope("scope", 2, "Receive", 2);

            // Table 4-1 / ScopeOpen x TryMergeScope.MergeNonAdjacent
            Assert.That(context.TryMergeScope(4), Is.False);

            // Table 4-1 / Ready x TryMergeScope.MergeAdjacent
            Assert.That(context.TryMergeScope(99), Is.False);
            Assert.That(context.GetLogs().ToList().Count, Is.EqualTo(3));
        }

        [Test]
        public void GetLogsStopsAtMaxSequence()
        {
            var blackbox = BlackboxTestDoubles.Blackbox("owner");
            var context = new LogContext(blackbox, 10);
            context.EnqueueLog("one", 0, "Run");
            context.EnqueueLog("two", 1, "Run");
            context.EnqueueLog("three", 2, "Run");

            // Table 4-1 / Ready x GetLogs
            var logs = context.GetLogs(1).ToList();

            Assert.That(logs.Count, Is.EqualTo(2));
            Assert.That(logs.Last().Message, Is.EqualTo("two"));
        }
    }
}
