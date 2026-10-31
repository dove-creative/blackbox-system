using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    internal sealed class ExertHandleTests : BlackboxTestBase
    {
        [Test]
        [RunWithUniTest]
        public void DefaultDisposeDoesNothing()
        {
            // Table 2-3 / Default x Dispose.MergeTargetExists
            default(ExertHandle).Dispose();

            Assert.That(BlackboxSessionRegistry.Count(), Is.EqualTo(0));
            Assert.That(BlackboxTestDoubles.WarningLogs, Is.Empty);
        }

        [Test]
        [RunWithUniTest]
        public void DisposeMergesAdjacentTargetScope()
        {
            var source = BlackboxTestDoubles.Blackbox("source");
            var target = BlackboxTestDoubles.Blackbox("target");
            var handle = BlackboxTestDoubles.Exert(source, target, "call", "Send");
            BlackboxTestDoubles.Scope(target, "scope", "Receive");

            // Table 2-3 / Alive x Dispose.MergeTargetExists
            handle.Dispose();

            var logs = BlackboxTestDoubles.Logs(target);
            Assert.That(handle.IsDisposed, Is.True);
            Assert.That(logs.Count, Is.EqualTo(1));
            Assert.That(logs[0].ScopeType, Is.EqualTo(ScopeType.Open));
            Assert.That(logs[0].InteractionId, Is.GreaterThanOrEqualTo(0));
            Assert.That(logs[0].Message, Does.Contain("call"));
        }

        [Test]
        [RunWithUniTest]
        public void DisposeWithoutMergeTargetOnlyDisposes()
        {
            var source = BlackboxTestDoubles.Blackbox("source");
            var target = BlackboxTestDoubles.Blackbox("target");
            var handle = BlackboxTestDoubles.Exert(source, target, "call", "Send");

            // Table 2-3 / Alive x Dispose.MergeTargetMissing
            handle.Dispose();

            var logs = BlackboxTestDoubles.Logs(target);
            Assert.That(handle.IsDisposed, Is.True);
            Assert.That(logs.Count, Is.EqualTo(1));
            Assert.That(logs[0].InteractionId, Is.GreaterThanOrEqualTo(0));
            Assert.That(logs[0].ScopeType, Is.EqualTo(ScopeType.None));
        }

        [Test]
        [RunWithUniTest]
        public void DisposedDisposeDoesNotMergeAgain()
        {
            var source = BlackboxTestDoubles.Blackbox("source");
            var target = BlackboxTestDoubles.Blackbox("target");
            var handle = BlackboxTestDoubles.Exert(source, target, "call", "Send");
            BlackboxTestDoubles.Scope(target, "scope", "Receive");
            handle.Dispose();
            var countAfterFirstDispose = BlackboxTestDoubles.Logs(target).Count;

            // Table 2-3 / Disposed x Dispose.MergeTargetExists
            handle.Dispose();

            Assert.That(BlackboxTestDoubles.Logs(target).Count, Is.EqualTo(countAfterFirstDispose));
        }

        [Test]
        [RunWithUniTest]
        public void DifferentThreadDisposeWarns()
        {
            var source = BlackboxTestDoubles.Blackbox("source");
            var target = BlackboxTestDoubles.Blackbox("target");
            var handle = BlackboxTestDoubles.Exert(source, target, "call", "Send");

            // Table 2-3 / Alive x Dispose.DifferentThread
            var thread = new Thread(handle.Dispose);
            thread.Start();
            thread.Join();

            Assert.That(handle.IsDisposed, Is.True);
            Assert.That(BlackboxTestDoubles.WarningLogs.Single(), Does.Contain("different thread"));
        }

        [Test]
        [RunWithUniTest]
        public void PrintedDisposeDoesNotMerge()
        {
            var source = BlackboxTestDoubles.Blackbox("source");
            var target = BlackboxTestDoubles.Blackbox("target");
            var handle = BlackboxTestDoubles.Exert(source, target, "call", "Send");
            BlackboxTestDoubles.Scope(target, "scope", "Receive");
            Assert.That(BlackboxSessionRegistry.Current.State.TryFreeze(new CrashInfo("test", string.Empty)), Is.True);

            // Table 2-3 / Alive x Dispose.Printed
            handle.Dispose();

            var logs = BlackboxTestDoubles.Logs(target);
            Assert.That(handle.IsDisposed, Is.True);
            Assert.That(logs.Count, Is.EqualTo(2));
            Assert.That(logs[0].ScopeType, Is.EqualTo(ScopeType.None));
            Assert.That(logs[1].ScopeType, Is.EqualTo(ScopeType.Open));
        }
    }
}
