using System.Collections.Generic;
using System.Linq;

namespace com.BlackThunder.BlackboxSystem.Exporters
{
    internal static partial class Tools
    {
        public static List<LogData> ResolveScopeDepths(IEnumerable<LogData> contextLogs)
        {
            var result = new List<LogData>(contextLogs.Count());
            int depth = 0;

            foreach (var log in contextLogs)
            {
                var current = log;

                if (current.ScopeType == ScopeType.Open)
                    current.ScopeDepth = depth++;
                else if (current.ScopeType == ScopeType.Close)
                    current.ScopeDepth = --depth;
                else
                    current.ScopeDepth = depth;

                result.Add(current);
            }

            return result;
        }
    }
}
