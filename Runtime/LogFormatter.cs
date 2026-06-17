using System;
using System.Text;
using System.Text.RegularExpressions;

namespace BlackThunder.BlackboxSystem
{
    internal static class LogFormatter
    {
        private const int IndentCount = 4;

        public static string RenderMessage(LogData log)
        {
            return RenderMessage(log, true);
        }

        internal static string RenderMessage(LogData log, bool includeTagInteractionIds)
        {
            var message = ResolveTagMessage(log.Message, log.Tags, includeTagInteractionIds);

            if (log.IsTagged)
                return RenderTaggedMessage(log, message, includeTagInteractionIds);

            return message;
        }

        public static string RenderTextLine(LogData log, int scopeDepth)
        {
            var time = log.Time.ToString("HH:mm:ss.fffffff");
            var sequence = log.Sequence >= 0 ? log.Sequence.ToString() : "?";
            var thread = string.IsNullOrEmpty(log.ThreadName) ? $"T{log.ThreadId}" : $"T{log.ThreadId}:{log.ThreadName}";
            var validity = log.IsValid ? "" : "[Invalid] ";
            var indent = "";
            for (int i = 0; i < Math.Max(0, scopeDepth); i++)
                indent += $"|{new string(' ', Math.Max(0, IndentCount - 1))}";

            var prefix = $"{validity}[{sequence}] [{thread}] [{time}] {indent}";

            var methodLabel = RenderMethodLabel(log);
            if (!string.IsNullOrEmpty(methodLabel))
                prefix += $"{methodLabel} ";

            var message = RenderMessage(log);

            if (log.ExertedBy != null && log.ExertingTo != null)
                return $"{prefix}{message} (#{log.ExertedBy.Id}: {log.ExertedBy.OwnerString} {Arrow(true, log.InteractionId)} #{log.Owner.Id}: this {Arrow(true, log.InteractionId)} #{log.ExertingTo.Id}: {log.ExertingTo.OwnerString})";
            if (log.ExertedBy != null)
                return $"{prefix}{message} (#{log.Owner.Id}: this {Arrow(false, log.InteractionId)} #{log.ExertedBy.Id}: {log.ExertedBy.OwnerString})";
            if (log.ExertingTo != null)
                return $"{prefix}{message} (#{log.Owner.Id}: this {Arrow(true, log.InteractionId)} #{log.ExertingTo.Id}: {log.ExertingTo.OwnerString})";

            return $"{prefix}{message}";
        }

        public static string RenderMethodLabel(LogData log)
        {
            if (string.IsNullOrEmpty(log.MethodName))
                return string.Empty;

            return log.ScopeType switch
            {
                ScopeType.Open => $"<{log.MethodName}>",
                ScopeType.Close => $"</{log.MethodName}>",
                ScopeType.Step => $"<{log.MethodName} />",
                _ => $"[{log.MethodName}]",
            };
        }

        public static string ObjectRef(Blackbox blackbox)
        {
            if (blackbox == null)
                return "null";

            return $"{blackbox.OwnerString} #{blackbox.Id}";
        }

        public static string TagRef(Blackbox blackbox, long interactionId)
        {
            if (blackbox == null)
                return "null";

            if (interactionId < 0)
                return $"'{ObjectRef(blackbox)}'";

            return $"'{ObjectRef(blackbox)} [{interactionId}]'";
        }

        public static string Arrow(bool right, long interactionId)
        {
            if (interactionId >= 0)
                return right ? $"-[{interactionId}]->" : $"<-[{interactionId}]-";

            return right ? "->" : "<-";
        }

        private static string RenderTaggedMessage(LogData log, string message, bool includeTagInteractionIds)
        {
            var targetTypes = log.TagTargetTypes.Resolve();
            var builder = new StringBuilder("tagged");

            if (Has(targetTypes, TargetTypes.Name))
            {
                builder.Append(" by ");
                builder.Append(TagRef(log.TaggedBy, -1));
            }
            else if (includeTagInteractionIds && Has(targetTypes, TargetTypes.InteractionId) && log.InteractionId >= 0)
            {
                builder.Append(" [");
                builder.Append(log.InteractionId);
                builder.Append(']');
            }

            if (Has(targetTypes, TargetTypes.Message))
            {
                builder.Append(": ");
                builder.Append(message);
            }

            return builder.ToString();
        }

        private static string ResolveTagMessage(string message, LogData.Tag[] tags, bool includeTagInteractionIds)
        {
            if (tags == null || tags.Length == 0)
                return message ?? string.Empty;

            var usedTags = new bool[tags.Length];
            var resolved = Regex.Replace(
                message ?? string.Empty,
                @"%(\d+)",
                match =>
                {
                    if (!int.TryParse(match.Groups[1].Value, out int tagIndex) || tagIndex >= tags.Length)
                        return match.Value;

                    usedTags[tagIndex] = true;
                    return FormatTag(tags[tagIndex], includeTagInteractionIds);
                });

            var builder = new StringBuilder(resolved);
            for (int i = 0; i < tags.Length; i++)
            {
                if (usedTags[i])
                    continue;
                if (IsMessageOnlyTag(tags[i]))
                    continue;

                if (builder.Length > 0)
                    builder.Append(' ');

                builder.Append("(tag %");
                builder.Append(i);
                builder.Append(": ");
                builder.Append(FormatTag(tags[i], includeTagInteractionIds));
                builder.Append(')');
            }

            return builder.ToString();
        }

        private static bool IsMessageOnlyTag(LogData.Tag tag) => tag.Target != null && tag.Id < 0;

        private static string FormatTag(LogData.Tag tag, bool includeTagInteractionIds) =>
            TagRef(tag.Target, includeTagInteractionIds ? tag.Id : -1);

        private static bool Has(TargetTypes targetTypes, TargetTypes flag) => (targetTypes & flag) == flag;
    }
}
