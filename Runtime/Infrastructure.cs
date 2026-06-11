using System;
using System.Threading;

namespace com.BlackThunder.BlackboxSystem
{
    public enum LogLevel
    {
        Normal,
        Warning,
    }

    internal static class Infrastructure
    {
        // Front
        public static string LogDirectory
        {
            get => _logDirectory;
            set => _logDirectory = value;
        }
        public static Action<string> NormalLogger
        {
            get => _normalLogger;
            set => _normalLogger = value;
        }
        public static Action<string> WarningLogger
        {
            get => _warningLogger;
            set => _warningLogger = value;
        }

        public static int MaxLogCount
        {
            get => _maxLogCount;
            set => _maxLogCount = value;
        }
        public static bool StrongReference
        {
            get => _strongReference;
            set => _strongReference = value;
        }
        public static int DefaultRecursionDepth
        {
            get => _defaultRecursionDepth;
            set => _defaultRecursionDepth = value;
        }

        public static bool IsPrinted => Volatile.Read(ref _isPrinted) != 0;
        private static int _isPrinted = 0;

        public static ExportFormat ExportFormat
        {
            get => _exportFormat;
            set
            {
                if (value != ExportFormat.BasedOnSettings)
                    _exportFormat = value;
            }
        }
        public static FullExportOption FullExportOption
        {
            get => _fullExportOption;
            set
            {
                if (value != FullExportOption.BasedOnSettings)
                    _fullExportOption = value;
            }
        }
        public static OpenLogOption OpenLogOption
        {
            get => _openLogOption;
            set
            {
                if (value != OpenLogOption.BasedOnSettings)
                    _openLogOption = value;
            }
        }
        public static ExceptionHandlingOption ExceptionHandlingOption
        {
            get => _exceptionHandlingOption;
            set
            {
                if (value != ExceptionHandlingOption.BasedOnSettings)
                    _exceptionHandlingOption = value;
            }
        }
        public static TargetTypes TagTargetTypes
        {
            get => _tagTargetTypes;
            set
            {
                if ((value & TargetTypes.BasedOnSettings) != TargetTypes.BasedOnSettings)
                    _tagTargetTypes = value;
            }
        }

        private static volatile string _logDirectory;
        private static volatile Action<string> _normalLogger;
        private static volatile Action<string> _warningLogger;
        private static volatile int _maxLogCount = 100;
        private static volatile bool _strongReference = false;
        private static volatile int _defaultRecursionDepth = 100;
        private static volatile ExportFormat _exportFormat = ExportFormat.Html;
        private static volatile FullExportOption _fullExportOption = FullExportOption.Full;
        private static volatile OpenLogOption _openLogOption = OpenLogOption.Open;
        private static volatile ExceptionHandlingOption _exceptionHandlingOption = ExceptionHandlingOption.None;
        private static volatile TargetTypes _tagTargetTypes = TargetTypes.Full;


        // Content
        public static bool TryMarkPrinted() => Interlocked.CompareExchange(ref _isPrinted, 1, 0) == 0;

        /// <summary>
        /// Resets the export-completion flag for debug or test reruns.
        /// </summary>
        /// <remarks>
        /// Call this only from a safe point, normally through BlackboxRegistry.ForceReset.
        /// It does not synchronize with active logging or export work.
        /// </remarks>
        public static void ForceResetRuntimeState() => Volatile.Write(ref _isPrinted, 0);

        public static bool Log(string message, LogLevel logLevel = LogLevel.Normal)
        {
            var logger = logLevel switch
            {
                LogLevel.Normal => NormalLogger,
                LogLevel.Warning => WarningLogger,
                _ => NormalLogger,
            };

            if (logger == null)
                return false;

            logger(message);
            return true;
        }

        public static ExportFormat Resolve(this ExportFormat format) => format == ExportFormat.BasedOnSettings ? ExportFormat : format;
        public static FullExportOption Resolve(this FullExportOption option) => option == FullExportOption.BasedOnSettings ? FullExportOption : option;
        public static OpenLogOption Resolve(this OpenLogOption option) => option == OpenLogOption.BasedOnSettings ? OpenLogOption : option;
        public static ExceptionHandlingOption Resolve(this ExceptionHandlingOption option) => option == ExceptionHandlingOption.BasedOnSettings ? ExceptionHandlingOption : option;
        public static TargetTypes Resolve(this TargetTypes targetTypes) =>
            (targetTypes & TargetTypes.BasedOnSettings) == TargetTypes.BasedOnSettings
                ? TagTargetTypes | (targetTypes & ~TargetTypes.BasedOnSettings)
                : targetTypes;
    }
}
