using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using com.BlackThunder.BlackboxSystem.Exporters;

namespace com.BlackThunder.BlackboxSystem
{
    public enum ExportFormat
    {
        Txt,
        Html,
        BasedOnSettings,
    }

    public enum FullExportOption
    {
        Focused,
        Full,
        BasedOnSettings,
    }

    public enum OpenLogOption
    {
        Open,
        Never,
        BasedOnSettings,
    }

    public enum ExceptionHandlingOption
    {
        None,
        CrashExport,
        BasedOnSettings,
    }

    public partial struct BlackboxHandle
    {
        // Forwarders
        public readonly object Owner => Blackbox?.Owner;
        public readonly string OwnerString => Blackbox?.OwnerString ?? InvalidHandleMessage;
        public readonly long Id => Blackbox?.Id ?? -1;
        public readonly bool IsValid => Blackbox != null;

        private const string InvalidHandleMessage = "[BlackboxHandle] Invalid Handle";

        public static string LogDirectory
        {
            get => Infrastructure.LogDirectory;
            set => Infrastructure.LogDirectory = value;
        }
        public static Action<string> NormalLogger
        {
            get => Infrastructure.NormalLogger;
            set => Infrastructure.NormalLogger = value;
        }
        public static Action<string> WarningLogger
        {
            get => Infrastructure.WarningLogger;
            set => Infrastructure.WarningLogger = value;
        }
        public static int MaxLogCount
        {
            get => Infrastructure.MaxLogCount;
            set => Infrastructure.MaxLogCount = value;
        }
        public static bool StrongReference
        {
            get => Infrastructure.StrongReference;
            set => Infrastructure.StrongReference = value;
        }
        public static int DefaultRecursionDepth
        {
            get => Infrastructure.DefaultRecursionDepth;
            set => Infrastructure.DefaultRecursionDepth = value;
        }

        public static ExportFormat ExportFormat
        {
            get => Infrastructure.ExportFormat;
            set => Infrastructure.ExportFormat = value;
        }
        public static FullExportOption FullExportOption
        {
            get => Infrastructure.FullExportOption;
            set => Infrastructure.FullExportOption = value;
        }
        public static OpenLogOption OpenLogOption
        {
            get => Infrastructure.OpenLogOption;
            set => Infrastructure.OpenLogOption = value;
        }
        public static TargetTypes TagTargetTypes
        {
            get => Infrastructure.TagTargetTypes;
            set => Infrastructure.TagTargetTypes = value;
        }

        // Internal
        private readonly Blackbox Blackbox
        {
            get
            {
                if (_blackbox != null) return _blackbox;
                if (_subject == null) return null;
                return BlackboxRegistry.GetBlackbox(_subject);
            }
        }
        private readonly Blackbox _blackbox;
        private readonly object _subject;


        // Content
        private BlackboxHandle(object subject)
        {
            _blackbox = null;
            _subject = subject;
        }
        private BlackboxHandle(object subject, Blackbox blackbox)
        {
            _blackbox = blackbox;
            _subject = subject;
        }

        #region Configuration
        public static void Configure(
            string logDirectory,
            Action<string> logger,
            bool strongReference = false,
            ExportFormat exportFormat = ExportFormat.BasedOnSettings,
            FullExportOption fullExportOption = FullExportOption.BasedOnSettings,
            OpenLogOption openLogOption = OpenLogOption.BasedOnSettings,
            ExceptionHandlingOption exceptionHandlingOption = ExceptionHandlingOption.BasedOnSettings,
            TargetTypes tagTargetTypes = TargetTypes.BasedOnSettings)
        {
            Configure(logDirectory, logger, logger, strongReference, exportFormat, fullExportOption, openLogOption, exceptionHandlingOption, tagTargetTypes);
        }

