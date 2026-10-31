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
    internal sealed class IdentityLifetimeTests : BlackboxTestBase
    {
        private sealed class ExplosiveOwner
        {
            internal int Calls;
            public override string ToString() { Interlocked.Increment(ref Calls); throw new InvalidOperationException("must not run"); }
        }
        private sealed class Outer<T> { public sealed class Inner<U> { } }

        [Test]
        [RunWithUniTest]
        public void RecordingTaggingConstructingAndExportingNeverCallSubjectToString()
        {
            using var session = BlackboxHandle.BeginSession();
            BlackboxHandle.LogDirectory = BlackboxTestDoubles.CreateTempDirectory();
            var owner = new ExplosiveOwner();
            var target = new ExplosiveOwner();
            using (BlackboxHandle.Of(owner, label: "Forest").Construct(out var handle))
            {
                handle.Write("uses %0").With(target);
                handle.Exert(target, "call").Dispose();
                handle.Dispose("done");
                handle.Export(format: ExportFormat.Txt, openLogOption: OpenLogOption.Never);
                handle.Export(format: ExportFormat.Html, openLogOption: OpenLogOption.Never);
            }
            Assert.That(owner.Calls + target.Calls, Is.Zero);
            Assert.That(File.ReadAllText(Directory.GetFiles(BlackboxHandle.LogDirectory, "*.txt").Single()),
                Does.Contain("Forest (IdentityLifetimeTests.ExplosiveOwner #0)"));
        }

        [Test]
        [RunWithUniTest]
        public void OfLabelsAreSetOnceUnderConcurrentLookup()
        {
            using var session = BlackboxHandle.BeginSession();
            var warnings = new ConcurrentQueue<string>();
            BlackboxHandle.WarningLogger = warnings.Enqueue;
            var owner = new object();
            var handle = BlackboxHandle.Of(owner);
            BlackboxHandle.Of(owner, label: null);
            BlackboxHandle.Of(owner, label: " ");
            Assert.That(handle.OwnerString, Is.EqualTo("Object"));
            Parallel.For(0, 30, i => BlackboxHandle.Of(owner, label: "name " + i));
            var chosen = handle.OwnerString;
            Assert.That(chosen, Does.StartWith("name "));
            BlackboxHandle.Of(owner, label: chosen);
            BlackboxHandle.Of(owner, label: "different");
            Assert.That(handle.OwnerString, Is.EqualTo(chosen));
            Assert.That(warnings.Count(s => s.Contains("already assigned")), Is.EqualTo(1));
            Assert.That(BlackboxSessionRegistry.Count(), Is.EqualTo(1));
        }

        [Test]
        [RunWithUniTest]
        public void OfLabelPreservesSubjectIdentityAndExistingRecords()
        {
            using var session = BlackboxHandle.BeginSession();
            var warnings = new ConcurrentQueue<string>();
            BlackboxHandle.WarningLogger = warnings.Enqueue;
            var owner = new object();
            var original = BlackboxHandle.Of(owner);
            original.Write("before label");
            var labeled = BlackboxHandle.Of(owner, label: "Forest");
            labeled.Write("after label");

            Assert.That(labeled.Id, Is.EqualTo(original.Id));
            Assert.That(labeled.SessionKey, Is.EqualTo(original.SessionKey));
            Assert.That(original.OwnerString, Is.EqualTo("Forest"));
            Assert.That(BlackboxHandle.Of(owner).OwnerString, Is.EqualTo("Forest"));
            foreach (var label in new[] { null, "", " ", "Forest" })
            {
                var repeated = BlackboxHandle.Of(owner, label: label);
                Assert.That(repeated.Id, Is.EqualTo(original.Id));
                Assert.That(repeated.OwnerString, Is.EqualTo("Forest"));
            }
            Assert.That(warnings, Is.Empty);
            Assert.That(BlackboxSessionRegistry.Count(), Is.EqualTo(1));
            Assert.That(BlackboxSessionRegistry.GetBlackbox(owner).GetLogs().Select(log => log.Message),
                Is.EqualTo(new[] { "before label", "after label" }));
        }

        [Test]
        [RunWithUniTest]
        public void OfLabelsAreIsolatedPerSession()
        {
            using var outer = BlackboxHandle.BeginSession();
            var owner = new object();
            var outerHandle = BlackboxHandle.Of(owner, label: "Outer");
            using (BlackboxHandle.BeginSession())
            {
                var innerHandle = BlackboxHandle.Of(owner, label: "Inner");
                Assert.That(innerHandle.SessionKey, Is.Not.EqualTo(outerHandle.SessionKey));
                Assert.That(innerHandle.OwnerString, Is.EqualTo("Inner"));
                Assert.That(outerHandle.OwnerString, Is.EqualTo("Outer"));
            }
            Assert.That(BlackboxHandle.Of(owner).OwnerString, Is.EqualTo("Outer"));
        }

        [Test]
        [RunWithUniTest]
        public void OfDoesNotAssignLabelsWhenRecordingIsDisabled()
        {
            using var session = BlackboxHandle.BeginSession();
            var owner = new object();
            var handle = BlackboxHandle.Of(owner);
            var other = new object();
            BlackboxHandle.UseBlackbox = false;

            Assert.That(BlackboxHandle.Of(owner, label: "ignored").IsValid, Is.False);
            Assert.That(BlackboxHandle.Of(other, label: "ignored").IsValid, Is.False);
            Assert.That(BlackboxHandle.Of<object>(null, label: "ignored").IsValid, Is.False);
            Assert.That(handle.OwnerString, Is.EqualTo("Object"));
            Assert.That(BlackboxSessionRegistry.Count(), Is.EqualTo(1));

            BlackboxHandle.UseBlackbox = true;
            Assert.That(BlackboxHandle.Of(owner, label: "enabled").OwnerString, Is.EqualTo("enabled"));
        }

        [Test]
        [RunWithUniTest]
        public void OfDoesNotAssignLabelsInDisposedSession()
        {
            using var session = BlackboxHandle.BeginSession();
            var owner = new object();
            var handle = BlackboxHandle.Of(owner);
            // Dispose in a child flow so this flow stays bound to the retired session.
            Task.Run(session.Dispose).GetAwaiter().GetResult();

            Assert.That(BlackboxHandle.Of(owner, label: "ignored").IsValid, Is.False);
            Assert.That(BlackboxHandle.Of(new object(), label: "ignored").IsValid, Is.False);
            Assert.That(handle.OwnerString, Is.EqualTo("Object"));
            Assert.That(BlackboxSessionRegistry.Count(), Is.EqualTo(1));
        }

        [Test]
        [RunWithUniTest]
        public void TypeNamesAndHtmlEscapingArePredictable()
        {
            Assert.That(TypeDisplayName.Format(typeof(List<int>)), Is.EqualTo("List<Int32>"));
            Assert.That(TypeDisplayName.Format(typeof(Outer<int>.Inner<string>)),
                Is.EqualTo("IdentityLifetimeTests.Outer<Int32>.Inner<String>"));
            int capture = 3;
            Action closure = () => GC.KeepAlive(capture);
            var generated = closure.Target.GetType();
            Assert.That(TypeDisplayName.Format(generated), Does.EndWith(generated.Name));
            using var session = BlackboxHandle.BeginSession();
            BlackboxHandle.LogDirectory = BlackboxTestDoubles.CreateTempDirectory();
            var handle = BlackboxHandle.Of(closure.Target, label: "<script>&\"");
            handle.Write("hello");
            handle.Export(format: ExportFormat.Html, openLogOption: OpenLogOption.Never);
            var html = File.ReadAllText(Directory.GetFiles(BlackboxHandle.LogDirectory).Single());
            Assert.That(html, Does.Contain("&lt;script&gt;&amp;&quot;"));
            Assert.That(html, Does.Not.Contain("<script>&\""));
        }

        [Test]
        [RunWithUniTest]
        public void ExportIdentitySnapshotDoesNotChangeWhenLabelIsAssignedLater()
        {
            using var session = BlackboxHandle.BeginSession();
            var source = BlackboxSessionRegistry.GetBlackbox(new object());
            var target = BlackboxSessionRegistry.GetBlackbox(new object());
            source.ExertMessage(target, "call", "Run");
            var graph = Tools.BuildExportGraph(source, 3, true);
            target.SetLabel("late label");
            Assert.That(LogFormatter.RenderTextLine(graph[0].Logs.Single(), 0), Does.Not.Contain("late label"));
            Assert.That(graph[0].Names[target], Is.EqualTo("Object #1"));
        }

        [Test]
        [RunWithUniTest]
        public void WeakSubjectCanBeCollectedWhileHandleAndLabelRemain()
        {
            using var session = BlackboxHandle.BeginSession();
            var value = MakeWeakHandle();
            BlackboxTestDoubles.ForceFullCollection();
            Assert.That(value.Weak.IsAlive, Is.False);
            Assert.That(value.Handle.Owner, Is.Null);
            Assert.That(value.Handle.OwnerString, Is.EqualTo("remembered"));
            value.Handle.Write("after GC");
            Assert.That(value.Handle.IsValid, Is.True);
        }

        [Test]
        [RunWithUniTest]
        public void StringLabelDoesNotRetainItsWeakStringSubject()
        {
            using var session = BlackboxHandle.BeginSession();
            var value = MakeStringSubject();
            BlackboxTestDoubles.ForceFullCollection();
            Assert.That(value.Weak.IsAlive, Is.False);
            Assert.That(value.Handle.OwnerString, Is.EqualTo(new string('x', 20)));
        }

        [Test]
        [RunWithUniTest]
        public void StrongSubjectRemainsWhileItsDisposedHandleIsRetained()
        {
            var session = BlackboxHandle.BeginSession();
            BlackboxHandle.StrongReference = true;
            var value = MakeWeakHandle();
            session.Dispose();
            BlackboxTestDoubles.ForceFullCollection();
            Assert.That(value.Weak.IsAlive, Is.True);
            Assert.That(value.Handle.Owner, Is.Not.Null);
            GC.KeepAlive(value.Handle);
        }

        [Test]
        [RunWithUniTest]
        public void LivingSubjectDoesNotRetainItsFormerSession()
        {
            var subject = new object();
            var weak = MakeDisposedSession(subject);
            BlackboxTestDoubles.ForceFullCollection();
            Assert.That(weak.IsAlive, Is.False);
            GC.KeepAlive(subject);
        }

        [Test]
        [RunWithUniTest]
        public void CleanupPreservesLivingEntriesAndRetirementReclaimsCollectedContextSlots()
        {
            using var session = BlackboxSessionRegistry.Begin(1);
            var recordingSession = BlackboxSessionRegistry.Current;
            int before = HandleManager<ScopeHandle>.LiveCountForTests;
            var living = new object();
            var canonical = recordingSession.Subjects.GetOrAdd(living);
            var deadContext = MakeAbandonedContext(recordingSession);
            BlackboxTestDoubles.ForceFullCollection();
            recordingSession.Subjects.Cleanup();
            BlackboxTestDoubles.ForceFullCollection();
            Assert.That(deadContext.IsAlive, Is.False);
            Assert.That(recordingSession.Subjects.GetOrAdd(living), Is.SameAs(canonical));
            Assert.That(recordingSession.Subjects.Count(), Is.EqualTo(1));
            Assert.That(HandleManager<ScopeHandle>.LiveCountForTests, Is.EqualTo(before + 1));
            session.Dispose();
            Assert.That(HandleManager<ScopeHandle>.LiveCountForTests, Is.EqualTo(before));
            GC.KeepAlive(living);
        }

        [Test]
        [RunWithUniTest]
        public void ConcurrentTagUpdatesAndReadersDoNotMutatePublishedArrays()
        {
            using var session = BlackboxHandle.BeginSession();
            var owner = new object();
            var box = BlackboxSessionRegistry.GetBlackbox(owner);
            var tag = BlackboxHandle.Of(owner).Write("uses %0");
            var targets = Enumerable.Range(0, 8).Select(_ => new object()).ToArray();
            Parallel.For(0, 200, i =>
            {
                tag.With(targets[i % targets.Length]);
                foreach (var log in box.GetLogs()) if (log.Tags != null) log.Tags[0] = default;
            });
            var result = box.GetLogs().Single();
            Assert.That(result.Tags.Single().Target, Is.Not.Null);
            GC.KeepAlive(targets);
        }

        [Test]
        [RunWithUniTest]
        public void CrossSessionExertDoesNotMergeIntoAnotherSessionsScope()
        {
            using var a = BlackboxHandle.BeginSession();
            var source = BlackboxHandle.Of(new object());
            var target = new object();
            using var b = BlackboxHandle.BeginSession();
            var targetHandle = BlackboxHandle.Of(target);
            var targetBox = BlackboxSessionRegistry.GetBlackbox(target);
            using (source.Exert(target, "from a"))
            using (targetHandle.Scope("in b")) { }
            Assert.That(targetBox.GetLogs().All(log => log.InteractionId == -1), Is.True);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (WeakReference Weak, BlackboxHandle Handle) MakeWeakHandle()
        {
            var owner = new object();
            return (new WeakReference(owner), BlackboxHandle.Of(owner, label: "remembered"));
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (WeakReference Weak, BlackboxHandle Handle) MakeStringSubject()
        {
            var owner = new string('x', 20);
            return (new WeakReference(owner), BlackboxHandle.Of(owner, label: owner));
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference MakeDisposedSession(object subject)
        {
            using var session = BlackboxHandle.BeginSession();
            BlackboxHandle.Of(subject).Write("log");
            return new WeakReference(session);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference MakeAbandonedContext(BlackboxSession recordingSession)
        {
            var box = recordingSession.Subjects.GetOrAdd(new object());
            BlackboxTestDoubles.Scope(box, "abandoned", "Run");
            return new WeakReference(box.LogContexts.Single());
        }
    }
}
