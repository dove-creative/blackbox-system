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
        public void OutsideSessionCurrentKeyIsDefaultKey()
        {
            Assert.That(BlackboxSession.DefaultKey, Is.Not.EqualTo(Guid.Empty));
            Assert.That(BlackboxSession.CurrentKey, Is.EqualTo(BlackboxSession.DefaultKey));
        }

        [Test]
        public void BeginExposesItsKeyWithoutChangingDefaultKey()
        {
            var defaultKey = BlackboxSession.DefaultKey;
            using (var session = BlackboxSession.Begin())
            {
                Assert.That(BlackboxSession.CurrentKey, Is.EqualTo(session.SessionKey));
                Assert.That(session.SessionKey, Is.Not.EqualTo(defaultKey));
                Assert.That(BlackboxSession.DefaultKey, Is.EqualTo(defaultKey));
            }
            Assert.That(BlackboxSession.CurrentKey, Is.EqualTo(defaultKey));
        }

        [Test]
        public async Task KeyFlowsThroughTasksThreadsAndAwait()
        {
            using var session = BlackboxSession.Begin();
            Assert.That(await Task.Run(() => BlackboxSession.CurrentKey), Is.EqualTo(session.SessionKey));
            await Task.Yield();
            Assert.That(BlackboxSession.CurrentKey, Is.EqualTo(session.SessionKey));

            Guid threadKey = Guid.Empty;
            var thread = new Thread(() => threadKey = BlackboxSession.CurrentKey);
            thread.Start();
            Assert.That(thread.Join(10000), Is.True);
            Assert.That(threadKey, Is.EqualTo(session.SessionKey));
        }

        [Test]
        public async Task TaskStartedBeforeBeginKeepsDefaultKey()
        {
            using var ready = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            var pending = Task.Run(() =>
            {
                ready.Set();
                Assert.That(release.Wait(10000), Is.True);
                return BlackboxSession.CurrentKey;
            });
            try
            {
                Assert.That(ready.Wait(10000), Is.True);
                using var session = BlackboxSession.Begin();
                release.Set();
                Assert.That(await pending, Is.EqualTo(BlackboxSession.DefaultKey));
                Assert.That(BlackboxSession.CurrentKey, Is.EqualTo(session.SessionKey));
            }
            finally { release.Set(); await pending; }
        }

        [Test]
        public void ThreadStartedBeforeBeginKeepsDefaultKey()
        {
            using var ready = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            Guid threadKey = Guid.Empty;
            var thread = new Thread(() =>
            {
                ready.Set();
                if (release.Wait(10000)) threadKey = BlackboxSession.CurrentKey;
            });
            thread.Start();
            try
            {
                Assert.That(ready.Wait(10000), Is.True);
                using var session = BlackboxSession.Begin();
                release.Set();
                Assert.That(thread.Join(10000), Is.True);
                Assert.That(threadKey, Is.EqualTo(BlackboxSession.DefaultKey));
                Assert.That(BlackboxSession.CurrentKey, Is.EqualTo(session.SessionKey));
            }
            finally { release.Set(); thread.Join(10000); }
        }

        [Test]
        public void DisposingNestedSessionRestoresOuterKey()
        {
            using var outer = BlackboxSession.Begin();
            using (var inner = BlackboxSession.Begin())
            {
                Assert.That(BlackboxSession.CurrentKey, Is.EqualTo(inner.SessionKey));
                Assert.That(inner.SessionKey, Is.Not.EqualTo(outer.SessionKey));
            }
            Assert.That(BlackboxSession.CurrentKey, Is.EqualTo(outer.SessionKey));
        }

        [Test]
        public void CachedConstructorHandleStaysInDefaultSession()
        {
            var subject = new CachedRecorder();
            var originalBox = BlackboxRegistry.GetBlackbox(subject);
            using var session = BlackboxSession.Begin();
            subject.Write();
            Assert.That(subject.Handle.SessionKey, Is.EqualTo(BlackboxSession.DefaultKey));
            Assert.That(subject.Handle.SessionKey, Is.Not.EqualTo(BlackboxSession.CurrentKey));
            Assert.That(BlackboxRegistry.Contains(subject), Is.False);
            Assert.That(originalBox.GetLogs().Any(log => log.Message == "cached record"), Is.True);
            GC.KeepAlive(subject);
        }

        [Test]
        public void DefaultHandleHasEmptyKey() =>
            Assert.That(default(BlackboxHandle).SessionKey, Is.EqualTo(Guid.Empty));

        [TestCase("reset")]
        [TestCase("dispose")]
        [TestCase("disabled")]
        [TestCase("frozen")]
        public void InvalidatedHandlesHaveEmptyKeysWithoutWarnings(string reason)
        {
            using var session = BlackboxSession.Begin();
            var owner = new object();
            var handle = BlackboxHandle.Of(owner);
            Assert.That(handle.SessionKey, Is.EqualTo(session.SessionKey));
            switch (reason)
            {
                case "reset": BlackboxHandle.ForceReset(); break;
                case "dispose": session.Dispose(); break;
                case "disabled": BlackboxHandle.UseBlackbox = false; break;
                case "frozen": BlackboxSession.CurrentState.TryFreeze(new CrashInfo("test", string.Empty)); break;
            }
            var warnings = BlackboxTestDoubles.WarningLogs.Count;
            Assert.That(handle.IsValid, Is.False);
            Assert.That(handle.SessionKey, Is.EqualTo(Guid.Empty));
            Assert.That(BlackboxTestDoubles.WarningLogs.Count, Is.EqualTo(warnings));
            GC.KeepAlive(owner);
        }

        [Test]
        public void ResetChangesGenerationWithoutChangingSessionKeys()
        {
            var defaultKey = BlackboxSession.DefaultKey;
            BlackboxHandle.ForceReset();
            Assert.That(BlackboxSession.CurrentKey, Is.EqualTo(defaultKey));
            using var session = BlackboxSession.Begin();
            var owner = new object();
            var old = BlackboxHandle.Of(owner);
            BlackboxHandle.ForceReset();
            Assert.That(old.SessionKey, Is.EqualTo(Guid.Empty));
            Assert.That(BlackboxHandle.Of(owner).SessionKey, Is.EqualTo(session.SessionKey));
            Assert.That(BlackboxSession.CurrentKey, Is.EqualTo(session.SessionKey));
            Assert.That(BlackboxSession.DefaultKey, Is.EqualTo(defaultKey));
            GC.KeepAlive(owner);
        }

        [Test]
        public void DiagnosticPropertiesAreReadOnlyAndExposeNoSessionObject()
        {
            foreach (var name in new[] { "CurrentKey", "DefaultKey" })
            {
                var property = typeof(BlackboxSession).GetProperty(name, BindingFlags.Public | BindingFlags.Static);
                Assert.That(property.PropertyType, Is.EqualTo(typeof(Guid)));
                Assert.That(property.CanWrite, Is.False);
            }
            var handleKey = typeof(BlackboxHandle).GetProperty("SessionKey");
            Assert.That(handleKey.PropertyType, Is.EqualTo(typeof(Guid)));
            Assert.That(handleKey.CanWrite, Is.False);
            Assert.That(typeof(BlackboxSession).GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Any(property => property.PropertyType == typeof(BlackboxSession)), Is.False);
        }

        [Test]
        public void RepeatedKeyReadsAllocateNothingAndDoNotLog()
        {
            using var session = BlackboxSession.Begin();
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
                if (BlackboxSession.CurrentKey != session.SessionKey || BlackboxSession.DefaultKey == Guid.Empty ||
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