        public static void Configure(
            string logDirectory,
            Action<string> normalLogger,
            Action<string> warningLogger,
            bool strongReference = false,
            ExportFormat exportFormat = ExportFormat.BasedOnSettings,
            FullExportOption fullExportOption = FullExportOption.BasedOnSettings,
            OpenLogOption openLogOption = OpenLogOption.BasedOnSettings,
            ExceptionHandlingOption exceptionHandlingOption = ExceptionHandlingOption.BasedOnSettings,
            TargetTypes tagTargetTypes = TargetTypes.BasedOnSettings)
        {
            Infrastructure.LogDirectory = logDirectory;
            Infrastructure.NormalLogger = normalLogger;
            Infrastructure.WarningLogger = warningLogger;
            Infrastructure.StrongReference = strongReference;

            Infrastructure.ExportFormat = exportFormat;
            Infrastructure.FullExportOption = fullExportOption;
            Infrastructure.OpenLogOption = openLogOption;
            Infrastructure.ExceptionHandlingOption = exceptionHandlingOption;
            Infrastructure.TagTargetTypes = tagTargetTypes;
        }
        /// <summary>
        /// Debug-only reset helper for clearing registry and runtime state.
        /// </summary>
        /// <remarks>
        /// Call this only from a safe point where no other thread can read from or
        /// write to the Blackbox registry.
        /// </remarks>
        public static void ForceReset() => BlackboxRegistry.ForceReset();
        #endregion

        #region Helpers
        public static BlackboxHandle Of<T>(T subject) where T : class
        {
#if !BLACKBOX
            return default;
#else
            if (subject == null)
                throw new ArgumentNullException(
                    nameof(subject),
                    $"{nameof(subject)} cannot be null.");

            return new BlackboxHandle(subject);
#endif
        }

        public ScopeHandle Construct([CallerMemberName] string methodName = "") => Construct(out _, methodName);
        public ScopeHandle Construct(out BlackboxHandle blackboxHandle, [CallerMemberName] string methodName = "")
        {
#if !BLACKBOX
            blackboxHandle = default;
            return default;
#else
            return Construct(string.Empty, out blackboxHandle, methodName);
#endif
        }
        public ScopeHandle Construct([InterpolatedStringHandlerArgument("")] ref WriteHandler handler, [CallerMemberName] string methodName = "") => Construct(ref handler, out _, methodName);
        public ScopeHandle Construct([InterpolatedStringHandlerArgument("")] ref WriteHandler handler, out BlackboxHandle blackboxHandle, [CallerMemberName] string methodName = "")
        {
#if !BLACKBOX
            blackboxHandle = default;
            return default;
#else
            if (!handler.ShouldLog)
            {
                blackboxHandle = default;
                return default;
            }

            return Construct(handler.GetTextAndClear(), out blackboxHandle, methodName);
#endif
        }
        public ScopeHandle Construct(string message, [CallerMemberName] string methodName = "") => Construct(message, out _, methodName);
        public ScopeHandle Construct(string message, out BlackboxHandle blackboxHandle, [CallerMemberName] string methodName = "")
        {
#if !BLACKBOX
            blackboxHandle = default;
            return default;
#else
            var blackbox = Blackbox;
            if (blackbox == null)
            {
                blackboxHandle = default;
                return default;
            }

            var constructMessage = $"[Ctor: {blackbox.OwnerString}]";
            if (!string.IsNullOrWhiteSpace(message))
                constructMessage += $" {message}";

            blackboxHandle = new BlackboxHandle(_subject, blackbox);
            return blackbox.WriteScope(constructMessage, methodName);
#endif
        }

        public readonly BlackboxHandle When(bool condition)
        {
#if !BLACKBOX
            return default;
#else
            return condition ? this : default;
#endif
        }
        public BlackboxHandle When(Func<bool> predicate)
        {
#if !BLACKBOX
            return default;
#else
            if (predicate == null)
                throw new ArgumentNullException(
                    nameof(predicate),
                    FormatLogMessage($"{nameof(predicate)} cannot be null."));

            return predicate() ? this : default;
#endif
        }

