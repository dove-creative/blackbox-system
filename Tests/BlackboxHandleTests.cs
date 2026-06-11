using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace com.BlackThunder.BlackboxSystem.Tests
{
    internal sealed class BlackboxHandleTests : BlackboxTestBase
    {
#if BLACKBOX
        [Test]
        public void OfCreatesValidHandleAndRejectsNull()
        {
            var owner = BlackboxTestDoubles.Owner("owner");

            // Table 5-1 / Valid x Of.ValidSubject
            var handle = BlackboxHandle.Of(owner);

            Assert.That(handle.IsValid, Is.True);
            Assert.That(handle.Owner, Is.SameAs(owner));
            Assert.That(handle.OwnerString, Is.EqualTo("owner"));
            Assert.That(handle.Id, Is.EqualTo(0));

            // Table 5-1 / Valid x Of.NullSubject
            Assert.That(() => BlackboxHandle.Of<object>(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void InvalidHandleReturnsFallbacks()
        {
            var invalid = default(BlackboxHandle);
            var other = BlackboxTestDoubles.Owner("other");

            // Table 5-1 / Default.Invalid x Write
            Assert.That(invalid.Write("message").With(), Is.EqualTo("message"));

            // Table 5-1 / Default.Invalid x WriteScope
            Assert.That(invalid.Scope("scope").IsDisposed, Is.True);

            // Table 5-1 / Default.Invalid x ExertMessage.OtherPeer
            Assert.That(invalid.ExertMessage(other, "call"), Is.EqualTo("call"));

            // Table 5-1 / Default.Invalid x Exert.OtherPeer
            Assert.That(invalid.Exert(other, "call").IsDisposed, Is.True);

            // Table 5-1 / Default.Invalid x WriteError.ErrorTargetsEmpty
            Assert.That(invalid.WriteError("error"), Is.EqualTo("error"));

            Assert.That(invalid.IsValid, Is.False);
            Assert.That(invalid.Owner, Is.Null);
            Assert.That(invalid.Id, Is.EqualTo(-1));
            Assert.That(BlackboxRegistry.Count(), Is.EqualTo(0));
        }

        [Test]
        public void ConstructWritesCtorScopeAndCachesHandle()
        {
            var owner = BlackboxTestDoubles.Owner("owner");
            var handle = BlackboxHandle.Of(owner);

            // Table 5-1 / Valid x Construct
            var scope = handle.Construct("created", out var cachedHandle, "Ctor");

            var logs = BlackboxTestDoubles.Logs(BlackboxRegistry.GetBlackbox(owner));
            Assert.That(scope.IsAlive, Is.True);
            Assert.That(cachedHandle.IsValid, Is.True);
            Assert.That(cachedHandle.Id, Is.EqualTo(handle.Id));
            Assert.That(logs.Single().ScopeType, Is.EqualTo(ScopeType.Open));
            Assert.That(logs.Single().Message, Does.Contain("[Ctor: owner]"));
            Assert.That(logs.Single().Message, Does.Contain("created"));
        }

        [Test]
        public void WhenReturnsValidOrSkippedHandle()
        {
            var handle = BlackboxHandle.Of(BlackboxTestDoubles.Owner("owner"));

            // Table 5-1 / Valid x When.True
            Assert.That(handle.When(true).IsValid, Is.True);
            Assert.That(handle.When(() => true).IsValid, Is.True);

            // Table 5-1 / Valid x When.False
            Assert.That(handle.When(false).IsValid, Is.False);
            Assert.That(handle.When(() => false).IsValid, Is.False);

            Assert.That(() => handle.When(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void DisposeWritesDisposedLog()
        {
            var owner = BlackboxTestDoubles.Owner("owner");
            var handle = BlackboxHandle.Of(owner);

            // Table 5-1 / Valid x Dispose
            var next = handle.Dispose("done", "DisposeCall");

            var log = BlackboxTestDoubles.Logs(BlackboxRegistry.GetBlackbox(owner)).Single();
            Assert.That(next.IsValid, Is.True);
            Assert.That(log.Message, Does.Contain("[Disposed: owner]"));
            Assert.That(log.Message, Does.Contain("done"));
        }

        [Test]
        public void WriteReturnsMessageAndStoresLog()
        {
            var owner = BlackboxTestDoubles.Owner("owner");
            var handle = BlackboxHandle.Of(owner);

            // Table 5-1 / Valid x Write
            var message = handle.Write("hello", "Run").With();

            var log = BlackboxTestDoubles.Logs(BlackboxRegistry.GetBlackbox(owner)).Single();
            Assert.That(message, Is.EqualTo("hello"));
            Assert.That(log.Message, Is.EqualTo("hello"));
        }

        [Test]
        public void WriteScopeReturnsAliveScopeHandle()
        {
            var owner = BlackboxTestDoubles.Owner("owner");
            var handle = BlackboxHandle.Of(owner);

            // Table 5-1 / Valid x WriteScope
            var scope = handle.Scope("scope", "Run");

            var log = BlackboxTestDoubles.Logs(BlackboxRegistry.GetBlackbox(owner)).Single();
            Assert.That(scope.IsAlive, Is.True);
            Assert.That(log.ScopeType, Is.EqualTo(ScopeType.Open));
        }

        [Test]
        public void ExertMessageRejectsNullAndStoresPeerInteraction()
        {
            var sourceOwner = BlackboxTestDoubles.Owner("source");
            var targetOwner = BlackboxTestDoubles.Owner("target");
            var handle = BlackboxHandle.Of(sourceOwner);

            // Table 5-1 / Valid x ExertMessage.OtherNull
            Assert.That(() => handle.ExertMessage<object>(null, "call"), Throws.TypeOf<ArgumentNullException>());

            // Table 5-1 / Valid x ExertMessage.OtherPeer
            Assert.That(handle.ExertMessage(targetOwner, "call", "Run"), Is.EqualTo("call"));

            Assert.That(BlackboxTestDoubles.Logs(BlackboxRegistry.GetBlackbox(sourceOwner)).Single().ExertingTo.Owner, Is.SameAs(targetOwner));
            Assert.That(BlackboxTestDoubles.Logs(BlackboxRegistry.GetBlackbox(targetOwner)).Single().ExertedBy.Owner, Is.SameAs(sourceOwner));
        }

        [Test]
        public void ExertRejectsNullAndReturnsHandleForPeer()
        {
            var sourceOwner = BlackboxTestDoubles.Owner("source");
            var targetOwner = BlackboxTestDoubles.Owner("target");
            var handle = BlackboxHandle.Of(sourceOwner);

            // Table 5-1 / Valid x Exert.OtherNull
            Assert.That(() => handle.Exert<object>(null, "call"), Throws.TypeOf<ArgumentNullException>());

            // Table 5-1 / Valid x Exert.OtherPeer
            var exert = handle.Exert(targetOwner, "call", "Run");

            Assert.That(exert.IsAlive, Is.True);
            Assert.That(BlackboxTestDoubles.Logs(BlackboxRegistry.GetBlackbox(targetOwner)).Single().ExertedBy.Owner, Is.SameAs(sourceOwner));
        }

        [Test]
        public void WriteErrorRecordsErrorAndTargets()
        {
            var owner = BlackboxTestDoubles.Owner("owner");
            var targetOwner = BlackboxTestDoubles.Owner("target");
            var handle = BlackboxHandle.Of(owner);

            // Table 5-1 / Valid x WriteError.ErrorTargetsEmpty
            Assert.That(handle.WriteError("empty", methodName: "Run"), Is.EqualTo("empty"));

            // Table 5-1 / Valid x WriteError.ErrorTargetsPresent
            Assert.That(handle.WriteError("targeted", new BlackboxHandle.ErrorContainer(("target", targetOwner)), methodName: "Run"), Is.EqualTo("targeted"));

            var ownerLines = BlackboxTestDoubles.Lines(BlackboxRegistry.GetBlackbox(owner));
            var targetLines = BlackboxTestDoubles.Lines(BlackboxRegistry.GetBlackbox(targetOwner));
            Assert.That(ownerLines.Any(line => line.Contains("[Error] empty")), Is.True);
            Assert.That(ownerLines.Any(line => line.Contains("target:")), Is.True);
            Assert.That(targetLines.Single(), Does.Contain("tagged by"));
        }

        [Test]
        public void WriteErrorCanTriggerCrashExport()
        {
            var directory = BlackboxTestDoubles.CreateTempDirectory();
            BlackboxTestDoubles.Reset(logDirectory: directory);
            var handle = BlackboxHandle.Of(BlackboxTestDoubles.Owner("owner"));

            // Table 5-1 / Valid x WriteError.ExceptionHandlingCrashExport
            var message = handle.WriteError("boom", exceptionHandlingOption: ExceptionHandlingOption.CrashExport, methodName: "Run");

            Assert.That(message, Is.EqualTo("boom"));
            Assert.That(BlackboxTestDoubles.WarningLogs.Single(), Does.Contain("[Blackbox] CRASH: boom"));
            Assert.That(BlackboxTestDoubles.GetFiles(directory, "[CRASH]*.html").Length, Is.EqualTo(1));
        }

        [Test]
        public void CrashExportWritesStackTraceAndExportsOnce()
        {
            var directory = BlackboxTestDoubles.CreateTempDirectory();
            BlackboxTestDoubles.Reset(logDirectory: directory);
            var handle = BlackboxHandle.Of(BlackboxTestDoubles.Owner("owner"));

            // Table 5-1 / Valid x CrashExport
            var message = handle.CrashExport("boom", openLog: OpenLogOption.Never, methodName: "Run");

            var file = BlackboxTestDoubles.GetFiles(directory, "[CRASH]*.html").Single();
            var html = File.ReadAllText(file);
            Assert.That(message, Is.EqualTo("boom"));
            Assert.That(html, Does.Contain("[STACK TRACE]"));
            Assert.That(Infrastructure.IsPrinted, Is.True);

            // Table 5-1 / Printed x CrashExport
            handle.CrashExport("again", openLog: OpenLogOption.Never, methodName: "Run");
            Assert.That(BlackboxTestDoubles.GetFiles(directory, "[CRASH]*.html").Length, Is.EqualTo(1));
        }

        [Test]
        public void ExportWarnsForInvalidOrDuplicateExport()
        {
            var directory = BlackboxTestDoubles.CreateTempDirectory();
            BlackboxTestDoubles.Reset(logDirectory: directory);

            // Table 5-1 / Default.Invalid x Export.FirstExport
            default(BlackboxHandle).Export(openLogOption: OpenLogOption.Never);
            Assert.That(BlackboxTestDoubles.WarningLogs.Last(), Does.Contain("invalid"));

            var handle = BlackboxHandle.Of(BlackboxTestDoubles.Owner("owner"));
            handle.Write("hello", "Run");

            // Table 5-1 / Valid x Export.FirstExport
            handle.Export(format: ExportFormat.Txt, openLogOption: OpenLogOption.Never);
            Assert.That(BlackboxTestDoubles.GetFiles(directory, "*.txt").Length, Is.EqualTo(1));

            // Table 5-1 / Printed x Export.DuplicateExport
            handle.Export(format: ExportFormat.Txt, openLogOption: OpenLogOption.Never);
            Assert.That(BlackboxTestDoubles.WarningLogs.Last(), Does.Contain("already been exported"));
        }

        [Test]
        public void ForceResetRestoresDefaultRuntimeState()
        {
            var directory = BlackboxTestDoubles.CreateTempDirectory();
            BlackboxTestDoubles.Reset(logDirectory: directory);
            var owner = BlackboxTestDoubles.Owner("owner");
            var handle = BlackboxHandle.Of(owner);
            handle.Write("before", "Run");
            handle.Export(format: ExportFormat.Txt, openLogOption: OpenLogOption.Never);

            // Table 5-1 / Valid x ForceReset
            BlackboxHandle.ForceReset();

            Assert.That(Infrastructure.IsPrinted, Is.False);
            Assert.That(BlackboxRegistry.Count(), Is.EqualTo(0));
            Assert.That(BlackboxHandle.Of(owner).Write("after", "Run").With(), Is.EqualTo("after"));
        }

        [Test]
        public void WriteHandlerFormatsInterpolatedValuesForValidHandle()
        {
            var owner = BlackboxTestDoubles.Owner("owner");
            var handle = BlackboxHandle.Of(owner);

            // Table 1-3 / Active x Construct.ValidHandle
            var handler = new BlackboxHandle.WriteHandler(8, 2, handle, out var shouldLog);

            Assert.That(shouldLog, Is.True);
            Assert.That(handler.ShouldLog, Is.True);

            // Table 1-3 / Active x AppendLiteral.Literal
            handler.AppendLiteral("value ");

            // Table 1-3 / Active x AppendFormatted.Value
            handler.AppendFormatted(7);
            handler.AppendLiteral(" ");

            // Table 1-3 / Active x AppendFormatted.NullValue
            handler.AppendFormatted<object>(null);

            // Table 1-3 / Active x GetTextAndClear
            Assert.That(handler.GetTextAndClear(), Is.EqualTo("value 7 null"));
            Assert.That(handler.GetTextAndClear(), Is.EqualTo(string.Empty));
        }

        [Test]
        public void WriteHandlerSkipsFormattingForInvalidHandle()
        {
            var invalid = default(BlackboxHandle);
            var formatCalls = 0;

            // Table 1-3 / Skipped x Construct.InvalidHandle
            var handler = new BlackboxHandle.WriteHandler(8, 1, invalid, out var shouldLog);

            Assert.That(shouldLog, Is.False);
            Assert.That(handler.ShouldLog, Is.False);

            // Table 1-3 / Skipped x AppendLiteral.Literal
            handler.AppendLiteral("value ");

            // Table 1-3 / Skipped x AppendFormatted.Value
            handler.AppendFormatted(7);

            // Table 1-3 / Skipped x GetTextAndClear
            Assert.That(handler.GetTextAndClear(), Is.EqualTo(string.Empty));
            Assert.That(invalid.Write(ref handler, "Run").With(), Is.EqualTo(string.Empty));

            // Table 1-3 / Skipped x AppendFormatted.Value
            Assert.That(invalid.Write($"value {FormatValue()}", "Run").With(), Is.EqualTo(string.Empty));
            Assert.That(formatCalls, Is.EqualTo(0));
            Assert.That(BlackboxRegistry.Count(), Is.EqualTo(0));

            string FormatValue()
            {
                formatCalls++;
                return "formatted";
            }
        }
#else
        [Test]
        public void SymbolOffReturnsInvalidHandlesAndOriginalMessages()
        {
            var owner = BlackboxTestDoubles.Owner("owner");
            var other = BlackboxTestDoubles.Owner("other");

            // Table 5-1 / SymbolOff x Of.ValidSubject
            var handle = BlackboxHandle.Of(owner);

            Assert.That(handle.IsValid, Is.False);

            // Table 5-1 / SymbolOff x Write
            Assert.That(handle.Write("message").With(), Is.EqualTo("message"));

            // Table 5-1 / SymbolOff x ExertMessage.OtherPeer
            Assert.That(handle.ExertMessage(other, "call"), Is.EqualTo("call"));

            // Table 5-1 / SymbolOff x WriteError.ErrorTargetsEmpty
            Assert.That(handle.WriteError("error"), Is.EqualTo("error"));
        }
#endif
    }
}
