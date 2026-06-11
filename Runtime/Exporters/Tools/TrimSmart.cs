using System.IO;
using System.Linq;

namespace com.BlackThunder.BlackboxSystem.Exporters
{
    internal static partial class Tools
    {
        public static string TrimSmart(string input)
        {
            if (string.IsNullOrEmpty(input)) return "null";

            var invalid = Path.GetInvalidFileNameChars();
            var safe = new string(input.Where(ch => !invalid.Contains(ch)).ToArray());

            if (string.IsNullOrEmpty(safe)) return "null";

            return safe.Length > 30 ? safe[..15] + "..." + safe[^5..] : safe;
        }
    }
}
