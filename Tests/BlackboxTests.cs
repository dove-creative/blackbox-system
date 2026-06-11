using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace com.BlackThunder.BlackboxSystem.Tests
{
#if BLACKBOX
    internal sealed class BlackboxTests : BlackboxTestBase
    {
        [Test]
        public void WriteStoresSelfLog()
        {
            var blackbox = BlackboxTestDoubles.Blackbox("owner");

            // Table 4-2 / Ready x Write
            blackbox.Write("hello", "Run");

            var log = BlackboxTestDoubles.Logs(blackbox).Single();
            Assert.That(log.Message, Is.EqualTo("hello"));
            Assert.That(log.Owner, Is.SameAs(blackbox));
        }

        [Test]
        public void WriteScopeReturnsScopeHandleAndOpenLog()
        {
            var blackbox = BlackboxTestDoubles.Blackbox("owner");

            // Table 4-2 / Ready x WriteScope
            var handle = blackbox.WriteScope("scope", "Run");

            var log = BlackboxTestDoubles.Logs(blackbox).Single();
            Assert.That(handle.IsAlive, Is.True);
            Assert.That(log.ScopeType, Is.EqualTo(ScopeType.Open));
            Assert.That(log.Message, Is.EqualTo("scope"));
        }

        [Test]
        public void ExertMessageRejectsNullTarget()
        {
            var blackbox = BlackboxTestDoubles.Blackbox("owner");

            // Table 4-2 / Ready x ExertMessage.OtherNull
            Assert.That(() => blackbox.ExertMessage(null, "call", "Run"), Throws.TypeOf<System.ArgumentNullException>());
        }

        [Test]
        public void ExertMessageStoresSelfInteraction()
        {
            var blackbox = BlackboxTestDoubles.Blackbox("owner");

            // Table 4-2 / Ready x ExertMessage.OtherSelf
            blackbox.ExertMessage(blackbox, "self", "Run");

            var log = BlackboxTestDoubles.Logs(blackbox).Single();
            Assert.That(log.InteractionId, Is.GreaterThanOrEqualTo(0));
            Assert.That(log.ExertedBy, Is.SameAs(blackbox));
            Assert.That(log.ExertingTo, Is.SameAs(blackbox));
        }

        [Test]
        public void ExertMessageStoresTwoSidedPeerInteraction()
        {
            var source = BlackboxTestDoubles.Blackbox("source");
            var target = BlackboxTestDoubles.Blackbox("target");

            // Table 4-2 / Ready x ExertMessage.OtherPeer
            source.ExertMessage(target, "call", "Run");

            var sourceLog = BlackboxTestDoubles.Logs(source).Single();
            var targetLog = BlackboxTestDoubles.Logs(target).Single();
            Assert.That(sourceLog.InteractionId, Is.EqualTo(targetLog.InteractionId));
            Assert.That(sourceLog.ExertingTo, Is.SameAs(target));
            Assert.That(targetLog.ExertedBy, Is.SameAs(source));
        }

        [Test]
        public void ExertReturnsHandleForPeerInteraction()
        {
            var source = BlackboxTestDoubles.Blackbox("source");
            var target = BlackboxTestDoubles.Blackbox("target");

            // Table 4-2 / Ready x Exert.OtherPeer
            var handle = source.Exert(target, "call", "Run");

            Assert.That(handle.IsAlive, Is.True);
            Assert.That(BlackboxTestDoubles.Logs(source).Single().InteractionId, Is.GreaterThanOrEqualTo(0));
            Assert.That(BlackboxTestDoubles.Logs(target).Single().InteractionId, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void PrintedStateStopsNewLogs()
        {
            var blackbox = BlackboxTestDoubles.Blackbox("owner");
            blackbox.Write("before", "Run");
            Assert.That(Infrastructure.TryMarkPrinted(), Is.True);

            // Table 4-2 / Printed x Write
            var message = blackbox.Write("after", "Run").With();

            // Table 4-2 / Printed x WriteScope
            var scope = blackbox.WriteScope("scope", "Run");

            // Table 4-2 / Printed x ExertMessage.OtherSelf
            var exertMessage = blackbox.ExertMessage(blackbox, "call", "Run");

            // Table 4-2 / Printed x Exert.OtherSelf
            var exert = blackbox.Exert(blackbox, "call", "Run");

            Assert.That(message, Is.EqualTo("after"));
            Assert.That(scope.IsDisposed, Is.True);
            Assert.That(exertMessage, Is.EqualTo("call"));
            Assert.That(exert.IsDisposed, Is.True);
            Assert.That(BlackboxTestDoubles.Logs(blackbox).Count, Is.EqualTo(1));
        }

        [Test]
        public void GetLogsSortsAcrossThreadContextsBySequence()
        {
            var blackbox = BlackboxTestDoubles.Blackbox("owner");
            blackbox.Write("main", "Run");
            var thread = new Thread(() => blackbox.Write("worker", "Run"));
            thread.Start();
            thread.Join();

            // Table 4-2 / Ready x GetLogs.MultiContext
            var logs = blackbox.GetLogs();

            Assert.That(logs.Select(log => log.Sequence).ToArray(), Is.Ordered);
            Assert.That(logs.Select(log => log.Message).ToList(), Is.EquivalentTo(new List<string> { "main", "worker" }));
        }

        [Test]
        public void GetLogsByContextReturnsSeparateThreadBuckets()
        {
            var blackbox = BlackboxTestDoubles.Blackbox("owner");
            blackbox.Write("main", "Run");
            var thread = new Thread(() => blackbox.Write("worker", "Run"));
            thread.Start();
            thread.Join();

            // Table 4-2 / Ready x GetLogsByContext
            var contexts = blackbox.GetLogsByContext();

            Assert.That(contexts.Length, Is.EqualTo(2));
            Assert.That(contexts.Sum(context => context.Count), Is.EqualTo(2));
        }

        [Test]
        public void OwnerStringFallsBackWhenOwnerReferenceIsLost()
        {
            var weakCase = CreateWeakOwnerCase();

            BlackboxTestDoubles.ForceFullCollection();

            // Table 4-2 / OwnerReferenceLost x GetLogs
            if (weakCase.WeakReference.IsAlive)
            {
                Assert.That(weakCase.Blackbox.OwnerString, Is.EqualTo("weak-owner"));
                Assert.That(weakCase.Blackbox.GetLogs().Single().ToString(), Does.Contain("weak-owner"));
                return;
            }

            Assert.That(weakCase.Blackbox.OwnerString, Does.Contain("weak-owner"));
            Assert.That(weakCase.Blackbox.OwnerString, Does.Contain("Reference Lost"));
            Assert.That(weakCase.Blackbox.GetLogs().Single().ToString(), Does.Contain("weak-owner"));
        }

        private static WeakOwnerCase CreateWeakOwnerCase()
        {
            var owner = BlackboxTestDoubles.Owner("weak-owner");
            var weakReference = new System.WeakReference(owner);
            var blackbox = BlackboxRegistry.GetBlackbox(owner);
            blackbox.ExertMessage(blackbox, "message", "Run");
            owner = null;

            return new WeakOwnerCase(weakReference, blackbox);
        }

        private readonly struct WeakOwnerCase
        {
            public readonly System.WeakReference WeakReference;
            public readonly Blackbox Blackbox;

            public WeakOwnerCase(System.WeakReference weakReference, Blackbox blackbox)
            {
                WeakReference = weakReference;
                Blackbox = blackbox;
            }
        }
    }
#endif
}
