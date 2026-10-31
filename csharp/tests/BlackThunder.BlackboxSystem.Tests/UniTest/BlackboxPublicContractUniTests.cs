using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    internal class BlackboxPublicContractUniTests : BlackboxTestBase
    {
        // Content --------------------
        // Conditional recording
        [RunWithUniTest]
        [TestCase("active")]
        [TestCase("default")]
        [TestCase("disabled")]
        [TestCase("disposed")]
        [TestCase("frozen")]
        public void PredicateConditionsEvaluateOnceAndValidateInEveryHandleState(string state)
        {
            using var session = BlackboxHandle.BeginSession();
            var owner = new object();
            var handle = state == "default" ? default : BlackboxHandle.Of(owner);
            if (state == "disabled") BlackboxHandle.UseBlackbox = false;
            if (state == "disposed") session.Dispose();
            if (state == "frozen")
            {
                BlackboxHandle.LogDirectory = BlackboxTestDoubles.CreateTempDirectory();
                handle.CrashExport("freeze", openLog: OpenLogOption.Never);
            }
            var sequence = session.State.CurrentSequence;
            var calls = 0;

            // Table 5-1 / Any x When: predicates are conditions, independent of recording enablement.
            var selected = handle.When(() => { calls++; return true; });
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(selected.Owner, Is.SameAs(handle.Owner));
            Assert.That(selected.IsValid, Is.EqualTo(state == "active"));
            Assert.That(handle.When(() => { calls++; return false; }).Owner, Is.Null);
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(() => handle.When((Func<bool>)null), Throws.TypeOf<ArgumentNullException>());
            var failure = new InvalidOperationException("predicate failure");
            var thrown = Assert.Throws<InvalidOperationException>(() => handle.When(() => throw failure));
            Assert.That(thrown, Is.SameAs(failure));
            Assert.That(session.State.CurrentSequence, Is.EqualTo(sequence));
            GC.KeepAlive(owner);
        }

        [RunWithUniTest]
        [Test]
        public void DisabledRecordingPreservesFallbacksAndSkipsTargetRegistrationAndCrash()
        {
            using var session = BlackboxHandle.BeginSession();
            var directory = BlackboxTestDoubles.CreateTempDirectory();
            BlackboxHandle.LogDirectory = directory;
            var owner = new object();
            var peer = new object();
            var handle = BlackboxHandle.Of(owner, label: "owner");
            var box = BlackboxSessionRegistry.GetBlackbox(owner);
            BlackboxHandle.UseBlackbox = false;

            // Table 5-1 / UseBlackboxOff x recording: no lookup, records, files or crash transition.
            Assert.That(BlackboxHandle.Of<object>(null, label: "ignored").IsValid, Is.False);
            Assert.That(handle.Construct("ignored", out var cached).IsDisposed, Is.True);
            Assert.That(cached.IsValid, Is.False);
            Assert.That(handle.Dispose("ignored").IsValid, Is.False);
            Assert.That(handle.Write("value %0").With(peer), Is.EqualTo("value %0"));
            Assert.That(handle.Scope("ignored").With(peer).IsDisposed, Is.True);
            Assert.That(handle.ExertMessage<object>(null, "call"), Is.EqualTo("call"));
            Assert.That(handle.Exert(peer, "call").IsDisposed, Is.True);
            Assert.That(handle.WriteError(null, ("peer", peer)), Is.EqualTo("null"));
            Assert.That(handle.WriteError("crash", ("peer", peer), ExceptionHandlingOption.CrashExport), Is.EqualTo("crash"));
            Assert.That(handle.CrashExport(null, ("peer", peer)), Is.EqualTo("null"));
            Assert.That(session.Subjects.Count(), Is.EqualTo(1));
            Assert.That(session.State.CurrentSequence, Is.EqualTo(-1));
            Assert.That(session.State.Crash, Is.Null);
            Assert.That(box.GetLogs(), Is.Empty);
            Assert.That(Directory.GetFiles(directory), Is.Empty);

            // Table 5-1 / UseBlackboxOff -> Valid: the original handle resumes its original identity.
            BlackboxHandle.UseBlackbox = true;
            handle.Write("resumed");
            Assert.That(handle.IsValid, Is.True);
            Assert.That(handle.Owner, Is.SameAs(owner));
            Assert.That(handle.OwnerString, Is.EqualTo("owner"));
            Assert.That(box.GetLogs().Single().Message, Is.EqualTo("resumed"));
            GC.KeepAlive(peer);
        }

        // Configuration
        [RunWithUniTest]
        [Test]
        public void SingleLoggerConfigurationSharesCallbacksAndPreservesUnspecifiedPolicies()
        {
            var messages = new List<string>();
            Action<string> logger = messages.Add;
            BlackboxHandle.Configure("first", logger, true, ExportFormat.Txt, FullExportOption.Focused,
                OpenLogOption.Never, ExceptionHandlingOption.CrashExport, TargetTypes.Message, UseBlackboxOption.DoNotUse);
            BlackboxHandle.MaxLogCount = 7;
            BlackboxHandle.DefaultRecursionDepth = 2;

            // Table 3-3 / Ready x Configure: one callback serves both channels and BasedOnSettings preserves policies.
            BlackboxHandle.Configure("second", logger);
            Assert.That(BlackboxHandle.LogDirectory, Is.EqualTo("second"));
            Assert.That(BlackboxHandle.NormalLogger, Is.SameAs(logger));
            Assert.That(BlackboxHandle.WarningLogger, Is.SameAs(logger));
            Assert.That(BlackboxHandle.StrongReference, Is.False);
            Assert.That(BlackboxHandle.UseBlackbox, Is.False);
            Assert.That(BlackboxHandle.ExportFormat, Is.EqualTo(ExportFormat.Txt));
            Assert.That(BlackboxHandle.FullExportOption, Is.EqualTo(FullExportOption.Focused));
            Assert.That(BlackboxHandle.OpenLogOption, Is.EqualTo(OpenLogOption.Never));
            Assert.That(BlackboxHandle.TagTargetTypes, Is.EqualTo(TargetTypes.Message));
            Assert.That(BlackboxHandle.MaxLogCount, Is.EqualTo(7));
            Assert.That(BlackboxHandle.DefaultRecursionDepth, Is.EqualTo(2));
            Assert.That(BlackboxSessionRegistry.Current.State.Settings.ExceptionHandlingOption, Is.EqualTo(ExceptionHandlingOption.CrashExport));
            Assert.That(Infrastructure.Log("normal"), Is.True);
            Assert.That(Infrastructure.Log("warning", LogLevel.Warning), Is.True);
            Assert.That(messages, Is.EqualTo(new[] { "normal", "warning" }));
        }

        // Error targets and labels
        [RunWithUniTest]
        [TestCase("default")]
        [TestCase("null")]
        [TestCase("empty")]
        public void EmptyErrorContainersRecordNullMessagesWithoutPhantomTargets(string kind)
        {
            var owner = new object();
            var handle = BlackboxHandle.Of(owner);
            var targets = kind == "default"
                ? default(BlackboxHandle.ErrorContainer)
                : new BlackboxHandle.ErrorContainer(kind == "null" ? null : Array.Empty<(string, object)>());

            // Table 5-1 / Valid x WriteError.ErrorTargetsEmpty: default, null and empty containers are equivalent.
            Assert.That(handle.WriteError(null, targets, ExceptionHandlingOption.None), Is.EqualTo("null"));
            var log = BlackboxSessionRegistry.GetBlackbox(owner).GetLogs().Single();
            Assert.That(log.Message, Is.EqualTo("[Error] null"));
            Assert.That(log.Tags, Is.Null);
            Assert.That(BlackboxSessionRegistry.Count(), Is.EqualTo(1));
        }

        [RunWithUniTest]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void TupleErrorTargetsUseDefaultContextAndKeepNullTargetPositions(string context)
        {
            var owner = new object();
            var peer = new object();
            var handle = BlackboxHandle.Of(owner);
            BlackboxHandle.Of(peer, label: "peer");

            // Table 5-1 / Valid x WriteError.ErrorTargetsPresent: tuple conversion and unnamed targets preserve identities.
            handle.WriteError("tuple", (context, peer), ExceptionHandlingOption.None);
            handle.WriteError("many", new BlackboxHandle.ErrorContainer((context, null), ("named", peer)), ExceptionHandlingOption.None);
            var logs = BlackboxSessionRegistry.GetBlackbox(owner).GetLogs().ToArray();
            Assert.That(logs[0].Message, Is.EqualTo("[Error] tuple (target: %0)"));
            Assert.That(logs[0].Tags.Single().Target.Owner, Is.SameAs(peer));
            Assert.That(logs[1].Message, Is.EqualTo("[Error] many (target: %0, named: %1)"));
            Assert.That(logs[1].Tags.Length, Is.EqualTo(2));
            Assert.That(logs[1].Tags[0].Target, Is.Null);
            Assert.That(logs[1].Tags[1].Target.Owner, Is.SameAs(peer));
            Assert.That(logs[1].ToString(), Does.Contain("target: null").And.Contain("named: 'peer"));
            var peerLogs = BlackboxSessionRegistry.GetBlackbox(peer).GetLogs().ToArray();
            Assert.That(peerLogs.Length, Is.EqualTo(2));
            Assert.That(peerLogs.All(log => log.TagTargetTypes == (TargetTypes.Name | TargetTypes.InteractionId)), Is.True);
        }

        [RunWithUniTest]
        [Test]
        public void FrozenSessionRejectsNewLabelsAndSubjectsButRetainsExportIdentity()
        {
            using var session = BlackboxHandle.BeginSession();
            var directory = BlackboxTestDoubles.CreateTempDirectory();
            BlackboxHandle.LogDirectory = directory;
            var owner = new object();
            var handle = BlackboxHandle.Of(owner);
            handle.Write("before freeze");
            handle.CrashExport("freeze", format: ExportFormat.Txt, openLog: OpenLogOption.Never);

            // Table 5-1 / Printed x Of: a frozen session cannot relabel retained history or register another subject.
            Assert.That(BlackboxHandle.Of(owner, label: "ignored label").IsValid, Is.False);
            Assert.That(BlackboxHandle.Of(new object(), label: "ignored subject").Owner, Is.Null);
            Assert.That(handle.OwnerString, Is.EqualTo("Object"));
            Assert.That(session.Subjects.Count(), Is.EqualTo(1));
            handle.Export(format: ExportFormat.Txt, openLogOption: OpenLogOption.Never);
            var files = Directory.GetFiles(directory, "*.txt");
            Assert.That(files.Length, Is.EqualTo(2));
            foreach (var path in files)
                Assert.That(File.ReadAllText(path), Does.Contain("before freeze").And.Not.Contain("ignored label").And.Not.Contain("ignored subject"));
            GC.KeepAlive(owner);
        }

        // Compiler-provided method names
        [RunWithUniTest]
        [Test]
        public void OptionalCallerNamesAndConstructForwardersPreserveTheCallingMethod()
        {
            var owner = new object();
            var peer = new object();
            var handle = BlackboxHandle.Of(owner);

            // Table 5-1 / Valid x Construct/Write/Scope/Exert/WriteError: omitted names bind to the consumer method.
            using (handle.Construct()) { }
            using (handle.Construct(out var cached))
                Assert.That(cached.Owner, Is.SameAs(owner));
            using (handle.Construct(message: "message")) { }
            handle.Write("write");
            using (handle.Scope("scope")) { }
            handle.ExertMessage(peer, "message");
            using (handle.Exert(peer, "call")) { }
            handle.WriteError("error");
            handle.Dispose("disposed");
            var logs = BlackboxSessionRegistry.GetBlackbox(owner).GetLogs().ToArray();
            Assert.That(logs.Length, Is.GreaterThan(10));
            Assert.That(logs.All(log => log.MethodName == nameof(OptionalCallerNamesAndConstructForwardersPreserveTheCallingMethod)), Is.True);
            Assert.That(logs.Count(log => log.Message?.StartsWith("[Ctor:") == true), Is.EqualTo(3));

            // The one-string overload names the method; a construction message must use its named argument.
            using (handle.Construct("explicit constructor")) { }
            var explicitConstructor = BlackboxSessionRegistry.GetBlackbox(owner).GetLogs().ToArray();
            Assert.That(explicitConstructor[logs.Length].MethodName, Is.EqualTo("explicit constructor"));
            Assert.That(explicitConstructor[logs.Length].Message, Does.StartWith("[Ctor:").And.Not.Contain("explicit constructor"));

            // The parameterless Dispose helper deliberately records its own fixed name.
            handle.Dispose();
            Assert.That(BlackboxSessionRegistry.GetBlackbox(owner).GetLogs().Last().MethodName, Is.EqualTo("Dispose"));
            GC.KeepAlive(peer);
        }
    }
}
