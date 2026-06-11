using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using com.BlackThunder.BlackboxSystem.Exporters;
using NUnit.Framework;

namespace com.BlackThunder.BlackboxSystem.Tests
{
#if BLACKBOX
    internal sealed class ExporterTests : BlackboxTestBase
    {
        [Test]
        public void TxtExporterRejectsMissingLogDirectory()
        {
            var blackbox = BlackboxTestDoubles.Blackbox("owner");

            // Table 6-2 / LogDirectoryMissing x Export.Normal
            Assert.That(
                () => TxtExporter.Export(blackbox, 10, false, true, false),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void TxtExporterCreatesNormalAndCrashFiles()
        {
            var directory = BlackboxTestDoubles.CreateTempDirectory();
            BlackboxTestDoubles.Reset(logDirectory: directory);
            var blackbox = BlackboxTestDoubles.Blackbox("owner");
            blackbox.Write("hello", "Run");

            // Table 6-2 / TxtReady x Export.Normal
            TxtExporter.Export(blackbox, 10, false, true, false);

            // Table 6-2 / TxtReady x Export.Crash
            TxtExporter.Export(blackbox, 10, true, true, false);

            var normalFile = BlackboxTestDoubles.GetFiles(directory, "Blackbox*.txt").Single();
            var crashFile = BlackboxTestDoubles.GetFiles(directory, "[CRASH]*.txt").Single();

            Assert.That(File.ReadAllText(normalFile), Does.Contain("hello"));
            Assert.That(Path.GetFileName(crashFile), Does.StartWith("[CRASH]"));
            Assert.That(BlackboxTestDoubles.NormalLogs.Any(log => log.Contains("TxtExporter")), Is.True);
        }

        [Test]
        public void HtmlExporterRejectsMissingLogDirectory()
        {
            var blackbox = BlackboxTestDoubles.Blackbox("owner");

            // Table 6-2 / LogDirectoryMissing x Export.Crash
            Assert.That(
                () => HtmlExporter.Export(blackbox, 10, true, true, false),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void HtmlExporterCreatesNormalAndCrashFiles()
        {
            var directory = BlackboxTestDoubles.CreateTempDirectory();
            BlackboxTestDoubles.Reset(logDirectory: directory);
            var blackbox = BlackboxTestDoubles.Blackbox("owner");
            blackbox.Write("hello", "Run");

            // Table 6-2 / HtmlReady x Export.Normal
            HtmlExporter.Export(blackbox, 10, false, true, false);

            // Table 6-2 / HtmlReady x Export.Crash
            HtmlExporter.Export(blackbox, 10, true, true, false);

            var normalFile = BlackboxTestDoubles.GetFiles(directory, "Blackbox*.html").Single();
            var crashFile = BlackboxTestDoubles.GetFiles(directory, "[CRASH]*.html").Single();

            Assert.That(File.ReadAllText(normalFile), Does.Contain("hello"));
            Assert.That(Path.GetFileName(crashFile), Does.StartWith("[CRASH]"));
            Assert.That(BlackboxTestDoubles.NormalLogs.Any(log => log.Contains("HtmlExporter")), Is.True);
        }

        [Test]
        public void HtmlExporterWritesInteractionLinks()
        {
            var directory = BlackboxTestDoubles.CreateTempDirectory();
            BlackboxTestDoubles.Reset(logDirectory: directory);
            var source = BlackboxTestDoubles.Blackbox("source");
            var target = BlackboxTestDoubles.Blackbox("target");
            source.ExertMessage(target, "call", "Run");

            // Table 6-2 / HtmlReady x Export.Normal
            HtmlExporter.Export(source, 10, false, true, false);

            var html = File.ReadAllText(BlackboxTestDoubles.GetFiles(directory, "Blackbox*.html").Single());
            Assert.That(html, Does.Contain("class='interaction'"));
            Assert.That(html, Does.Contain("href='#log_"));
            Assert.That(html, Does.Contain("target"));
        }

        [Test]
        public void HtmlExporterWritesTagLinksWithoutInlineTagIds()
        {
            var directory = BlackboxTestDoubles.CreateTempDirectory();
            BlackboxTestDoubles.Reset(logDirectory: directory);
            var source = BlackboxTestDoubles.Blackbox("source");
            var target = BlackboxTestDoubles.Blackbox("target");
            source.ExertMessage(target, "include target", "Run");
            source.Write("uses %0", "Tag").With(target, TargetTypes.Full);

            // Table 6-2 / HtmlReady x Export.Normal
            HtmlExporter.Export(source, 10, false, true, false);

            var html = File.ReadAllText(BlackboxTestDoubles.GetFiles(directory, "Blackbox*.html").Single());
            Assert.That(html, Does.Contain("uses &#39;target #1&#39;"));
            Assert.That(html, Does.Contain(".interaction.tag-interaction { color: #f6a04d;"));
            Assert.That(html, Does.Contain("href='#log_1_1' class='interaction tag-interaction' title='Tag #1: #1: target'>[1]</a>"));
            Assert.That(html, Does.Contain("href='#log_0_1' class='interaction tag-interaction' title='Tag #1: #0: source'>[1]</a>"));
            Assert.That(html, Does.Contain("<span id='log_0_1'></span>"));
            Assert.That(html, Does.Contain("function resolveHighlightTarget(el)"));
            Assert.That(html, Does.Contain("const highlightTarget = resolveHighlightTarget(targetElement);"));
            Assert.That(html, Does.Not.Contain("-[1]-&gt; #1: target"));
            Assert.That(html, Does.Not.Contain("&lt;-[1]- #0: source"));
            Assert.That(html, Does.Not.Contain("target #1 [1]"));
            Assert.That(html, Does.Not.Contain("source #0 [1]"));
        }

        [Test]
        public void OpenLogFailureIsReportedAsWarning()
        {
            var directory = BlackboxTestDoubles.CreateTempDirectory();
            BlackboxTestDoubles.Reset(logDirectory: directory);
            var blackbox = BlackboxTestDoubles.Blackbox("owner");
            blackbox.Write("hello", "Run");

            TxtExporter.OpenLogProcess = _ => throw new InvalidOperationException("blocked txt open");
            HtmlExporter.OpenLogProcess = _ => throw new InvalidOperationException("blocked html open");

            try
            {
                // Table 6-2 / TxtReady x Export.OpenLog
                TxtExporter.Export(blackbox, 10, false, true, true);

                // Table 6-2 / HtmlReady x Export.OpenLog
                HtmlExporter.Export(blackbox, 10, false, true, true);
            }
            finally
            {
                TxtExporter.OpenLogProcess = startInfo => Process.Start(startInfo);
                HtmlExporter.OpenLogProcess = startInfo => Process.Start(startInfo);
            }

            Assert.That(BlackboxTestDoubles.WarningLogs.Any(log => log.Contains("Failed to open the file automatically")), Is.True);
            Assert.That(BlackboxTestDoubles.WarningLogs.Any(log => log.Contains("blocked txt open")), Is.True);
            Assert.That(BlackboxTestDoubles.WarningLogs.Any(log => log.Contains("blocked html open")), Is.True);
        }
    }
#endif
}
