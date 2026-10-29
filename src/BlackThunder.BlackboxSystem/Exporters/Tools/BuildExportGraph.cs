using System.Collections.Generic;
using System.Linq;

namespace BlackThunder.BlackboxSystem.Exporters
{
    internal class ExportNode
    {
        public Blackbox Blackbox { get; }
        public Blackbox ParentBlackbox { get; }
        public bool HasParent => ParentBlackbox != null;
        public int Depth { get; }
        public IReadOnlyList<LogData> Logs { get; }
        internal IReadOnlyDictionary<Blackbox, string> Names { get; set; }

        public ExportNode(Blackbox current, int depth, IReadOnlyList<LogData> logs) : this(current, null, depth, logs) { }
        public ExportNode(Blackbox current, Blackbox parent, int depth, IReadOnlyList<LogData> logs)
        {
            Blackbox = current;
            ParentBlackbox = parent;
            Depth = depth;
            Logs = logs;
        }
    }

    internal static partial class Tools
    {
        public static IReadOnlyList<ExportNode> BuildExportGraph(
            Blackbox root,
            int recursionDepth,
            bool fullExport,
            long maxSequence = -1)
        {
            var nodes = new List<ExportNode>();
            var names = new Dictionary<Blackbox, string>();
            var visited = new HashSet<Blackbox>();
            var maxDepth = recursionDepth >= 0 ? recursionDepth : int.MaxValue;

            BuildExportGraphRecursive(root, null, 0);
            void BuildExportGraphRecursive(Blackbox current, Blackbox parent, int depth)
            {
                if (current == null || visited.Contains(current))
                    return;

                visited.Add(current);

                CaptureName(current);
                // Copy retained entries before converting output-only values.
                var logs = Blackbox.MergeLogContext(
                    current
                        .GetLogsByContext(maxSequence)
                        .Select(FlattenSteps)
                        .Select(ResolveScopeDepths)
                    );

                for (int i = 0; i < logs.Count; i++)
                {
                    var log = logs[i];
                    CaptureName(log.Owner);
                    CaptureName(log.ExertedBy);
                    CaptureName(log.ExertingTo);
                    CaptureName(log.TaggedBy);
                    if (log.Tags != null) foreach (var tag in log.Tags) CaptureName(tag.Target);
                    log.DisplayNames = names;
                    logs[i] = log;
                }
                nodes.Add(parent != null
                    ? new ExportNode(current, parent, depth, logs)
                    : new ExportNode(current, depth, logs));
                nodes[nodes.Count - 1].Names = names;

                if (depth >= maxDepth)
                    return;

                // Collect connected blackboxes from the current node's logs.
                var peers = new List<Blackbox>();

                foreach (var log in logs)
                {
                    AddExportPeer(log.ExertedBy);

                    if (fullExport)
                        AddExportPeer(log.ExertingTo);
                }

                void AddExportPeer(Blackbox peer)
                {
                    if (peer == null || peer == current || peers.Contains(peer))
                        return;

                    peers.Add(peer);
                }

                // Visit connected blackboxes in the same order they first appeared.
                foreach (var peer in peers)
                    BuildExportGraphRecursive(peer, current, depth + 1);
            }

            return nodes;

            void CaptureName(Blackbox box)
            {
                if (box != null && !names.ContainsKey(box)) names.Add(box, box.DisplayName);
            }
        }
    }
}
