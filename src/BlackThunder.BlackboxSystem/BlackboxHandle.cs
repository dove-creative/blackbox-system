using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using BlackThunder.BlackboxSystem.Exporters;

namespace BlackThunder.BlackboxSystem
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

    public enum UseBlackboxOption
    {
        Use,
        DoNotUse,
        BasedOnSettings,
    }

    public partial struct BlackboxHandle
    {
        // Forwarders
        public readonly object Owner => _blackbox?.Owner;
        public readonly string OwnerString => _blackbox?.OwnerString ?? InvalidHandleMessage;
        public readonly long Id => _blackbox?.Id ?? -1;
        public readonly bool IsValid => _blackbox != null && _blackbox.State.CanWrite;
        internal readonly bool CanRecord => Blackbox != null;

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

        public static bool UseBlackbox
        {
            get => Infrastructure.UseBlackbox == UseBlackboxOption.Use;
            set => Infrastructure.UseBlackbox = value
                ? UseBlackboxOption.Use
                : UseBlackboxOption.DoNotUse;
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

        private readonly Blackbox _blackbox;
        private readonly Blackbox Blackbox => _blackbox != null && _blackbox.State.CheckWrite() ? _blackbox : null;
        private BlackboxHandle(Blackbox blackbox) { _blackbox = blackbox; }

        /// <summary>Assign an optional, immutable display label. The first nonblank label wins.</summary>
        public readonly BlackboxHandle Label(string label)
        {
            _blackbox?.SetLabel(label);
            return this;
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
            TargetTypes tagTargetTypes = TargetTypes.BasedOnSettings,
            UseBlackboxOption useBlackbox = UseBlackboxOption.BasedOnSettings)
        {
            Configure(logDirectory, logger, logger, strongReference, exportFormat, fullExportOption, openLogOption, exceptionHandlingOption, tagTargetTypes, useBlackbox);
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
            TargetTypes tagTargetTypes = TargetTypes.BasedOnSettings,
            UseBlackboxOption useBlackbox = UseBlackboxOption.BasedOnSettings)
        {
            BlackboxSession.CurrentState.UpdateSettings(s =>
            {
                s.LogDirectory = logDirectory;
                s.NormalLogger = normalLogger;
                s.WarningLogger = warningLogger;
                s.StrongReference = strongReference;
                if (useBlackbox != UseBlackboxOption.BasedOnSettings) s.UseBlackbox = useBlackbox;
                if (exportFormat != ExportFormat.BasedOnSettings) s.ExportFormat = exportFormat;
                if (fullExportOption != FullExportOption.BasedOnSettings) s.FullExportOption = fullExportOption;
                if (openLogOption != OpenLogOption.BasedOnSettings) s.OpenLogOption = openLogOption;
                if (exceptionHandlingOption != ExceptionHandlingOption.BasedOnSettings) s.ExceptionHandlingOption = exceptionHandlingOption;
                if ((tagTargetTypes & TargetTypes.BasedOnSettings) == 0) s.TagTargetTypes = tagTargetTypes;
            });
        }
        /// <summary>
        /// Debug-only reset helper for clearing registry and runtime state.
        /// </summary>
        /// <remarks>
        /// Retires the current generation without modifying other sessions or reusing its buffers.
        /// Existing handles remain bound to the retired generation.
        /// </remarks>
        public static void ForceReset() => BlackboxRegistry.ForceReset();
        #endregion

        #region Helpers
        public static BlackboxHandle Of<T>(T subject) where T : class
        {
            var state = BlackboxSession.CurrentState;
            if (state.Settings.UseBlackbox != UseBlackboxOption.Use) return default;

            if (subject == null)
                throw new ArgumentNullException(
                    nameof(subject),
                    $"{nameof(subject)} cannot be null.");

            return new BlackboxHandle(state.Subjects.GetOrAdd(subject));
        }

        public ScopeHandle Construct([CallerMemberName] string methodName = "") => Construct(out _, methodName);
        public ScopeHandle Construct(out BlackboxHandle blackboxHandle, [CallerMemberName] string methodName = "")
        {
            return Construct(string.Empty, out blackboxHandle, methodName);
        }
        public ScopeHandle Construct([InterpolatedStringHandlerArgument("")] ref WriteHandler handler, [CallerMemberName] string methodName = "") => Construct(ref handler, out _, methodName);
        public ScopeHandle Construct([InterpolatedStringHandlerArgument("")] ref WriteHandler handler, out BlackboxHandle blackboxHandle, [CallerMemberName] string methodName = "")
        {
            if (!handler.ShouldLog)
            {
                blackboxHandle = default;
                return default;
            }

            return Construct(handler.GetTextAndClear(), out blackboxHandle, methodName);
        }
        public ScopeHandle Construct(string message, [CallerMemberName] string methodName = "") => Construct(message, out _, methodName);
        public ScopeHandle Construct(string message, out BlackboxHandle blackboxHandle, [CallerMemberName] string methodName = "")
        {
            var blackbox = Blackbox;
            if (blackbox == null)
            {
                blackboxHandle = default;
                return default;
            }

            var constructMessage = $"[Ctor: {blackbox.DisplayName}]";
            if (!string.IsNullOrWhiteSpace(message))
                constructMessage += $" {message}";

            blackboxHandle = new BlackboxHandle(blackbox);
            return blackbox.WriteScope(constructMessage, methodName);
        }

        public readonly BlackboxHandle When(bool condition)
        {
            return condition ? this : default;
        }
        public BlackboxHandle When(Func<bool> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(
                    nameof(predicate),
                    FormatLogMessage($"{nameof(predicate)} cannot be null."));

            return predicate() ? this : default;
        }

        public BlackboxHandle Dispose()
        {
            return Dispose(string.Empty, nameof(Dispose));
        }
        public BlackboxHandle Dispose([InterpolatedStringHandlerArgument("")] ref WriteHandler handler, [CallerMemberName] string methodName = "")
        {
            if (!handler.ShouldLog) return default;
            return Dispose(handler.GetTextAndClear(), methodName);
        }
        public BlackboxHandle Dispose(string message, [CallerMemberName] string methodName = "")
        {
            var blackbox = Blackbox;
            if (blackbox == null) return default;

            var disposerMessage = $"[Disposed: {blackbox.DisplayName}]";
            if (!string.IsNullOrWhiteSpace(message))
                disposerMessage += $" {message}";

            blackbox.Write(disposerMessage, methodName);
            return new BlackboxHandle(blackbox);
        }
        #endregion

        public readonly TagHandle Write([InterpolatedStringHandlerArgument("")] ref WriteHandler handler, [CallerMemberName] string methodName = "")
        {
            if (!handler.ShouldLog) return default;
            return Write(handler.GetTextAndClear(), methodName);
        }
        public readonly TagHandle Write(string message, [CallerMemberName] string methodName = "")
        {
            return Blackbox?.Write(message, methodName) ?? TagHandle.FromMessage(message);
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
            return Blackbox?.ExertMessage(_blackbox.State.Subjects.GetOrAdd(other), message, methodName) ?? message;
        }

        public readonly ExertHandle Exert<T>(T other, [InterpolatedStringHandlerArgument("")] ref WriteHandler handler, [CallerMemberName] string methodName = "") where T : class
        {
            if (!handler.ShouldLog) return default;
            return Exert(other, handler.GetTextAndClear(), methodName);
        }
        public readonly ExertHandle Exert<T>(T other, string message, [CallerMemberName] string methodName = "") where T : class
        {
            return Blackbox?.Exert(_blackbox.State.Subjects.GetOrAdd(other), message, methodName) ?? default;
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
            
            var settings = _blackbox?.State.Settings ?? BlackboxSession.CurrentState.Settings;
            if ((exceptionHandlingOption == ExceptionHandlingOption.BasedOnSettings ? settings.ExceptionHandlingOption : exceptionHandlingOption) == ExceptionHandlingOption.CrashExport)
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

            var box = Blackbox;
            if (box == null) return messageStr;
            var settings = box.State.Settings;
            var crashMessage = messageStr;
            // Capture explicit target identities before freezing, without invoking Subject.ToString().
            if (others.HasValue && others.Value.Targets != null)
                foreach (var target in others.Value.Targets)
                    if (target.target != null)
                    {
                        var peer = box.State.Subjects.GetOrAdd(target.target);
                        if (peer != null) crashMessage += $" ({target.context}: {peer.DisplayName})";
                    }
            var crash = new CrashInfo(crashMessage, new StackTrace(true).ToString());
            if (!box.State.TryFreeze(crash)) return messageStr;
            settings.Log($"[Blackbox] CRASH: {messageStr}", LogLevel.Warning);
            ExportInternal(recursionDepth ?? settings.DefaultRecursionDepth, true, format, fullExport, openLog);
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
            ExportInternal(recursionDepth ?? (_blackbox?.State.Settings.DefaultRecursionDepth ?? DefaultRecursionDepth), false, format, fullExportOption, openLogOption);

        private readonly void ExportInternal(int recursionDepth, bool isCrash, ExportFormat format, FullExportOption fullExportOption, OpenLogOption openLogOption)
        {
            var blackbox = _blackbox;
            if (blackbox == null)
            {
                Infrastructure.Log("Cannot export because the handle is invalid.", LogLevel.Warning);
                return;
            }
            var settings = blackbox.State.Settings;
            var full = (fullExportOption == FullExportOption.BasedOnSettings ? settings.FullExportOption : fullExportOption) == FullExportOption.Full;
            var open = (openLogOption == OpenLogOption.BasedOnSettings ? settings.OpenLogOption : openLogOption) == OpenLogOption.Open;
            var resolvedFormat = format == ExportFormat.BasedOnSettings ? settings.ExportFormat : format;
            var maxSequence = blackbox.State.CurrentSequence;
            blackbox.State.Subjects.Cleanup();
            if (resolvedFormat == ExportFormat.Html)
                HtmlExporter.Export(blackbox, recursionDepth, isCrash, full, open, maxSequence, settings);
            else
                TxtExporter.Export(blackbox, recursionDepth, isCrash, full, open, maxSequence, settings);
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
