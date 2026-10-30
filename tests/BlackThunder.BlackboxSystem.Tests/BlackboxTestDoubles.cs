using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    [NonParallelizable]
    internal abstract class BlackboxTestBase
    {
        [SetUp]
        public void SetUpBlackboxTest()
        {
            BlackboxTestDoubles.Reset();
        }

        [TearDown]
        public void TearDownBlackboxTest()
        {
            try
            {
                Assert.That(BlackboxHandle.ActiveSessionCount, Is.Zero,
                    "Undisposed Blackbox sessions: " + string.Join(", ", BlackboxHandle.GetActiveSessionKeys()));
            }
            finally
            {
                BlackboxHandle.DisposeActiveSessions();
                BlackboxTestDoubles.CleanupTempDirectories();
                BlackboxHandle.RestartDefaultSession();
            }
        }
    }

    internal static class BlackboxTestDoubles
    {
        public sealed class NamedOwner
        {
            private readonly string _name;
            public string Name => _name;

            public NamedOwner(string name)
            {
                _name = name;
            }

            public override string ToString()
            {
                return _name;
            }
        }

        private static readonly List<string> _normalLogs = new List<string>();
        private static readonly List<string> _warningLogs = new List<string>();
        private static readonly List<string> _tempDirectories = new List<string>();

        public static IReadOnlyList<string> NormalLogs
        {
            get { return _normalLogs; }
        }

        public static IReadOnlyList<string> WarningLogs
        {
            get { return _warningLogs; }
        }

        public static void Reset(int maxLogCount = 100, string logDirectory = null)
        {
            BlackboxHandle.RestartDefaultSession();
            _normalLogs.Clear();
            _warningLogs.Clear();

            BlackboxHandle.Configure(
                logDirectory,
                _normalLogs.Add,
                _warningLogs.Add,
                false,
                ExportFormat.Txt,
                FullExportOption.Full,
                OpenLogOption.Never,
                ExceptionHandlingOption.None,
                TargetTypes.Full,
                UseBlackboxOption.Use);

            BlackboxHandle.MaxLogCount = maxLogCount;
            BlackboxHandle.DefaultRecursionDepth = 10;
        }

        public static NamedOwner Owner(string name)
        {
            return new NamedOwner(name);
        }

        public static Blackbox Blackbox(string ownerName)
        {
            return BlackboxFor(Owner(ownerName));
        }

        public static Blackbox BlackboxFor(object owner)
        {
            var box = BlackboxSessionRegistry.GetBlackbox(owner);
            if (box != null && box.State.CanWrite) box.SetLabel(NameOf(owner));
            return box;
        }

        public static BlackboxHandle HandleFor<T>(T owner) where T : class => BlackboxHandle.Of(owner, label: NameOf(owner));
        // Front-side scope, exert and tag binding for Blackboxes owned by the current session.
        public static ScopeHandle Scope(Blackbox box, string message, string methodName) =>
            BlackboxSessionRegistry.Current.OpenScope(box, message, methodName);
        public static ExertHandle Exert(Blackbox source, Blackbox target, string message, string methodName) =>
            BlackboxSessionRegistry.Current.Exert(source, target, message, methodName);
        public static TagHandle Bind(TagHandle tag) => tag.Bind(BlackboxSessionRegistry.Current);
        private static string NameOf(object owner) => owner is NamedOwner named ? named.Name :
            owner is global::BlackThunder.BlackboxSystem.Tests.NamedOwner table ? table.Name : null;

        public static List<LogData> Logs(Blackbox blackbox)
        {
            return blackbox.GetLogs().ToList();
        }

        public static List<string> Lines(Blackbox blackbox)
        {
            return blackbox.GetLogs().Select(log => log.ToString()).ToList();
        }

        public static string CreateTempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "BlackboxTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            _tempDirectories.Add(directory);
            return directory;
        }

        public static string[] GetFiles(string directory, string searchPattern)
        {
            return Directory.Exists(directory)
                ? Directory.GetFiles(directory, searchPattern)
                : Array.Empty<string>();
        }

        public static void CleanupTempDirectories()
        {
            foreach (var directory in _tempDirectories.ToArray())
            {
                try
                {
                    if (Directory.Exists(directory))
                        Directory.Delete(directory, true);
                }
                catch
                {
                    // Test cleanup must not hide the assertion failure that happened first.
                }
            }

            _tempDirectories.Clear();
        }

        public static void ForceFullCollection()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
