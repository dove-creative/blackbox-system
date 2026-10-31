using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests.CSharp10
{
    // Uses only the public API of the packaged netstandard2.1 library, as a C# 10 consumer would.
    internal class InterpolatedWriteTests
    {
        // Internal
        private bool _useBlackbox;
        private int _formatCalls;
        private string _directory;


        // Content
        [SetUp]
        public void SetUp()
        {
            _useBlackbox = BlackboxHandle.UseBlackbox;
            _formatCalls = 0;
            _directory = Path.Combine(Path.GetTempPath(), "BlackboxCSharp10_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            BlackboxHandle.UseBlackbox = _useBlackbox;
            BlackboxHandle.DisposeActiveSessions();
            BlackboxHandle.RestartDefaultSession();
            var path = Path.GetFullPath(_directory);
            if (Path.GetDirectoryName(path) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
                || !Path.GetFileName(path).StartsWith("BlackboxCSharp10_", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected C# 10 test output directory.");
            Directory.Delete(path, true);
        }

        private string FormatValue()
        {
            _formatCalls++;
            return "formatted";
        }

        // Table 1-3 / compiler-selected handler overloads across every recording lifetime state.
        public static IEnumerable<TestCaseData> HandlerCases()
        {
            foreach (var state in new[] { "active", "default", "disabledLookup", "disabledRetained", "disposed", "frozen" })
                foreach (var operation in new[] { "write", "construct", "constructCached", "dispose", "scope", "exertMessage", "exert" })
                    yield return new TestCaseData(state, operation);
        }

        [TestCaseSource(nameof(HandlerCases))]
        [RunWithUniTest]
        public void InterpolatedOverloadsRespectRecordingLifetime(string state, string operation)
        {
            BlackboxHandle.UseBlackbox = true;
            using var session = BlackboxHandle.BeginSession();
            BlackboxHandle.Configure(_directory, null, null, false, ExportFormat.Txt, FullExportOption.Full,
                OpenLogOption.Never, ExceptionHandlingOption.None, TargetTypes.Full, UseBlackboxOption.Use);
            var owner = new object();
            var peer = new object();
            if (state == "disabledLookup") BlackboxHandle.UseBlackbox = false;
            var handle = state == "default" ? default : BlackboxHandle.Of(owner, label: "owner");
            if (state == "disabledRetained") BlackboxHandle.UseBlackbox = false;
            if (state == "disposed") session.Dispose();
            if (state == "frozen") handle.CrashExport("freeze", openLog: OpenLogOption.Never);
            var active = state == "active";

            switch (operation)
            {
                case "write":
                    Assert.That((string)handle.Write($"value {FormatValue()}", "Run"),
                        Is.EqualTo(active ? "value formatted" : string.Empty));
                    break;
                case "construct":
                    using (var scope = handle.Construct($"value {FormatValue()}", "Run"))
                        Assert.That(scope.IsAlive, Is.EqualTo(active));
                    break;
                case "constructCached":
                    using (var scope = handle.Construct($"value {FormatValue()}", out var cached, "Run"))
                    {
                        Assert.That(scope.IsAlive, Is.EqualTo(active));
                        Assert.That(cached.IsValid, Is.EqualTo(active));
                        if (active) Assert.That(cached.Owner, Is.SameAs(owner));
                    }
                    break;
                case "dispose":
                    Assert.That(handle.Dispose($"value {FormatValue()}", "Run").IsValid, Is.EqualTo(active));
                    break;
                case "scope":
                    using (var scope = handle.Scope($"value {FormatValue()}", "Run"))
                        Assert.That(scope.IsAlive, Is.EqualTo(active));
                    break;
                case "exertMessage":
                    Assert.That(handle.ExertMessage(peer, $"value {FormatValue()}", "Run"),
                        Is.EqualTo(active ? "value formatted" : string.Empty));
                    break;
                case "exert":
                    using (var exert = handle.Exert(peer, $"value {FormatValue()}", "Run"))
                        Assert.That(exert.IsAlive, Is.EqualTo(active));
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(operation));
            }

            Assert.That(_formatCalls, Is.EqualTo(active ? 1 : 0));
            if (handle.Owner != null) handle.Export(openLogOption: OpenLogOption.Never);
            var reports = string.Join("\n", Directory.GetFiles(_directory).Select(File.ReadAllText));
            Assert.That(reports.Contains("value formatted"), Is.EqualTo(active));
            GC.KeepAlive(owner);
            GC.KeepAlive(peer);
        }
    }
}
