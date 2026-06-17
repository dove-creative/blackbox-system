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
            var visited = new HashSet<Blackbox>();
            var maxDepth = recursionDepth >= 0 ? recursionDepth : int.MaxValue;

            BuildExportGraphRecursive(root, null, 0);
            void BuildExportGraphRecursive(Blackbox current, Blackbox parent, int depth)
            {
                if (current == null || visited.Contains(current))
                    return;

                visited.Add(current);

                // Build the export node for the current blackbox.
                var logs = Blackbox.MergeLogContext(
                    current
                        .GetLogsByContext(maxSequence)
                        .Select(FlattenSteps)
                        .Select(ResolveScopeDepths)
                    );

                nodes.Add(parent != null
                    ? new ExportNode(current, parent, depth, logs)
                    : new ExportNode(current, depth, logs));

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
        }
    }
}
