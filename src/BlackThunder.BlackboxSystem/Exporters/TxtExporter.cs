using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace BlackThunder.BlackboxSystem.Exporters
{
    internal static class TxtExporter
    {
#if BLACKBOX_TESTS
        internal static Action<ProcessStartInfo> OpenLogProcess = startInfo => Process.Start(startInfo);
#endif

        public static void Export(Blackbox blackbox, int recursionDepth, bool isCrash, bool fullExport, bool openLog, long maxSequence = -1)
        {
            if (string.IsNullOrWhiteSpace(Infrastructure.LogDirectory))
                throw new InvalidOperationException($"[TxtExporter] LogDirectory is empty.");

            Directory.CreateDirectory(Infrastructure.LogDirectory);

            var fileName = $"Blackbox {Tools.TrimSmart(blackbox.OwnerString)} ({blackbox.Id}).txt";
            if (isCrash) fileName = "[CRASH] " + fileName;

            var sb = new StringBuilder();
            var nodes = Tools.BuildExportGraph(blackbox, recursionDepth, fullExport, maxSequence);

            BuildText(nodes, sb);

            var result = sb.ToString();

            var fullPath = Path.Combine(Infrastructure.LogDirectory, fileName);
            File.WriteAllText(fullPath, result);

            Infrastructure.Log($"[TxtExporter] Log successfully exported to '{fullPath}'");

            if (openLog)
            {
                try
                {
                    var startInfo = new ProcessStartInfo(fullPath) { UseShellExecute = true };
#if BLACKBOX_TESTS
                    OpenLogProcess(startInfo);
#else
                    Process.Start(startInfo);
#endif
                }
                catch (Exception ex)
                {
                    Infrastructure.Log(
                        $"[TxtExporter] Failed to open the file automatically.\n{ex.ToString()}",
                        LogLevel.Warning);
                }
            }
        }

        private static void BuildText(IReadOnlyList<ExportNode> nodes, StringBuilder sb)
        {
            var includedBlackboxes = new HashSet<Blackbox>();
            foreach (var node in nodes) includedBlackboxes.Add(node.Blackbox);

            foreach (var node in nodes)
            {
                var current = node.Blackbox;
                var parent = node.ParentBlackbox;
                var fromInfo = node.HasParent ? $" | From = #{parent.Id}: {parent.OwnerString}" : "";
                var header = $"========= #{current.Id}: {current.OwnerString} (Depth = {node.Depth}{fromInfo}) =========";

                sb.AppendLine(header);
                sb.AppendLine(new string('-', 40));

                for (int i = 0; i < node.Logs.Count; i++)
                {
                    var log = node.Logs[i];

                    if (i > 0 && log.ScopeDepth == 0 && log.ScopeType == ScopeType.Open)
                        sb.AppendLine(new string('-', 40));

                    sb.AppendLine(log.ToString(log.ScopeDepth) + GetNotIncludedSuffix(log, includedBlackboxes));
                }

                sb.AppendLine();
                sb.AppendLine();
            }
        }

        private static string GetNotIncludedSuffix(LogData log, HashSet<Blackbox> includedBlackboxes)
        {
            var peerCount = 0;
            var missingPeers = new List<Blackbox>();

            AddPeer(log.ExertedBy);
            AddPeer(log.ExertingTo);

            if (missingPeers.Count == 0)
                return "";

            if (missingPeers.Count == 1 && peerCount == 1)
                return " (not included)";

            var result = "";
            foreach (var peer in missingPeers)
                result += $" (#{peer.Id} not included)";

            return result;

            void AddPeer(Blackbox peer)
            {
                if (peer == null)
                    return;

                peerCount++;
                if (!includedBlackboxes.Contains(peer) && !missingPeers.Contains(peer))
                    missingPeers.Add(peer);
            }
        }
    }
}