        public BlackboxHandle Dispose()
        {
#if !BLACKBOX
            return default;
#else
            return Dispose(string.Empty, nameof(Dispose));
#endif
        }
        public BlackboxHandle Dispose([InterpolatedStringHandlerArgument("")] ref WriteHandler handler, [CallerMemberName] string methodName = "")
        {
#if !BLACKBOX
            return default;
#else
            if (!handler.ShouldLog) return default;
            return Dispose(handler.GetTextAndClear(), methodName);
#endif
        }
        public BlackboxHandle Dispose(string message, [CallerMemberName] string methodName = "")
        {
#if !BLACKBOX
            return default;
#else
            var blackbox = Blackbox;
            if (blackbox == null) return default;

            var disposerMessage = $"[Disposed: {blackbox.OwnerString}]";
            if (!string.IsNullOrWhiteSpace(message))
                disposerMessage += $" {message}";

            blackbox.Write(disposerMessage, methodName);
            return new BlackboxHandle(_subject, blackbox);
#endif
        }
        #endregion

        public readonly TagHandle Write([InterpolatedStringHandlerArgument("")] ref WriteHandler handler, [CallerMemberName] string methodName = "")
        {
#if !BLACKBOX
            return default;
#else
            if (!handler.ShouldLog) return default;
            return Write(handler.GetTextAndClear(), methodName);
#endif
        }
        public readonly TagHandle Write(string message, [CallerMemberName] string methodName = "")
        {
#if !BLACKBOX
            return TagHandle.FromMessage(message);
#else
            return Blackbox?.Write(message, methodName) ?? TagHandle.FromMessage(message);
#endif
        }

        public readonly ScopeHandle Scope([InterpolatedStringHandlerArgument("")] ref WriteHandler handler, [CallerMemberName] string methodName = "")
        {
            if (!handler.ShouldLog) return default;
            return Scope(handler.GetTextAndClear(), methodName);
        }
        public readonly ScopeHandle Scope(string message, [CallerMemberName] string methodName = "")
        {
            return Blackbox?.WriteScope(message, methodName) ?? default;
        }

        public readonly string ExertMessage<T>(T other, [InterpolatedStringHandlerArgument("")] ref WriteHandler handler, [CallerMemberName] string methodName = "") where T : class
        {
            if (!handler.ShouldLog) return handler.GetTextAndClear();
            return ExertMessage(other, handler.GetTextAndClear(), methodName);
        }
        public readonly string ExertMessage<T>(T other, string message, [CallerMemberName] string methodName = "") where T : class
        {
            return Blackbox?.ExertMessage(BlackboxRegistry.GetBlackbox(other), message, methodName) ?? message;
        }

        public readonly ExertHandle Exert<T>(T other, [InterpolatedStringHandlerArgument("")] ref WriteHandler handler, [CallerMemberName] string methodName = "") where T : class
        {
            if (!handler.ShouldLog) return default;
            return Exert(other, handler.GetTextAndClear(), methodName);
        }
        public readonly ExertHandle Exert<T>(T other, string message, [CallerMemberName] string methodName = "") where T : class
        {
            return Blackbox?.Exert(BlackboxRegistry.GetBlackbox(other), message, methodName) ?? default;
        }


