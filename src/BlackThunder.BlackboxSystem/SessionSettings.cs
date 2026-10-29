using System;

namespace BlackThunder.BlackboxSystem
{
    // Mutated only while constructing an unpublished copy. Readers capture one reference.
    internal sealed class SessionSettings
    {
        internal string LogDirectory;
        internal Action<string> NormalLogger, WarningLogger;
        internal int MaxLogCount = 100, DefaultRecursionDepth = 100;
        internal bool StrongReference;
        internal UseBlackboxOption UseBlackbox = UseBlackboxOption.Use;
        internal ExportFormat ExportFormat = ExportFormat.Html;
        internal FullExportOption FullExportOption = FullExportOption.Full;
        internal OpenLogOption OpenLogOption = OpenLogOption.Open;
        internal ExceptionHandlingOption ExceptionHandlingOption = ExceptionHandlingOption.None;
        internal TargetTypes TagTargetTypes = TargetTypes.Full;
        internal SessionSettings Copy() => (SessionSettings)MemberwiseClone();
        internal bool Log(string message, LogLevel level = LogLevel.Normal)
        {
            var logger = level == LogLevel.Warning ? WarningLogger : NormalLogger;
            if (logger == null) return false;
            logger(message);
            return true;
        }
        internal TargetTypes Resolve(TargetTypes types) => (types & TargetTypes.BasedOnSettings) != 0
            ? TagTargetTypes | (types & ~TargetTypes.BasedOnSettings) : types;
    }
}
