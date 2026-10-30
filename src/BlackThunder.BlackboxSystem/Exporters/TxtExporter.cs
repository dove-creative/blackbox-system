using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace BlackThunder.BlackboxSystem.Exporters
{
    internal static class TxtExporter
    {
        // Front
#if BLACKBOX_TESTS
        internal static Action<ProcessStartInfo> OpenLogProcess = startInfo => Process.Start(startInfo);
#endif


        // Content --------------------
        // Export pipeline
        public static void Export(
            BlackboxSession session, Blackbox blackbox, int recursionDepth,
            bool isCrash, bool fullExport, bool openLog,
            long maxSequence = -1, SessionSettings settings = null)
        {
            settings ??= blackbox.State.Settings;
            var sb = new StringBuilder();
            var nodes = Tools.BuildExportGraph(blackbox, recursionDepth, fullExport, maxSequence);

            var crash = blackbox.State.Crash;
            if (crash != null) sb.AppendLine($"[Error] {crash.Message}\n[STACK TRACE]\n{crash.Stack}");
            BuildText(nodes, sb);

            var result = sb.ToString();

            var fullPath = Tools.WriteExportFile(session, blackbox, settings, ".txt", isCrash, result);

            settings.Log($"[TxtExporter] Log successfully exported to '{fullPath}'");

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
                    settings.Log(
                        $"[TxtExporter] Failed to open the file automatically.\n{ex.ToString()}",
                        LogLevel.Warning);
                }
            }
        }

        // Object and log rendering
        private static void BuildText(IReadOnlyList<ExportNode> nodes, StringBuilder sb)
        {
            var includedBlackboxes = new HashSet<Blackbox>();
            foreach (var node in nodes) includedBlackboxes.Add(node.Blackbox);

            foreach (var node in nodes)
            {
                var current = node.Blackbox;
                var parent = node.ParentBlackbox;
                var fromInfo = node.HasParent ? $" | From = {LogFormatter.ObjectRef(parent, node.Names)}" : "";
                var header = $"========= {LogFormatter.ObjectRef(current, node.Names)} (Depth = {node.Depth}{fromInfo}) =========";

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
