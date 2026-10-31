using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    internal sealed class SessionKeyTests : BlackboxTestBase
    {
        [Test]
        [RunWithUniTest]
        public void OutsideSessionCurrentKeyIsDefaultKey()
        {
            Assert.That(BlackboxHandle.DefaultSessionKey, Is.Not.EqualTo(Guid.Empty));
            Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(BlackboxHandle.DefaultSessionKey));
        }

        [Test]
        [RunWithUniTest]
        public void BeginExposesItsKeyWithoutChangingDefaultKey()
        {
            var defaultKey = BlackboxHandle.DefaultSessionKey;
            using (var session = BlackboxHandle.BeginSession())
            {
                Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(session.SessionKey));
                Assert.That(session.SessionKey, Is.Not.EqualTo(defaultKey));
                Assert.That(BlackboxHandle.DefaultSessionKey, Is.EqualTo(defaultKey));
            }
            Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(defaultKey));
        }

        [Test]
        [RunWithUniTest]
        public async Task KeyFlowsThroughTasksThreadsAndAwait()
        {
            using var session = BlackboxHandle.BeginSession();
            Assert.That(await Task.Run(() => BlackboxHandle.CurrentSessionKey), Is.EqualTo(session.SessionKey));
            await Task.Yield();
            Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(session.SessionKey));

            Guid threadKey = Guid.Empty;
            var thread = new Thread(() => threadKey = BlackboxHandle.CurrentSessionKey);
            thread.Start();
            Assert.That(thread.Join(10000), Is.True);
            Assert.That(threadKey, Is.EqualTo(session.SessionKey));
        }

        [Test]
        [RunWithUniTest]
        public async Task TaskStartedBeforeBeginKeepsDefaultKey()
        {
            using var ready = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            var pending = Task.Run(() =>
            {
                ready.Set();
                Assert.That(release.Wait(10000), Is.True);
                return BlackboxHandle.CurrentSessionKey;
            });
            try
            {
                Assert.That(ready.Wait(10000), Is.True);
                using var session = BlackboxHandle.BeginSession();
                release.Set();
                Assert.That(await pending, Is.EqualTo(BlackboxHandle.DefaultSessionKey));
                Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(session.SessionKey));
            }
            finally { release.Set(); await pending; }
        }

        [Test]
        [RunWithUniTest]
        public void ThreadStartedBeforeBeginKeepsDefaultKey()
        {
            using var ready = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            Guid threadKey = Guid.Empty;
            var thread = new Thread(() =>
            {
                ready.Set();
                if (release.Wait(10000)) threadKey = BlackboxHandle.CurrentSessionKey;
            });
            thread.Start();
            try
            {
                Assert.That(ready.Wait(10000), Is.True);
                using var session = BlackboxHandle.BeginSession();
                release.Set();
                Assert.That(thread.Join(10000), Is.True);
                Assert.That(threadKey, Is.EqualTo(BlackboxHandle.DefaultSessionKey));
                Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(session.SessionKey));
            }
            finally { release.Set(); thread.Join(10000); }
        }

        [Test]
        [RunWithUniTest]
        public void DisposingNestedSessionRestoresOuterKey()
        {
            using var outer = BlackboxHandle.BeginSession();
            using (var inner = BlackboxHandle.BeginSession())
            {
                Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(inner.SessionKey));
                Assert.That(inner.SessionKey, Is.Not.EqualTo(outer.SessionKey));
            }
            Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(outer.SessionKey));
        }

        [Test]
        [RunWithUniTest]
        public void CachedConstructorHandleStaysInDefaultSession()
        {
            var subject = new CachedRecorder();
            var originalBox = BlackboxSessionRegistry.GetBlackbox(subject);
            using var session = BlackboxHandle.BeginSession();
            subject.Write();
            Assert.That(subject.Handle.SessionKey, Is.EqualTo(BlackboxHandle.DefaultSessionKey));
            Assert.That(subject.Handle.SessionKey, Is.Not.EqualTo(BlackboxHandle.CurrentSessionKey));
            Assert.That(BlackboxSessionRegistry.Contains(subject), Is.False);
            Assert.That(originalBox.GetLogs().Any(log => log.Message == "cached record"), Is.True);
            GC.KeepAlive(subject);
        }

        [Test]
        [RunWithUniTest]
        public void DefaultHandleHasEmptyKey() =>
            Assert.That(default(BlackboxHandle).SessionKey, Is.EqualTo(Guid.Empty));

        [TestCase("restartDefault")]
        [TestCase("dispose")]
        [TestCase("disabled")]
        [TestCase("frozen")]
        [RunWithUniTest]
        public void InvalidatedHandlesHaveEmptyKeysWithoutWarnings(string reason)
        {
            using var session = reason == "restartDefault" ? null : BlackboxHandle.BeginSession();
            var owner = new object();
            var handle = BlackboxHandle.Of(owner);
            Assert.That(handle.SessionKey, Is.EqualTo(BlackboxHandle.CurrentSessionKey));
            switch (reason)
            {
                case "restartDefault": BlackboxHandle.RestartDefaultSession(); break;
                case "dispose": session.Dispose(); break;
                case "disabled": BlackboxHandle.UseBlackbox = false; break;
                case "frozen": BlackboxSessionRegistry.Current.State.TryFreeze(new CrashInfo("test", string.Empty)); break;
            }
            var warnings = BlackboxTestDoubles.WarningLogs.Count;
            Assert.That(handle.IsValid, Is.False);
            Assert.That(handle.SessionKey, Is.EqualTo(Guid.Empty));
            Assert.That(BlackboxTestDoubles.WarningLogs.Count, Is.EqualTo(warnings));
            GC.KeepAlive(owner);
        }

        [Test]
        [RunWithUniTest]
        public void RestartChangesDefaultKeyWithoutChangingExplicitSessionKeys()
        {
            var defaultKey = BlackboxHandle.DefaultSessionKey;
            BlackboxHandle.RestartDefaultSession();
            Assert.That(BlackboxHandle.CurrentSessionKey, Is.Not.EqualTo(defaultKey));
            defaultKey = BlackboxHandle.DefaultSessionKey;
            using var session = BlackboxHandle.BeginSession();
            var owner = new object();
            var old = BlackboxHandle.Of(owner);
            BlackboxHandle.RestartDefaultSession();
            Assert.That(old.SessionKey, Is.EqualTo(session.SessionKey));
            Assert.That(BlackboxHandle.Of(owner).SessionKey, Is.EqualTo(session.SessionKey));
            Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(session.SessionKey));
            Assert.That(BlackboxHandle.DefaultSessionKey, Is.Not.EqualTo(defaultKey));
            GC.KeepAlive(owner);
        }

        [Test]
        [RunWithUniTest]
        public void DiagnosticPropertiesAreReadOnlyAndExposeNoSessionObject()
        {
            foreach (var name in new[] { nameof(BlackboxHandle.CurrentSessionKey), nameof(BlackboxHandle.DefaultSessionKey) })
            {
                var property = typeof(BlackboxHandle).GetProperty(name, BindingFlags.Public | BindingFlags.Static);
                Assert.That(property.PropertyType, Is.EqualTo(typeof(Guid)));
                Assert.That(property.CanWrite, Is.False);
            }
            var handleKey = typeof(BlackboxHandle).GetProperty("SessionKey");
            Assert.That(handleKey.PropertyType, Is.EqualTo(typeof(Guid)));
            Assert.That(handleKey.CanWrite, Is.False);
            Assert.That(typeof(BlackboxHandle).GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Any(property => property.PropertyType == typeof(BlackboxSession)), Is.False);
            // Session statics live in the internal registry; the public entry point is BlackboxHandle.
            Assert.That(typeof(BlackboxSession).GetMembers(BindingFlags.Public | BindingFlags.Static), Is.Empty);
        }

        [Test]
        [RunWithUniTest]
        public void RepeatedKeyReadsAllocateNothingAndDoNotLog()
        {
            using var session = BlackboxHandle.BeginSession();
            var owner = new object();
            var handle = BlackboxHandle.Of(owner);
            ReadKeys();
            var warnings = BlackboxTestDoubles.WarningLogs.Count;
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) ReadKeys();
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Assert.That(allocated, Is.Zero);
            Assert.That(BlackboxTestDoubles.WarningLogs.Count, Is.EqualTo(warnings));
            GC.KeepAlive(owner);

            void ReadKeys()
            {
                if (BlackboxHandle.CurrentSessionKey != session.SessionKey || BlackboxHandle.DefaultSessionKey == Guid.Empty ||
                    handle.SessionKey != session.SessionKey || default(BlackboxHandle).SessionKey != Guid.Empty)
                    throw new InvalidOperationException("Unexpected diagnostic key.");
            }
        }

        private sealed class CachedRecorder
        {
            private readonly BlackboxHandle _bb;
            public BlackboxHandle Handle => _bb;
            public CachedRecorder() { using var scope = BlackboxHandle.Of(this).Construct(out _bb); }
            public void Write() => _bb.Write("cached record");
        }
    }
}
