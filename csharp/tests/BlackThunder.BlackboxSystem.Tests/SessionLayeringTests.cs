using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    // Recording code (Core) sees only SessionState; sessions, stores, trackers and the registry stay in Front.
    internal sealed class SessionLayeringTests : BlackboxTestBase
    {
        private const BindingFlags AllFields = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        [TestCase(typeof(Blackbox))]
        [TestCase(typeof(LogContext))]
        [TestCase(typeof(SessionState))]
        [RunWithUniTest]
        public void CoreTypesHoldNoFrontSessionParts(Type coreType)
        {
            foreach (var field in coreType.GetFields(AllFields))
                Assert.That(IsFrontSessionPart(field.FieldType), Is.False, $"{coreType.Name}.{field.Name}");
        }

        [Test]
        [RunWithUniTest]
        public void SessionOwnsStateStoreAndTrackers()
        {
            var fields = typeof(BlackboxSession).GetFields(AllFields).Select(field => field.FieldType).ToArray();
            Assert.That(fields, Does.Contain(typeof(SessionState)));
            Assert.That(fields, Does.Contain(typeof(SubjectStore)));
            Assert.That(fields, Does.Contain(typeof(HandleTracker<ScopeHandle>[])));
            Assert.That(fields, Does.Contain(typeof(HandleTracker<ExertHandle>[])));
        }

        [Test]
        [RunWithUniTest]
        public void BlackboxesShareTheirSessionStateInstance()
        {
            using var session = BlackboxHandle.BeginSession();
            var first = BlackboxSessionRegistry.GetBlackbox(new object());
            var second = BlackboxSessionRegistry.GetBlackbox(new object());
            Assert.That(first.State, Is.SameAs(session.State));
            Assert.That(second.State, Is.SameAs(session.State));
        }

        private static bool IsFrontSessionPart(Type type)
        {
            if (type.IsArray) return IsFrontSessionPart(type.GetElementType());
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HandleTracker<>)) return true;
            return type == typeof(BlackboxSession) || type == typeof(SubjectStore);
        }
    }
}
