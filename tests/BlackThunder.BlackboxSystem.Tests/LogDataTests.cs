using System;
using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    internal sealed class LogDataTests : BlackboxTestBase
    {
        [Test]
        public void ConstructorStoresValueMetadata()
        {
            var owner = BlackboxTestDoubles.Blackbox("owner");
            var time = new DateTime(2026, 5, 18, 1, 2, 3, DateTimeKind.Utc);

            // Table 1-1 / Value x Create
            var log = new LogData(owner, "hello", time, 3, "Run", ScopeType.None, -1, 7, "worker", -1, null, null, null, null);

            Assert.That(log.Owner, Is.SameAs(owner));
            Assert.That(log.Message, Is.EqualTo("hello"));
            Assert.That(log.Time, Is.EqualTo(time));
            Assert.That(log.Sequence, Is.EqualTo(3));
            Assert.That(log.MethodName, Is.EqualTo("Run"));
            Assert.That(log.ScopeType, Is.EqualTo(ScopeType.None));
            Assert.That(log.ScopeId, Is.EqualTo(-1));
            Assert.That(log.ThreadId, Is.EqualTo(7));
            Assert.That(log.ThreadName, Is.EqualTo("worker"));
            Assert.That(log.IsValid, Is.True);
        }

        [Test]
        public void ConstructorStoresTagMetadata()
        {
            var owner = BlackboxTestDoubles.Blackbox("owner");
            var taggedBy = BlackboxTestDoubles.Blackbox("source");
            var target = BlackboxTestDoubles.Blackbox("target");
            var tags = new[] { new LogData.Tag(4, target) };

            // Table 1-1 / Tagged x Create
            var log = new LogData(owner, "tagged", DateTime.UtcNow, 1, "Tag", ScopeType.None, -1, 1, "thread", 4, null, null, tags, taggedBy, TargetTypes.Name);

            Assert.That(log.IsTagged, Is.True);
            Assert.That(log.TaggedBy, Is.SameAs(taggedBy));
            Assert.That(log.Tags, Is.SameAs(tags));
            Assert.That(log.TagTargetTypes, Is.EqualTo(TargetTypes.Name));
            Assert.That(log.Tags[0].Target, Is.SameAs(target));
            Assert.That(log.Tags[0].Id, Is.EqualTo(4));
        }

        [Test]
        public void ConstructorStoresInteractionMetadata()
        {
            var owner = BlackboxTestDoubles.Blackbox("owner");
            var exertingTo = BlackboxTestDoubles.Blackbox("target");
            var exertedBy = BlackboxTestDoubles.Blackbox("source");

            // Table 1-1 / Interaction x Create
            var log = new LogData(owner, "call", DateTime.UtcNow, 2, "Call", ScopeType.None, -1, 1, "thread", 9, exertingTo, exertedBy, null, null);

            Assert.That(log.InteractionId, Is.EqualTo(9));
            Assert.That(log.ExertingTo, Is.SameAs(exertingTo));
            Assert.That(log.ExertedBy, Is.SameAs(exertedBy));
        }

        [Test]
        public void MutableExportFieldsCanBeUpdated()
        {
            var owner = BlackboxTestDoubles.Blackbox("owner");
            var peer = BlackboxTestDoubles.Blackbox("peer");

            var log = new LogData(owner, "before", DateTime.UtcNow, 2, "Run", ScopeType.Open, 5, 1, "thread", -1, null, null, null, null);

            // Table 1-1 / Value x MutateForExport
            log.ScopeDepth = 2;
            log.IsValid = false;
            log.Message = "after";

            // Table 1-1 / Interaction x MutateForExport
            log.InteractionId = 12;
            log.ExertingTo = peer;

            Assert.That(log.ScopeDepth, Is.EqualTo(2));
            Assert.That(log.IsValid, Is.False);
            Assert.That(log.Message, Is.EqualTo("after"));
            Assert.That(log.InteractionId, Is.EqualTo(12));
            Assert.That(log.ExertingTo, Is.SameAs(peer));
        }

        [Test]
        public void ToStringUsesFormatter()
        {
            var owner = BlackboxTestDoubles.Blackbox("owner");
            var log = new LogData(owner, "hello", DateTime.UtcNow, 11, "Run", ScopeType.None, -1, 7, "worker", -1, null, null, null, null);

            // Table 1-1 / Value x ToString
            var rendered = log.ToString();

            Assert.That(rendered, Does.Contain("[11]"));
            Assert.That(rendered, Does.Contain("[T7:worker]"));
            Assert.That(rendered, Does.Contain("[Run]"));
            Assert.That(rendered, Does.Contain("hello"));
        }
    }
}
