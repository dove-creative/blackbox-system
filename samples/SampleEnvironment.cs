using System;
using System.IO;

namespace BlackThunder.BlackboxSystem.Samples
{
    internal static class SampleEnvironment
    {
        private static readonly string _outputDirectory
            = Path.Combine(AppContext.BaseDirectory, "SampleOutput");

        public static string GetOutputDirectory(string scenarioName) => Path.Combine(_outputDirectory, scenarioName);

        public static void Configure(string scenarioName, bool openLog)
        {
            var logDirectory = GetOutputDirectory(scenarioName);
            Directory.CreateDirectory(logDirectory);

            BlackboxHandle.Configure(
                logDirectory: logDirectory,
                normalLogger: null,
                warningLogger: Console.Error.WriteLine,
                exportFormat: ExportFormat.Html,
                openLogOption: openLog ? OpenLogOption.Open : OpenLogOption.Never,
                exceptionHandlingOption: ExceptionHandlingOption.CrashExport);
        }
    }
}
