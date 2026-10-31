using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    internal sealed class DefaultSessionRestartTests : BlackboxTestBase
    {
        [Test]
        [RunWithUniTest]
        public void RestartStartsANewLifetimeWithTheDefaultSettings()
        {
            BlackboxHandle.Configure("retained-directory", _ => { }, _ => { }, true,
                ExportFormat.Txt, FullExportOption.Focused, OpenLogOption.Never,
                ExceptionHandlingOption.CrashExport, TargetTypes.Name, UseBlackboxOption.Use);
            BlackboxHandle.MaxLogCount = 17;
            BlackboxHandle.DefaultRecursionDepth = 3;
            var old = BlackboxSessionRegistry.Current;
            var settings = old.State.Settings;
            var owner = new object();
            var handle = BlackboxHandle.Of(owner);
            handle.Write("retained log");
            old.State.NextInteractionId();
            old.NextExportOrdinal();
            old.State.WarnOnce(SessionWarning.CrossThreadClose, "old warning");
            Assert.That(old.State.TryFreeze(new CrashInfo("old crash", "old stack")), Is.True);

            BlackboxHandle.RestartDefaultSession();

            var current = BlackboxSessionRegistry.Current;
            Assert.That(current, Is.Not.SameAs(old));
            Assert.That(current.SessionKey, Is.Not.EqualTo(old.SessionKey));
            Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(BlackboxHandle.DefaultSessionKey));
            Assert.That(old.IsDisposed, Is.True);
            Assert.That(handle.IsValid, Is.False);
            Assert.That(handle.SessionKey, Is.EqualTo(Guid.Empty));
            Assert.That(current.Subjects.Count(), Is.Zero);
            Assert.That(current.State.CurrentSequence, Is.EqualTo(-1));
            Assert.That(current.State.NextInteractionId(), Is.Zero);
            Assert.That(current.NextExportOrdinal(), Is.EqualTo(1));
            Assert.That(current.State.Crash, Is.Null);
            Assert.That(current.State.Settings.LogDirectory, Is.EqualTo("retained-directory"));
            Assert.That(current.State.Settings.NormalLogger, Is.SameAs(settings.NormalLogger));
            Assert.That(current.State.Settings.WarningLogger, Is.SameAs(settings.WarningLogger));
            Assert.That(BlackboxHandle.StrongReference, Is.True);
            Assert.That(BlackboxHandle.MaxLogCount, Is.EqualTo(17));
            Assert.That(BlackboxHandle.DefaultRecursionDepth, Is.EqualTo(3));
            Assert.That(BlackboxHandle.ExportFormat, Is.EqualTo(ExportFormat.Txt));
            Assert.That(BlackboxHandle.FullExportOption, Is.EqualTo(FullExportOption.Focused));
            Assert.That(BlackboxHandle.OpenLogOption, Is.EqualTo(OpenLogOption.Never));
            Assert.That(BlackboxHandle.TagTargetTypes, Is.EqualTo(TargetTypes.Name));
            Assert.That(current.State.Settings.ExceptionHandlingOption, Is.EqualTo(ExceptionHandlingOption.CrashExport));

            var fresh = BlackboxHandle.Of(owner);
            Assert.That(fresh.Id, Is.Zero);
            fresh.Write("fresh log");
            handle.Write("rejected");
            Assert.That(current.State.CurrentSequence, Is.Zero);
            Assert.That(old.Subjects.GetOrAdd(owner).GetLogs().Single().Message, Is.EqualTo("retained log"));
            Assert.That(current.Subjects.GetOrAdd(owner).GetLogs().Single().Message, Is.EqualTo("fresh log"));
            BlackboxHandle.MaxLogCount = 9;
            Assert.That(old.State.Settings.MaxLogCount, Is.EqualTo(17));
            Assert.That(old.State.Crash.Message, Is.EqualTo("old crash"));
        }

        [Test]
        [RunWithUniTest]
        public void RestartPreservesDisabledRecordingAndRenewsWarningBudget()
        {
            var warnings = new ConcurrentQueue<string>();
            BlackboxHandle.WarningLogger = warnings.Enqueue;
            var first = BlackboxHandle.Of(new object());
            BlackboxHandle.RestartDefaultSession();
            first.Write("rejected");
            first.Write("rejected again");
            var second = BlackboxHandle.Of(new object());
            BlackboxHandle.UseBlackbox = false;
            BlackboxHandle.RestartDefaultSession();
            Assert.That(BlackboxHandle.UseBlackbox, Is.False);
            Assert.That(BlackboxHandle.Of(new object()).IsValid, Is.False);
            second.Write("also rejected");
            second.Write("also rejected again");
            Assert.That(warnings.Count, Is.EqualTo(2));
            Assert.That(warnings.All(w => w.Contains("disposed")), Is.True);
        }

        [TestCase(ExportFormat.Txt, ".txt")]
        [TestCase(ExportFormat.Html, ".html")]
        [RunWithUniTest]
        public void RetainedHandlesExportAfterRestartWithoutMixingOrOverwriting(ExportFormat format, string extension)
        {
            var directory = BlackboxTestDoubles.CreateTempDirectory();
            BlackboxHandle.LogDirectory = directory;
            var owner = new object();
            var old = BlackboxHandle.Of(owner);
            var oldKey = old.SessionKey.ToString("N").Substring(0, 12);
            old.Write("OLD_ONLY");
            old.Export(format: format, openLogOption: OpenLogOption.Never);
            BlackboxHandle.RestartDefaultSession();
            var fresh = BlackboxHandle.Of(owner);
            var newKey = fresh.SessionKey.ToString("N").Substring(0, 12);
            fresh.Write("NEW_ONLY");
            old.Write("REJECTED_RECORD");
            old.Export(format: format, openLogOption: OpenLogOption.Never);
            fresh.Export(format: format, openLogOption: OpenLogOption.Never);
            var files = Directory.GetFiles(directory, "*" + extension);
            Assert.That(files.Length, Is.EqualTo(3));
            var oldFiles = files.Where(f => Path.GetFileName(f).Contains(oldKey)).ToArray();
            Assert.That(oldFiles.Length, Is.EqualTo(2));
            foreach (var file in oldFiles)
                Assert.That(File.ReadAllText(file), Does.Contain("OLD_ONLY").And.Not.Contain("NEW_ONLY").And.Not.Contain("REJECTED_RECORD"));
            var newFile = files.Single(f => Path.GetFileName(f).Contains(newKey));
            Assert.That(Path.GetFileName(newFile), Does.EndWith("-1" + extension));
            Assert.That(File.ReadAllText(newFile), Does.Contain("NEW_ONLY").And.Not.Contain("OLD_ONLY"));
            GC.KeepAlive(owner);
        }

        [Test]
        [RunWithUniTest]
        public async Task RestartInsideNestedSessionPreservesItsHandlesSettingsAndChildren()
        {
            BlackboxHandle.MaxLogCount = 23;
            var oldDefault = BlackboxSessionRegistry.Current;
            var defaultOwner = new object();
            var defaultHandle = BlackboxHandle.Of(defaultOwner);
            var defaultScope = defaultHandle.Scope("default");
            Guid restartedKey;
            using (var outer = BlackboxHandle.BeginSession())
            {
                BlackboxHandle.MaxLogCount = 7;
                var outerHandle = BlackboxHandle.Of(new object());
                using (var inner = BlackboxHandle.BeginSession())
                {
                    BlackboxHandle.MaxLogCount = 5;
                    var owner = new object();
                    var handle = BlackboxHandle.Of(owner);
                    using var scope = handle.Scope("inner");
                    using var exert = handle.Exert(owner, "inner exert");
                    using var ready = new ManualResetEventSlim();
                    using var go = new ManualResetEventSlim();
                    var child = Task.Run(() =>
                    {
                        ready.Set();
                        Assert.That(go.Wait(10000), Is.True);
                        handle.Write("child survives");
                        return (BlackboxHandle.CurrentSessionKey, BlackboxHandle.MaxLogCount);
                    });
                    try
                    {
                        Assert.That(ready.Wait(10000), Is.True);
                        BlackboxHandle.RestartDefaultSession();
                        restartedKey = BlackboxHandle.DefaultSessionKey;
                        Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(inner.SessionKey));
                        Assert.That(BlackboxHandle.MaxLogCount, Is.EqualTo(5));
                        Assert.That(handle.IsValid && scope.IsAlive && exert.IsAlive && outerHandle.IsValid, Is.True);
                        Assert.That(defaultHandle.IsValid || defaultScope.IsAlive, Is.False);
                        go.Set();
                        var result = await child;
                        Assert.That(result.Item1, Is.EqualTo(inner.SessionKey));
                        Assert.That(result.Item2, Is.EqualTo(5));
                        Assert.That(BlackboxSessionRegistry.GetBlackbox(owner).GetLogs().Any(l => l.Message == "child survives"), Is.True);
                    }
                    finally { go.Set(); await child; }
                    GC.KeepAlive(owner);
                }
                Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(outer.SessionKey));
                Assert.That(BlackboxHandle.MaxLogCount, Is.EqualTo(7));
            }
            Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(restartedKey));
            Assert.That(restartedKey, Is.Not.EqualTo(oldDefault.SessionKey));
            Assert.That(BlackboxHandle.MaxLogCount, Is.EqualTo(23));
            defaultScope.Dispose();
            GC.KeepAlive(defaultOwner);
        }

        [Test]
        [RunWithUniTest]
        public async Task CompetingRestartsDisposeReplacedSessionsAndUnpublishedCandidates()
        {
            var original = BlackboxSessionRegistry.Current;
            var candidates = new ConcurrentBag<BlackboxSession>();
            using var ready = new CountdownEvent(2);
            using var go = new ManualResetEventSlim();
            int attempts = 0;
            BlackboxSessionRegistry.BeforeDefaultSessionExchange = candidate =>
            {
                Assert.That(BlackboxHandle.ActiveSessionCount, Is.Zero);
                Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.Empty);
                candidates.Add(candidate);
                if (Interlocked.Increment(ref attempts) <= 2)
                {
                    ready.Signal();
                    if (!go.Wait(10000)) throw new TimeoutException();
                }
            };
            var first = Task.Run(BlackboxHandle.RestartDefaultSession);
            var second = Task.Run(BlackboxHandle.RestartDefaultSession);
            try
            {
                Assert.That(ready.Wait(10000), Is.True);
                Assert.That(BlackboxHandle.ActiveSessionCount, Is.Zero);
                Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.Empty);
                go.Set();
                await Task.WhenAll(first, second);
            }
            finally
            {
                go.Set();
                try { await Task.WhenAll(first, second); }
                finally { BlackboxSessionRegistry.BeforeDefaultSessionExchange = null; }
            }
            var current = BlackboxSessionRegistry.Current;
            Assert.That(candidates.Count, Is.EqualTo(3));
            Assert.That(candidates.Select(s => s.SessionKey).Distinct().Count(), Is.EqualTo(3));
            Assert.That(candidates.Count(s => !s.IsDisposed), Is.EqualTo(1));
            Assert.That(candidates.Single(s => !s.IsDisposed), Is.SameAs(current));
            Assert.That(original.IsDisposed, Is.True);
            Assert.That(BlackboxHandle.Of(new object()).IsValid, Is.True);
            Assert.That(BlackboxHandle.ActiveSessionCount, Is.Zero);
            Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.Empty);
        }

        [Test]
        [RunWithUniTest]
        public void RestartFromDisposedExplicitBindingDoesNotReviveOrRebindIt()
        {
            using var outer = BlackboxHandle.BeginSession();
            using var inner = BlackboxHandle.BeginSession();
            outer.Dispose();
            inner.Dispose();
            var defaultKey = BlackboxHandle.DefaultSessionKey;
            BlackboxHandle.RestartDefaultSession();
            Assert.That(BlackboxHandle.DefaultSessionKey, Is.Not.EqualTo(defaultKey));
            Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(outer.SessionKey));
            Assert.That(BlackboxHandle.Of(new object()).IsValid, Is.False);
        }

        [Test]
        [RunWithUniTest]
        public void RestartIsOnlyExposedOnBlackboxHandle()
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.Static;
            var method = typeof(BlackboxHandle).GetMethod(nameof(BlackboxHandle.RestartDefaultSession), Flags);
            Assert.That(method.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(method.GetParameters(), Is.Empty);
            Assert.That(typeof(BlackboxSession).GetMethod(method.Name, Flags), Is.Null);
            Assert.That(typeof(BlackboxHandle).GetMethod("ForceReset", Flags), Is.Null);
        }
    }
}
