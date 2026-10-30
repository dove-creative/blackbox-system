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
        public void RecordingTaggingConstructingAndExportingNeverCallSubjectToString()
        {
            using var session = BlackboxHandle.BeginSession();
            BlackboxHandle.LogDirectory = BlackboxTestDoubles.CreateTempDirectory();
            var owner = new ExplosiveOwner();
            var target = new ExplosiveOwner();
            using (BlackboxHandle.Of(owner).Label("Forest").Construct(out var handle))
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
        public void LabelsAreSetOnceAndInvalidHandlesCannotSetThem()
        {
            using var session = BlackboxHandle.BeginSession();
            var warnings = new ConcurrentQueue<string>();
            BlackboxHandle.WarningLogger = warnings.Enqueue;
            var owner = new object();
            var handle = BlackboxHandle.Of(owner);
            handle.Label(null).Label(" ");
            Assert.That(handle.OwnerString, Is.EqualTo("Object"));
            Parallel.For(0, 30, i => handle.Label("name " + i));
            var chosen = handle.OwnerString;
            handle.Label(chosen);
            handle.Label("different");
            Assert.That(handle.OwnerString, Is.EqualTo(chosen));
            Assert.That(warnings.Count(s => s.Contains("already assigned")), Is.EqualTo(1));
            session.Dispose();
            handle.Label("after disposal");
            Assert.That(handle.OwnerString, Is.EqualTo(chosen));
            Assert.That(default(BlackboxHandle).Label("ignored").IsValid, Is.False);
        }

        [Test]
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
            var handle = BlackboxHandle.Of(closure.Target).Label("<script>&\"");
            handle.Write("hello");
            handle.Export(format: ExportFormat.Html, openLogOption: OpenLogOption.Never);
            var html = File.ReadAllText(Directory.GetFiles(BlackboxHandle.LogDirectory).Single());
            Assert.That(html, Does.Contain("&lt;script&gt;&amp;&quot;"));
            Assert.That(html, Does.Not.Contain("<script>&\""));
        }

        [Test]
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
        public void StringLabelDoesNotRetainItsWeakStringSubject()
        {
            using var session = BlackboxHandle.BeginSession();
            var value = MakeStringSubject();
            BlackboxTestDoubles.ForceFullCollection();
            Assert.That(value.Weak.IsAlive, Is.False);
            Assert.That(value.Handle.OwnerString, Is.EqualTo(new string('x', 20)));
        }

        [Test]
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
        public void LivingSubjectDoesNotRetainItsFormerSession()
        {
            var subject = new object();
            var weak = MakeDisposedSession(subject);
            BlackboxTestDoubles.ForceFullCollection();
            Assert.That(weak.IsAlive, Is.False);
            GC.KeepAlive(subject);
        }

        [Test]
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
            return (new WeakReference(owner), BlackboxHandle.Of(owner).Label("remembered"));
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (WeakReference Weak, BlackboxHandle Handle) MakeStringSubject()
        {
            var owner = new string('x', 20);
            return (new WeakReference(owner), BlackboxHandle.Of(owner).Label(owner));
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
