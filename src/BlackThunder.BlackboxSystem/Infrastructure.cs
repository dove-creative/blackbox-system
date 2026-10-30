using System;

namespace BlackThunder.BlackboxSystem
{
    public enum LogLevel
    {
        Normal,
        Warning
    }

    internal static class Infrastructure
    {
        // Front
        public static string LogDirectory
        {
            get => Settings.LogDirectory;
            set => Update(s => s.LogDirectory = value);
        }
        public static Action<string> NormalLogger
        {
            get => Settings.NormalLogger;
            set => Update(s => s.NormalLogger = value);
        }
        public static Action<string> WarningLogger
        {
            get => Settings.WarningLogger;
            set => Update(s => s.WarningLogger = value);
        }
        public static int MaxLogCount
        {
            get => Settings.MaxLogCount;
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
                Update(s => s.MaxLogCount = value);
            }
        }
        public static bool StrongReference
        {
            get => Settings.StrongReference;
            set => Update(s => s.StrongReference = value);
        }
        public static int DefaultRecursionDepth
        {
            get => Settings.DefaultRecursionDepth;
            set => Update(s => s.DefaultRecursionDepth = value);
        }
        public static UseBlackboxOption UseBlackbox
        {
            get => Settings.UseBlackbox;
            set
            {
                if (value != UseBlackboxOption.BasedOnSettings) Update(s => s.UseBlackbox = value);
            }
        }
        public static ExportFormat ExportFormat
        {
            get => Settings.ExportFormat;
            set
            {
                if (value != ExportFormat.BasedOnSettings) Update(s => s.ExportFormat = value);
            }
        }
        public static FullExportOption FullExportOption
        {
            get => Settings.FullExportOption;
            set
            {
                if (value != FullExportOption.BasedOnSettings) Update(s => s.FullExportOption = value);
            }
        }
        public static OpenLogOption OpenLogOption
        {
            get => Settings.OpenLogOption;
            set
            {
                if (value != OpenLogOption.BasedOnSettings) Update(s => s.OpenLogOption = value);
            }
        }
        public static ExceptionHandlingOption ExceptionHandlingOption
        {
            get => Settings.ExceptionHandlingOption;
            set
            {
                if (value != ExceptionHandlingOption.BasedOnSettings) Update(s => s.ExceptionHandlingOption = value);
            }
        }
        public static TargetTypes TagTargetTypes
        {
            get => Settings.TagTargetTypes;
            set
            {
                if ((value & TargetTypes.BasedOnSettings) == 0) Update(s => s.TagTargetTypes = value);
            }
        }

        // Internal
        private static SessionSettings Settings => BlackboxSessionRegistry.Current.State.Settings;


        // Content --------------------
        // Session configuration
        internal static void Update(Action<SessionSettings> change)
        {
            var state = BlackboxSessionRegistry.Current.State;
            // A disposed session keeps its settings; the rejected change is reported once.
            if (!state.TryUpdateSettings(change)) state.CheckWrite();
        }

        // Logging and option resolution
        public static bool Log(string message, LogLevel level = LogLevel.Normal) => Settings.Log(message, level);

        public static UseBlackboxOption Resolve(this UseBlackboxOption option) =>
            option == UseBlackboxOption.BasedOnSettings ? UseBlackbox : option;
        public static ExportFormat Resolve(this ExportFormat option) =>
            option == ExportFormat.BasedOnSettings ? ExportFormat : option;
        public static FullExportOption Resolve(this FullExportOption option) =>
            option == FullExportOption.BasedOnSettings ? FullExportOption : option;
        public static OpenLogOption Resolve(this OpenLogOption option) =>
            option == OpenLogOption.BasedOnSettings ? OpenLogOption : option;
        public static ExceptionHandlingOption Resolve(this ExceptionHandlingOption option) =>
            option == ExceptionHandlingOption.BasedOnSettings ? ExceptionHandlingOption : option;
        public static TargetTypes Resolve(this TargetTypes option) => Settings.Resolve(option);
    }
}
