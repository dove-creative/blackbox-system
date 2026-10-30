using System;
using System.Collections.Generic;
using System.Text;

namespace BlackThunder.BlackboxSystem
{
    internal static class LogFormatter
    {
        // Internal
        private const int IndentCount = 4;


        // Content --------------------
        // Message and line rendering
        public static string RenderMessage(LogData log) => RenderMessage(log, true);
        internal static string RenderMessage(LogData log, bool includeTagInteractionIds)
        {
            var message = ResolveTagMessage(log.Message, log.Tags, includeTagInteractionIds, log.DisplayNames);

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
                return $"{prefix}{message} ({ObjectRef(log.ExertedBy, log.DisplayNames)} {Arrow(true, log.InteractionId)} #{log.Owner.Id}: this {Arrow(true, log.InteractionId)} {ObjectRef(log.ExertingTo, log.DisplayNames)})";
            if (log.ExertedBy != null)
                return $"{prefix}{message} (#{log.Owner.Id}: this {Arrow(false, log.InteractionId)} {ObjectRef(log.ExertedBy, log.DisplayNames)})";
            if (log.ExertingTo != null)
                return $"{prefix}{message} (#{log.Owner.Id}: this {Arrow(true, log.InteractionId)} {ObjectRef(log.ExertingTo, log.DisplayNames)})";

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

        // Object and interaction references
        public static string ObjectRef(Blackbox blackbox, IReadOnlyDictionary<Blackbox, string> names = null)
        {
            if (blackbox == null)
                return "null";

            return names != null && names.TryGetValue(blackbox, out var name) ? name : blackbox.DisplayName;
        }

        public static string TagRef(Blackbox blackbox, long interactionId, IReadOnlyDictionary<Blackbox, string> names = null)
        {
            if (blackbox == null)
                return "null";

            if (interactionId < 0)
                return $"'{ObjectRef(blackbox, names)}'";

            return $"'{ObjectRef(blackbox, names)} [{interactionId}]'";
        }

        public static string Arrow(bool right, long interactionId)
        {
            if (interactionId >= 0)
                return right ? $"-[{interactionId}]->" : $"<-[{interactionId}]-";

            return right ? "->" : "<-";
        }

        // Tag rendering
        private static string RenderTaggedMessage(LogData log, string message, bool includeTagInteractionIds)
        {
            var targetTypes = log.TagTargetTypes;
            var builder = new StringBuilder("tagged");

            if (Has(targetTypes, TargetTypes.Name))
            {
                builder.Append(" by ");
                builder.Append(TagRef(log.TaggedBy, -1, log.DisplayNames));
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

        private static string ResolveTagMessage(
            string message, LogData.Tag[] tags, bool includeTagInteractionIds,
            IReadOnlyDictionary<Blackbox, string> names)
        {
            if (tags == null || tags.Length == 0)
                return message ?? string.Empty;

            var usedTags = new bool[tags.Length];
            var text = message ?? string.Empty;
            var builder = new StringBuilder(text.Length);
            for (int position = 0; position < text.Length;)
            {
                if (text[position] != '%' || position + 1 == text.Length || !char.IsDigit(text[position + 1]))
                {
                    builder.Append(text[position++]);
                    continue;
                }
                int end = position + 1;
                while (end < text.Length && char.IsDigit(text[end])) end++;
                if (int.TryParse(text.AsSpan(position + 1, end - position - 1), out int tagIndex) && tagIndex < tags.Length)
                {
                    usedTags[tagIndex] = true;
                    builder.Append(FormatTag(tags[tagIndex], includeTagInteractionIds, names));
                }
                else
                    builder.Append(text, position, end - position);
                position = end;
            }

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
                builder.Append(FormatTag(tags[i], includeTagInteractionIds, names));
                builder.Append(')');
            }

            return builder.ToString();
        }

        private static bool IsMessageOnlyTag(LogData.Tag tag) => tag.Target != null && tag.Id < 0;

        private static string FormatTag(LogData.Tag tag, bool includeTagInteractionIds, IReadOnlyDictionary<Blackbox, string> names) =>
            TagRef(tag.Target, includeTagInteractionIds ? tag.Id : -1, names);

        private static bool Has(TargetTypes targetTypes, TargetTypes flag) => (targetTypes & flag) == flag;
    }
}
