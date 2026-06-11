using System;
using NUnit.Framework;

namespace com.BlackThunder.BlackboxSystem.Tests
{
#if BLACKBOX
    internal sealed class LogFormatterTests : BlackboxTestBase
    {
        [Test]
        public void RenderMessageReplacesTagPlaceholders()
        {
            var owner = BlackboxTestDoubles.Blackbox("owner");
            var target = BlackboxTestDoubles.Blackbox("target");
            var tags = new[] { new LogData.Tag(8, target) };
            var log = new LogData(owner, "hello %0", DateTime.UtcNow, 0, "Run", ScopeType.None, -1, 1, "thread", -1, null, null, tags, null);

            // Table 1-2 / Ready x RenderMessage
            var rendered = LogFormatter.RenderMessage(log);

            Assert.That(rendered, Does.Contain("hello"));
            Assert.That(rendered, Does.Contain("target"));
            Assert.That(rendered, Does.Contain("[8]"));
        }

        [Test]
        public void RenderMessageAppendsUnusedTags()
        {
            var owner = BlackboxTestDoubles.Blackbox("owner");
            var target = BlackboxTestDoubles.Blackbox("target");
            var tags = new[] { new LogData.Tag(3, target) };
            var log = new LogData(owner, "hello", DateTime.UtcNow, 0, "Run", ScopeType.None, -1, 1, "thread", -1, null, null, tags, null);

            // Table 1-2 / Ready x TagRef
            var rendered = LogFormatter.RenderMessage(log);

            Assert.That(rendered, Does.Contain("(tag %0:"));
            Assert.That(rendered, Does.Contain("target"));
        }

        [Test]
        public void RenderTaggedMessageHonorsTargetTypes()
        {
            var owner = BlackboxTestDoubles.Blackbox("target");
            var source = BlackboxTestDoubles.Blackbox("source");

            // Table 1-2 / Ready x RenderMessage
            var nameOnly = new LogData(owner, "message", DateTime.UtcNow, 0, "Tag", ScopeType.None, -1, 1, "thread", 5, null, null, null, source, TargetTypes.Name);
            var full = new LogData(owner, "message", DateTime.UtcNow, 0, "Tag", ScopeType.None, -1, 1, "thread", 5, null, null, null, source, TargetTypes.Full);
            var none = new LogData(owner, "message", DateTime.UtcNow, 0, "Tag", ScopeType.None, -1, 1, "thread", 5, null, null, null, source, TargetTypes.None);

            var fullMessage = LogFormatter.RenderMessage(full);

            Assert.That(LogFormatter.RenderMessage(nameOnly), Does.Contain("tagged by"));
            Assert.That(fullMessage, Is.EqualTo("tagged by 'source #1': message"));
            Assert.That(LogFormatter.RenderMessage(none), Is.EqualTo("tagged"));
        }

        [Test]
        public void RenderTextLineIncludesSequenceThreadScopeAndInteraction()
        {
            var owner = BlackboxTestDoubles.Blackbox("owner");
            var target = BlackboxTestDoubles.Blackbox("target");
            var log = new LogData(owner, "call", DateTime.UtcNow, 14, "Run", ScopeType.Open, 6, 8, "worker", 2, target, null, null, null);

            // Table 1-2 / Ready x RenderTextLine
            var rendered = LogFormatter.RenderTextLine(log, 1);

            Assert.That(rendered, Does.Contain("[14]"));
            Assert.That(rendered, Does.Contain("[T8:worker]"));
            Assert.That(rendered, Does.Contain("<Run>"));
            Assert.That(rendered, Does.Contain("call"));
            Assert.That(rendered, Does.Contain("-[2]->"));
            Assert.That(rendered, Does.Contain("target"));
        }

        [Test]
        public void RenderMethodLabelDependsOnScopeType()
        {
            var owner = BlackboxTestDoubles.Blackbox("owner");

            // Table 1-2 / Ready x RenderMethodLabel
            Assert.That(LogFormatter.RenderMethodLabel(Log("Run", ScopeType.None)), Is.EqualTo("[Run]"));
            Assert.That(LogFormatter.RenderMethodLabel(Log("Run", ScopeType.Open)), Is.EqualTo("<Run>"));
            Assert.That(LogFormatter.RenderMethodLabel(Log("Run", ScopeType.Close)), Is.EqualTo("</Run>"));
            Assert.That(LogFormatter.RenderMethodLabel(Log("Run", ScopeType.Step)), Is.EqualTo("<Run />"));
            Assert.That(LogFormatter.RenderMethodLabel(Log("", ScopeType.None)), Is.EqualTo(string.Empty));

            LogData Log(string methodName, ScopeType scopeType)
            {
                return new LogData(owner, "message", DateTime.UtcNow, 0, methodName, scopeType, 1, 1, "thread", -1, null, null, null, null);
            }
        }

        [Test]
        public void ArrowAndTagRefHandleInteractionIds()
        {
            var owner = BlackboxTestDoubles.Blackbox("owner");

            // Table 1-2 / Ready x Arrow
            Assert.That(LogFormatter.Arrow(true, 7), Is.EqualTo("-[7]->"));
            Assert.That(LogFormatter.Arrow(false, 7), Is.EqualTo("<-[7]-"));
            Assert.That(LogFormatter.Arrow(true, -1), Is.EqualTo("->"));

            // Table 1-2 / Ready x TagRef
            Assert.That(LogFormatter.TagRef(owner, 3), Does.Contain("[3]"));
            Assert.That(LogFormatter.TagRef(owner, -1), Does.Not.Contain("["));
            Assert.That(LogFormatter.TagRef(null, -1), Is.EqualTo("null"));
        }
    }
#endif
}
