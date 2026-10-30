using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace BlackThunder.BlackboxSystem.Exporters
{
    internal static partial class HtmlExporter
    {
#if BLACKBOX_TESTS
        internal static Action<ProcessStartInfo> OpenLogProcess = startInfo => Process.Start(startInfo);
#endif

        // Export pipeline
        public static void Export(
            BlackboxSession session, Blackbox blackbox, int recursionDepth,
            bool isCrash, bool fullExport, bool openLog,
            long maxSequence = -1, SessionSettings settings = null)
        {
            settings ??= blackbox.State.Settings;
            var sb = new StringBuilder();
            var nodes = Tools.BuildExportGraph(blackbox, recursionDepth, fullExport, maxSequence);

            AppendDocumentStart(sb);
            var crash = blackbox.State.Crash;
            if (crash != null)
                sb.Append("<pre>" + HtmlEncode($"[Error] {crash.Message}\n[STACK TRACE]\n{crash.Stack}") + "</pre>");
            BuildHtml(nodes, sb);
            AppendDocumentEnd(sb);

            var fullPath = Tools.WriteExportFile(session, blackbox, settings, ".html", isCrash, sb.ToString());

            settings.Log($"[HtmlExporter] Exported to '{fullPath}'");

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
                        $"[HtmlExporter] Failed to open the file automatically.\n{ex.ToString()}",
                        LogLevel.Warning);
                }
            }
        }

        // Object and log rendering
        private static void BuildHtml(IReadOnlyList<ExportNode> nodes, StringBuilder sb)
        {
            var includedBlackboxes = new HashSet<Blackbox>();
            foreach (var node in nodes) includedBlackboxes.Add(node.Blackbox);

            foreach (var node in nodes)
            {
                var current = node.Blackbox;
                var parent = node.ParentBlackbox;
                sb.AppendLine($"<div class='box' id='b{current.Id}'>");
                var parentInfo = node.HasParent ? $" <span style='color:#888'>&larr; Called by <a href='#b{parent.Id}' style='color:#888'>#{parent.Id}</a></span>" : "";
                sb.AppendLine($"<div class='header'><div><span class='id-badge'>#{current.Id}</span> {HtmlEncode(LogFormatter.ObjectRef(current, node.Names))}{parentInfo}</div></div>");
                sb.AppendLine("<div class='log-container'>");

                foreach (var log in node.Logs)
                {
                    var indentPx = log.ScopeDepth * 20;
                    var contentStyle = $"padding-left: {indentPx}px";
                    var sequence = log.Sequence >= 0 ? log.Sequence.ToString() : "?";
                    var thread = string.IsNullOrEmpty(log.ThreadName) ? $"T{log.ThreadId}" : $"T{log.ThreadId}:{log.ThreadName}";

                    var scopeColor = _rainbowColors[log.ScopeDepth % _rainbowColors.Length];
                    var messageColor = _lightRainbowColors[(log.ScopeDepth + _rainbowColors.Length - 1) % _rainbowColors.Length];

                    var rowIdAttr = log.InteractionId >= 0 ? $"id='log_{current.Id}_{log.InteractionId}'" : "";

                    sb.Append($"<div class='log-row' {rowIdAttr} data-depth='{log.ScopeDepth}' data-type='{log.ScopeType}'>");
                    sb.Append($"<div class='time'>#{sequence} {HtmlEncode(thread)} {log.Time:mm:ss.ffffff}</div>");
                    sb.Append($"<div class='content' style='{contentStyle}'>");
                    AppendTagAnchors(sb, current, log);

                    var methodLabel = LogFormatter.RenderMethodLabel(log);
                    if (!string.IsNullOrEmpty(methodLabel))
                    {
                        var methodColor = log.ScopeType == ScopeType.None ? messageColor : scopeColor;
                        var classAttr = log.ScopeType == ScopeType.None ? " class='method'" : "";
                        sb.Append($"<span{classAttr} style='color:{methodColor}'>{HtmlEncode(methodLabel)}</span> ");
                    }

                    // Render the message before its interaction links.
                    sb.Append($"{HtmlEncode(LogFormatter.RenderMessage(log, false))}");

                    if (log.ExertedBy != null)
                        AppendInteraction(sb, log.ExertedBy, false, log.InteractionId, includedBlackboxes, node.Names);

                    if (log.ExertingTo != null)
                        AppendInteraction(sb, log.ExertingTo, true, log.InteractionId, includedBlackboxes, node.Names);

                    AppendTagInteractions(sb, current, log, includedBlackboxes, node.Names);

                    sb.AppendLine("</div></div>");
                }

                sb.AppendLine("</div></div>");
            }
        }

        // Interaction links
        private static void AppendInteraction(
            StringBuilder sb,
            Blackbox peer,
            bool right,
            long interactionId,
            HashSet<Blackbox> includedBlackboxes, IReadOnlyDictionary<Blackbox, string> names)
        {
            var peerId = peer.Id;
            var peerName = HtmlEncode(LogFormatter.ObjectRef(peer, names));
            var arrow = HtmlEncode(LogFormatter.Arrow(right, interactionId));

            if (includedBlackboxes.Contains(peer))
            {
                var href = interactionId >= 0 ? $"#log_{peerId}_{interactionId}" : $"#b{peerId}";
                sb.Append($"<a href='{href}' class='interaction' title='Interaction #{interactionId}'>{arrow} #{peerId}: {peerName}</a> ");
            }
            else
            {
                sb.Append($"<span class='interaction disabled' title='Not included in this export'>{arrow} #{peerId}: {peerName}</span> ");
            }
        }

        private static void AppendTagAnchors(StringBuilder sb, Blackbox current, LogData log)
        {
            if (log.Tags == null)
                return;

            foreach (var tag in log.Tags)
            {
                if (tag.Id < 0 || tag.Target == null || tag.Id == log.InteractionId)
                    continue;

                sb.Append($"<span id='log_{current.Id}_{tag.Id}'></span>");
            }
        }

        private static void AppendTagInteractions(
            StringBuilder sb,
            Blackbox current,
            LogData log,
            HashSet<Blackbox> includedBlackboxes, IReadOnlyDictionary<Blackbox, string> names)
        {
            if (log.IsTagged && log.TaggedBy != null && log.InteractionId >= 0)
                AppendTagInteraction(sb, log.TaggedBy, log.InteractionId, includedBlackboxes, names);

            if (log.Tags == null)
                return;

            foreach (var tag in log.Tags)
            {
                if (tag.Id < 0 || tag.Target == null || tag.Target == current)
                    continue;

                AppendTagInteraction(sb, tag.Target, tag.Id, includedBlackboxes, names);
            }
        }

        private static void AppendTagInteraction(
            StringBuilder sb,
            Blackbox peer,
            long interactionId,
            HashSet<Blackbox> includedBlackboxes, IReadOnlyDictionary<Blackbox, string> names)
        {
            var peerId = peer.Id;
            var peerName = HtmlEncode(LogFormatter.ObjectRef(peer, names));
            var label = interactionId >= 0 ? $"[{interactionId}]" : "[?]";

            if (includedBlackboxes.Contains(peer))
            {
                var href = interactionId >= 0 ? $"#log_{peerId}_{interactionId}" : $"#b{peerId}";
                sb.Append($"<a href='{href}' class='interaction tag-interaction' title='Tag #{interactionId}: #{peerId}: {peerName}'>{label}</a> ");
            }
            else
            {
                sb.Append($"<span class='interaction tag-interaction disabled' title='Tag target not included in this export: #{peerId}: {peerName}'>{label}</span> ");
            }
        }

        private static string HtmlEncode(string text) => System.Net.WebUtility.HtmlEncode(text);
    }
}
