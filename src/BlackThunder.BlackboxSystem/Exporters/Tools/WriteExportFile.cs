using System.IO;
using System.Text;

namespace BlackThunder.BlackboxSystem.Exporters
{
    internal static partial class Tools
    {
        internal static string WriteExportFile(BlackboxSession session, Blackbox box, SessionSettings settings, string extension, bool crash, string text)
        {
            if (string.IsNullOrWhiteSpace(settings.LogDirectory))
                throw new System.InvalidOperationException("[Blackbox] LogDirectory is empty.");
            Directory.CreateDirectory(settings.LogDirectory);
            var type = box.TypeName;
            foreach (var ch in Path.GetInvalidFileNameChars()) type = type.Replace(ch, '_');
            if (type.Length > 80) type = type.Substring(0, 80);
            while (true)
            {
                var name = $"{(crash ? "[CRASH] " : "")}Blackbox {type} ({box.Id})-{session.SessionKey.ToString("N").Substring(0, 12)}-{session.NextExportOrdinal()}{extension}";
                var path = Path.Combine(settings.LogDirectory, name);
                FileStream stream;
                try { stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read); }
                catch (IOException) when (File.Exists(path)) { continue; }
                using (stream)
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(text);
                return path;
            }
        }
    }
}
