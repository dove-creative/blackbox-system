using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    internal sealed class ScopeHandleTests : BlackboxTestBase
    {
        [Test]
        public void DefaultHandleOperationsDoNothing()
        {
            // Table 2-2 / Default x With.ValidTarget
            var withResult = default(ScopeHandle).With(BlackboxTestDoubles.Owner("target"));

            // Table 2-2 / Default x Dispose.SameThread
            default(ScopeHandle).Dispose();

            Assert.That(withResult.IsDisposed, Is.True);
            Assert.That(withResult.ScopeId, Is.EqualTo(-1));
            Assert.That(BlackboxRegistry.Count(), Is.EqualTo(0));
            Assert.That(BlackboxTestDoubles.WarningLogs, Is.Empty);
        }

        [Test]
        public void AliveDisposeClosesScope()
        {
            var blackbox = BlackboxTestDoubles.Blackbox("owner");
            var handle = blackbox.WriteScope("scope", "Run");

            Assert.That(handle.IsAlive, Is.True);

            // Table 2-2 / Alive x Dispose.SameThread
            handle.Dispose();

            var logs = BlackboxTestDoubles.Logs(blackbox);
            Assert.That(handle.IsDisposed, Is.True);
            Assert.That(logs.Count, Is.EqualTo(2));
            Assert.That(logs[0].ScopeType, Is.EqualTo(ScopeType.Open));
            Assert.That(logs[1].ScopeType, Is.EqualTo(ScopeType.Close));
        }

        [Test]
        public void DisposedDisposeDoesNotAddLogAgain()
        {
            var blackbox = BlackboxTestDoubles.Blackbox("owner");
            var handle = blackbox.WriteScope("scope", "Run");
            handle.Dispose();
            var countAfterFirstDispose = BlackboxTestDoubles.Logs(blackbox).Count;

            // Table 2-2 / Disposed x Dispose.SameThread
            handle.Dispose();

            Assert.That(BlackboxTestDoubles.Logs(blackbox).Count, Is.EqualTo(countAfterFirstDispose));
        }

        [Test]
        public void WithAddsTagsToOpenLog()
        {
            var blackbox = BlackboxTestDoubles.Blackbox("owner");
            var targetOwner = BlackboxTestDoubles.Owner("target");
            var target = BlackboxTestDoubles.BlackboxFor(targetOwner);

            // Table 2-2 / Alive x With.ValidTarget
            var handle = blackbox.WriteScope("scope %0", "Run").With(targetOwner, TargetTypes.Full);

            Assert.That(handle.IsAlive, Is.True);
            Assert.That(BlackboxTestDoubles.Lines(blackbox).Single(), Does.Contain("target"));
            Assert.That(BlackboxTestDoubles.Lines(target).Single(), Does.Contain("tagged by"));
        }

        [Test]
        public void DifferentThreadDisposeWarns()
        {
            var blackbox = BlackboxTestDoubles.Blackbox("owner");
            var handle = blackbox.WriteScope("scope", "Run");

            // Table 2-2 / Alive x Dispose.DifferentThread
            var thread = new Thread(handle.Dispose);
            thread.Start();
            thread.Join();

            Assert.That(handle.IsDisposed, Is.True);
            Assert.That(BlackboxTestDoubles.WarningLogs.Any(log => log.Contains("different thread")), Is.True);
            Assert.That(BlackboxTestDoubles.Logs(blackbox).Last().ScopeType, Is.EqualTo(ScopeType.Close));
        }

        [Test]
        public void PrintedDisposeDoesNotCloseScope()
        {
            var blackbox = BlackboxTestDoubles.Blackbox("owner");
            var handle = blackbox.WriteScope("scope", "Run");
            Assert.That(Infrastructure.TryMarkPrinted(), Is.True);

            // Table 2-2 / Alive x Dispose.Printed
            handle.Dispose();

            var logs = BlackboxTestDoubles.Logs(blackbox);
            Assert.That(handle.IsDisposed, Is.True);
            Assert.That(logs.Count, Is.EqualTo(1));
            Assert.That(logs[0].ScopeType, Is.EqualTo(ScopeType.Open));
        }
    }
}
