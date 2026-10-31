using System.IO;
using System.Linq;
using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    internal sealed class IntegrationTests : BlackboxTestBase
    {
        [Test]
        [RunWithUniTest]
        public void SingleOwnerHistoryExportsReadableFile()
        {
            var directory = BlackboxTestDoubles.CreateTempDirectory();
            BlackboxTestDoubles.Reset(logDirectory: directory);
            var owner = BlackboxTestDoubles.Owner("owner");
            var handle = BlackboxTestDoubles.HandleFor(owner);

            // Table 7 / SingleOwnerHistory x Run
            using (handle.Scope("scope", "Run"))
            {
                handle.Write("inside", "Run");
            }

            handle.Export(format: ExportFormat.Txt, openLogOption: OpenLogOption.Never);

            var text = File.ReadAllText(BlackboxTestDoubles.GetFiles(directory, "*.txt").Single());
            Assert.That(text, Does.Contain("owner"));
            Assert.That(text, Does.Contain("scope"));
            Assert.That(text, Does.Contain("inside"));
            Assert.That(text, Does.Contain("</Run>"));
        }

        [Test]
        [RunWithUniTest]
        public void TwoOwnerInteractionExportsConnectedLogs()
        {
            var directory = BlackboxTestDoubles.CreateTempDirectory();
            BlackboxTestDoubles.Reset(logDirectory: directory);
            var sourceOwner = BlackboxTestDoubles.Owner("source");
            var targetOwner = BlackboxTestDoubles.Owner("target");
            var source = BlackboxTestDoubles.HandleFor(sourceOwner);

            // Table 7 / TwoOwnerInteraction x Run
            using (source.Exert(targetOwner, "call", "Send"))
            using (BlackboxTestDoubles.HandleFor(targetOwner).Scope("receive", "Receive"))
            {
                BlackboxTestDoubles.HandleFor(targetOwner).Write("inside", "Receive");
            }

            source.Export(format: ExportFormat.Txt, fullExportOption: FullExportOption.Full, openLogOption: OpenLogOption.Never);

            var text = File.ReadAllText(BlackboxTestDoubles.GetFiles(directory, "*.txt").Single());
            Assert.That(text, Does.Contain("source"));
            Assert.That(text, Does.Contain("target"));
            Assert.That(text, Does.Contain("call"));
            Assert.That(text, Does.Contain("receive"));
        }

        [Test]
        [RunWithUniTest]
        public void TagTargetFlowWritesSourceAndTargetReferences()
        {
            var sourceOwner = BlackboxTestDoubles.Owner("source");
            var targetOwner = BlackboxTestDoubles.Owner("target");
            var source = BlackboxTestDoubles.HandleFor(sourceOwner);
            BlackboxTestDoubles.HandleFor(targetOwner);

            // Table 7 / TagTargetFlow x Run
            source.Write("hello %0", "Run").With(targetOwner, TargetTypes.Full);
            using (source.Scope("scope %0", "Run").With(targetOwner, TargetTypes.Name))
            {
            }

            var sourceLines = BlackboxTestDoubles.Lines(BlackboxTestDoubles.BlackboxFor(sourceOwner));
            var targetLines = BlackboxTestDoubles.Lines(BlackboxTestDoubles.BlackboxFor(targetOwner));

            Assert.That(sourceLines.Any(line => line.Contains("target")), Is.True);
            Assert.That(targetLines.Count(line => line.Contains("tagged by")), Is.EqualTo(2));
        }

        [Test]
        [RunWithUniTest]
        public void ErrorFlowExportsErrorAndCrashContext()
        {
            var directory = BlackboxTestDoubles.CreateTempDirectory();
            BlackboxTestDoubles.Reset(logDirectory: directory);
            var owner = BlackboxTestDoubles.Owner("owner");
            var target = BlackboxTestDoubles.Owner("target");
            var handle = BlackboxTestDoubles.HandleFor(owner);

            // Table 7 / ErrorFlow x Run
            handle.WriteError("minor", new BlackboxHandle.ErrorContainer(("target", target)), methodName: "Run");
            handle.CrashExport("major", openLog: OpenLogOption.Never, methodName: "Run");

            var html = File.ReadAllText(BlackboxTestDoubles.GetFiles(directory, "[CRASH]*.html").Single());
            Assert.That(html, Does.Contain("[Error] minor"));
            Assert.That(html, Does.Contain("[Error] major"));
            Assert.That(html, Does.Contain("[STACK TRACE]"));
            Assert.That(html, Does.Contain("target"));
        }

        [Test]
        [RunWithUniTest]
        public void RestartDefaultAndRerunStartsClean()
        {
            var directory = BlackboxTestDoubles.CreateTempDirectory();
            BlackboxTestDoubles.Reset(logDirectory: directory);
            var owner = BlackboxTestDoubles.Owner("owner");
            var first = BlackboxTestDoubles.HandleFor(owner);
            first.Write("first", "Run");
            first.Export(format: ExportFormat.Txt, openLogOption: OpenLogOption.Never);

            // Table 7 / RestartDefaultAndRerun x Run
            BlackboxHandle.RestartDefaultSession();
            BlackboxTestDoubles.Reset(logDirectory: directory);
            var second = BlackboxTestDoubles.HandleFor(owner);
            second.Write("second", "Run");

            Assert.That(second.Id, Is.EqualTo(0));
            Assert.That(BlackboxTestDoubles.Logs(BlackboxTestDoubles.BlackboxFor(owner)).Single().Message, Is.EqualTo("second"));
        }

        [Test]
        [RunWithUniTest]
        public void RingBufferLimitKeepsRecentLogsInPublicFlow()
        {
            BlackboxTestDoubles.Reset(maxLogCount: 2);
            var owner = BlackboxTestDoubles.Owner("owner");
            var handle = BlackboxTestDoubles.HandleFor(owner);

            // Table 7 / RingBufferLimit x Run
            handle.Write("one", "Run");
            handle.Write("two", "Run");
            handle.Write("three", "Run");

            var logs = BlackboxTestDoubles.Logs(BlackboxTestDoubles.BlackboxFor(owner));
            Assert.That(logs.Count, Is.EqualTo(2));
            Assert.That(logs[0].Message, Is.EqualTo("two"));
            Assert.That(logs[1].Message, Is.EqualTo("three"));
        }
    }
}
