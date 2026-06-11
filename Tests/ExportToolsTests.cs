using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using com.BlackThunder.BlackboxSystem.Exporters;
using NUnit.Framework;

namespace com.BlackThunder.BlackboxSystem.Tests
{
#if BLACKBOX
    internal sealed class ExportToolsTests : BlackboxTestBase
    {
        [Test]
        public void BuildExportGraphBuildsRootOnly()
        {
            var root = BlackboxTestDoubles.Blackbox("root");
            root.Write("hello", "Run");

            // Table 6-1 / RootOnly x BuildExportGraph
            var nodes = Tools.BuildExportGraph(root, 10, false);

            Assert.That(nodes.Count, Is.EqualTo(1));
            Assert.That(nodes[0].Blackbox, Is.SameAs(root));
            Assert.That(nodes[0].Depth, Is.EqualTo(0));
        }

        [Test]
        public void BuildExportGraphIncludesFocusedIncomingPeers()
        {
            var source = BlackboxTestDoubles.Blackbox("source");
            var target = BlackboxTestDoubles.Blackbox("target");
            source.ExertMessage(target, "call", "Run");

            // Table 6-1 / Focused x BuildExportGraph
            var nodes = Tools.BuildExportGraph(target, 10, false);

            Assert.That(nodes.Select(node => node.Blackbox).ToList(), Has.Member(source));
            Assert.That(nodes.Select(node => node.Blackbox).ToList(), Has.Member(target));
        }

        [Test]
        public void BuildExportGraphIncludesFullOutgoingPeers()
        {
            var source = BlackboxTestDoubles.Blackbox("source");
            var target = BlackboxTestDoubles.Blackbox("target");
            source.ExertMessage(target, "call", "Run");

            // Table 6-1 / Full x BuildExportGraph
            var focused = Tools.BuildExportGraph(source, 10, false);
            var full = Tools.BuildExportGraph(source, 10, true);

            Assert.That(focused.Count, Is.EqualTo(1));
            Assert.That(full.Select(node => node.Blackbox).ToList(), Has.Member(target));
        }

        [Test]
        public void BuildExportGraphRespectsDepthLimit()
        {
            var root = BlackboxTestDoubles.Blackbox("root");
            var middle = BlackboxTestDoubles.Blackbox("middle");
            var leaf = BlackboxTestDoubles.Blackbox("leaf");
            root.ExertMessage(middle, "first", "Run");
            middle.ExertMessage(leaf, "second", "Run");

            // Table 6-1 / DepthLimited x BuildExportGraph
            var nodes = Tools.BuildExportGraph(root, 1, true);

            Assert.That(nodes.Select(node => node.Blackbox).ToList(), Has.Member(root));
            Assert.That(nodes.Select(node => node.Blackbox).ToList(), Has.Member(middle));
            Assert.That(nodes.Select(node => node.Blackbox).ToList(), Has.No.Member(leaf));
        }

        [Test]
        public void FlattenStepsConvertsEmptyScopePair()
        {
            var owner = BlackboxTestDoubles.Blackbox("owner");
            var logs = new List<LogData>
            {
                Log(owner, "open", 0, ScopeType.Open, 1),
                Log(owner, "close", 1, ScopeType.Close, 1),
            };

            // Table 6-1 / RootOnly x FlattenSteps
            var result = Tools.FlattenSteps(logs);

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].ScopeType, Is.EqualTo(ScopeType.Step));
            Assert.That(result[0].Message, Is.EqualTo("open > close"));
        }

        [Test]
        public void FlattenStepsPreservesNonEmptyScope()
        {
            var owner = BlackboxTestDoubles.Blackbox("owner");
            var logs = new List<LogData>
            {
                Log(owner, "open", 0, ScopeType.Open, 1),
                Log(owner, "inside", 1, ScopeType.None, -1),
                Log(owner, "close", 2, ScopeType.Close, 1),
            };

            // Table 6-1 / Focused x FlattenSteps
            var result = Tools.FlattenSteps(logs);

            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(result[0].ScopeType, Is.EqualTo(ScopeType.Open));
            Assert.That(result[2].ScopeType, Is.EqualTo(ScopeType.Close));
        }

        [Test]
        public void ResolveScopeDepthsAssignsNestedDepths()
        {
            var owner = BlackboxTestDoubles.Blackbox("owner");
            var logs = new List<LogData>
            {
                Log(owner, "outer", 0, ScopeType.Open, 1),
                Log(owner, "inner", 1, ScopeType.Open, 2),
                Log(owner, "inside", 2, ScopeType.None, -1),
                Log(owner, "", 3, ScopeType.Close, 2),
                Log(owner, "", 4, ScopeType.Close, 1),
            };

            // Table 6-1 / Full x ResolveScopeDepths
            var result = Tools.ResolveScopeDepths(logs);

            Assert.That(result[0].ScopeDepth, Is.EqualTo(0));
            Assert.That(result[1].ScopeDepth, Is.EqualTo(1));
            Assert.That(result[2].ScopeDepth, Is.EqualTo(2));
            Assert.That(result[3].ScopeDepth, Is.EqualTo(1));
            Assert.That(result[4].ScopeDepth, Is.EqualTo(0));
        }

        [Test]
        public void TrimSmartSanitizesFileNameParts()
        {
            // Table 6-1 / FileName x TrimSmart
            Assert.That(Tools.TrimSmart(null), Is.EqualTo("null"));
            Assert.That(Tools.TrimSmart(string.Empty), Is.EqualTo("null"));

            var invalidChar = Path.GetInvalidFileNameChars().First();
            Assert.That(Tools.TrimSmart("a" + invalidChar + "b"), Is.EqualTo("ab"));

            var longName = "abcdefghijklmnopqrstuvwxyz0123456789";
            Assert.That(Tools.TrimSmart(longName), Is.EqualTo("abcdefghijklmno...56789"));
        }

        private static LogData Log(Blackbox owner, string message, long sequence, ScopeType scopeType, long scopeId)
        {
            return new LogData(owner, message, DateTime.UtcNow, sequence, "Run", scopeType, scopeId, 1, "thread", -1, null, null, null, null);
        }
    }
#endif
}
