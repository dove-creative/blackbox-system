using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace com.BlackThunder.BlackboxSystem.Tests
{
#if BLACKBOX
    internal sealed class ExertHandleTests : BlackboxTestBase
    {
        [Test]
        public void DefaultDisposeDoesNothing()
        {
            // Table 2-3 / Default x Dispose.MergeTargetExists
            default(ExertHandle).Dispose();

            Assert.That(BlackboxRegistry.Count(), Is.EqualTo(0));
            Assert.That(BlackboxTestDoubles.WarningLogs, Is.Empty);
        }

        [Test]
        public void DisposeMergesAdjacentTargetScope()
        {
            var source = BlackboxTestDoubles.Blackbox("source");
            var target = BlackboxTestDoubles.Blackbox("target");
            var handle = source.Exert(target, "call", "Send");
            target.WriteScope("scope", "Receive");

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
        public void DisposeWithoutMergeTargetOnlyDisposes()
        {
            var source = BlackboxTestDoubles.Blackbox("source");
            var target = BlackboxTestDoubles.Blackbox("target");
            var handle = source.Exert(target, "call", "Send");

            // Table 2-3 / Alive x Dispose.MergeTargetMissing
            handle.Dispose();

            var logs = BlackboxTestDoubles.Logs(target);
            Assert.That(handle.IsDisposed, Is.True);
            Assert.That(logs.Count, Is.EqualTo(1));
            Assert.That(logs[0].InteractionId, Is.GreaterThanOrEqualTo(0));
            Assert.That(logs[0].ScopeType, Is.EqualTo(ScopeType.None));
        }

        [Test]
        public void DisposedDisposeDoesNotMergeAgain()
        {
            var source = BlackboxTestDoubles.Blackbox("source");
            var target = BlackboxTestDoubles.Blackbox("target");
            var handle = source.Exert(target, "call", "Send");
            target.WriteScope("scope", "Receive");
            handle.Dispose();
            var countAfterFirstDispose = BlackboxTestDoubles.Logs(target).Count;

            // Table 2-3 / Disposed x Dispose.MergeTargetExists
            handle.Dispose();

            Assert.That(BlackboxTestDoubles.Logs(target).Count, Is.EqualTo(countAfterFirstDispose));
        }

        [Test]
        public void DifferentThreadDisposeWarns()
        {
            var source = BlackboxTestDoubles.Blackbox("source");
            var target = BlackboxTestDoubles.Blackbox("target");
            var handle = source.Exert(target, "call", "Send");

            // Table 2-3 / Alive x Dispose.DifferentThread
            var thread = new Thread(handle.Dispose);
            thread.Start();
            thread.Join();

            Assert.That(handle.IsDisposed, Is.True);
            Assert.That(BlackboxTestDoubles.WarningLogs.Single(), Does.Contain("different thread"));
        }

        [Test]
        public void PrintedDisposeDoesNotMerge()
        {
            var source = BlackboxTestDoubles.Blackbox("source");
            var target = BlackboxTestDoubles.Blackbox("target");
            var handle = source.Exert(target, "call", "Send");
            target.WriteScope("scope", "Receive");
            Assert.That(Infrastructure.TryMarkPrinted(), Is.True);

            // Table 2-3 / Alive x Dispose.Printed
            handle.Dispose();

            var logs = BlackboxTestDoubles.Logs(target);
            Assert.That(handle.IsDisposed, Is.True);
            Assert.That(logs.Count, Is.EqualTo(2));
            Assert.That(logs[0].ScopeType, Is.EqualTo(ScopeType.None));
            Assert.That(logs[1].ScopeType, Is.EqualTo(ScopeType.Open));
        }
    }
#endif
}