        public readonly struct ErrorContainer
        {
            internal readonly (string context, object target)[] Targets;

            public ErrorContainer(params (string context, object target)[] targets) => Targets = targets;
            public static implicit operator ErrorContainer((string context, object target) target) => new ErrorContainer(target);
        }
        public readonly string WriteError(object message, ErrorContainer? others = null, ExceptionHandlingOption exceptionHandlingOption = ExceptionHandlingOption.BasedOnSettings, [CallerMemberName] string methodName = "")
        {
            var messageStr = ToMessageString(message);
            
            if (exceptionHandlingOption.Resolve() == ExceptionHandlingOption.CrashExport)
            {
                CrashExport(messageStr, others, methodName: methodName);
                return messageStr;
            }

            WriteErrorToBlackbox(messageStr, others, methodName);
            return messageStr;
        }
        public readonly string CrashExport(object message, ErrorContainer? others = null, int? recursionDepth = null, ExportFormat format = ExportFormat.Html, FullExportOption fullExport = FullExportOption.BasedOnSettings, OpenLogOption openLog = OpenLogOption.BasedOnSettings, [CallerMemberName] string methodName = "")
        {
            var messageStr = ToMessageString(message);

            Infrastructure.Log($"[Blackbox] CRASH: {messageStr}", LogLevel.Warning);
            WriteErrorToBlackbox(messageStr, others, methodName);
            Write($"[STACK TRACE]\n{new StackTrace(true)}\n");

            ExportInternal(recursionDepth ?? DefaultRecursionDepth, true, format, fullExport, openLog);
            return messageStr;
        }
        private readonly void WriteErrorToBlackbox(string message, ErrorContainer? others, string methodName)
        {
            var errorMessage = $"[Error] {message}";
            var targets = others.HasValue ? others.Value.Targets : null;

            if (targets == null || targets.Length == 0)
            {
                Write(errorMessage, methodName);
                return;
            }

            var tags = new object[targets.Length + 1];
            errorMessage += " (";

            for (int i = 0; i < targets.Length; i++)
            {
                var context = string.IsNullOrWhiteSpace(targets[i].context)
                    ? "target"
                    : targets[i].context;

                if (i > 0)
                    errorMessage += ", ";

                errorMessage += $"{context}: %{i}";
                tags[i] = targets[i].target;
            }

            errorMessage += ")";
            tags[targets.Length] = TargetTypes.Name | TargetTypes.InteractionId;
            Write(errorMessage, methodName).With(tags);
        }

        public readonly void Export(int? recursionDepth = null, ExportFormat format = ExportFormat.BasedOnSettings, FullExportOption fullExportOption = FullExportOption.BasedOnSettings, OpenLogOption openLogOption = OpenLogOption.BasedOnSettings) =>
            ExportInternal(recursionDepth ?? DefaultRecursionDepth, false, format, fullExportOption, openLogOption);

        private readonly void ExportInternal(int recursionDepth, bool isCrash, ExportFormat format, FullExportOption fullExportOption, OpenLogOption openLogOption)
        {
            var blackbox = Blackbox;
            if (blackbox == null)
            {
                Infrastructure.Log(FormatLogMessage(
                    $"Cannot export because the handle is invalid."),
                    LogLevel.Warning);
                return;
            }

            if (!Infrastructure.TryMarkPrinted())
            {
                Infrastructure.Log(FormatLogMessage(
                    $"Blackbox has already been exported. Skipping duplicate export."),
                    LogLevel.Warning);
                return;
            }

            var maxSequence = Blackbox.GetCurrentSequence();

            var fullExport = fullExportOption.Resolve() switch
            {
                FullExportOption.Full => true,
                FullExportOption.Focused => false,
                _ => false,
            };
            var openLog = openLogOption.Resolve() switch
            {
                OpenLogOption.Open => true,
                OpenLogOption.Never => false,
                _ => false,
            };

            switch (format.Resolve())
            {
                case ExportFormat.Html:
                    HtmlExporter.Export(blackbox, recursionDepth, isCrash, fullExport, openLog, maxSequence);
                    break;

                default:
                    TxtExporter.Export(blackbox, recursionDepth, isCrash, fullExport, openLog, maxSequence);
                    break;
            }
        }

        private readonly string ToMessageString(object obj) => obj?.ToString() ?? "null";

        private readonly string FormatLogMessage(string message)
        {
            var nametag = "BlackboxHandle";
            if (Blackbox != null) nametag += $": {Blackbox.OwnerString}";

            return $"[{nametag}] {message}";
        }
    }
}
