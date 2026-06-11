using System.Collections.Generic;
using System.Linq;

namespace com.BlackThunder.BlackboxSystem.Exporters
{
    internal static partial class Tools
    {
        /// <summary>
        /// Converts empty open/close scope pairs into step logs for export output.
        /// </summary>
        /// <param name="contextLogs">Sequence-ordered logs to flatten.</param>
        /// <returns>
        /// A new list that keeps the original order, skips the close log of each empty scope,
        /// and replaces the matching open log with a step log that preserves both messages.
        /// </returns>
        /// <remarks>
        /// A scope is considered empty when the next log in the same context log list
        /// is the matching close log with the same scope id.
        /// </remarks>
        public static List<LogData> FlattenSteps(IEnumerable<LogData> contextLogs)
        {
            var source = contextLogs.ToList();
            var result = new List<LogData>(source.Count);

            for (var i = 0; i < source.Count; i++)
            {
                var log = source[i];
                if (TryFindEmptyScopeCloseIndex(source, i, out var close))
                {
                    log.Message = CombineMessages(log.Message, close.Message);
                    log.ScopeType = ScopeType.Step;
                    i++;
                }

                result.Add(log);
            }

            return result;


            bool TryFindEmptyScopeCloseIndex(IReadOnlyList<LogData> contextLogs, int openIndex, out LogData close)
            {
                close = default;

                var open = contextLogs[openIndex];

                if (open.ScopeType != ScopeType.Open
                    || openIndex >= contextLogs.Count - 1
                    || !open.IsValid)
                    return false;

                var next = contextLogs[openIndex + 1];

                if (next.ScopeType != ScopeType.Close
                    || !next.IsValid
                    || next.ScopeId != open.ScopeId)
                    return false;

                close = next;
                return true;
            }

            string CombineMessages(string openMessage, string closeMessage)
            {
                if (string.IsNullOrEmpty(openMessage))
                    return closeMessage;

                if (string.IsNullOrEmpty(closeMessage))
                    return openMessage;

                return $"{openMessage} > {closeMessage}";
            }
        }
    }
}
