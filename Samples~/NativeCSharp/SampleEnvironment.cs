using System;
using System.IO;

namespace BlackThunder.BlackboxSystem.Samples.NativeCSharp
{
    internal static class SampleEnvironment
    {
        public static readonly string BaseLogDirectory
            = Path.Combine(AppContext.BaseDirectory, "BlackboxSystem", "Samples", "NativeCSharp");

        public static void Configure(string sampleName)
        {
            var logDirectory = Path.Combine(BaseLogDirectory, sampleName);

            BlackboxHandle.ForceReset();
            BlackboxHandle.Configure(
                logDirectory: logDirectory,
                normalLogger: Console.WriteLine,
                warningLogger: Console.WriteLine,
                exportFormat: ExportFormat.Html,
                openLogOption: OpenLogOption.Open,
                exceptionHandlingOption: ExceptionHandlingOption.CrashExport);
        }
    }
}
