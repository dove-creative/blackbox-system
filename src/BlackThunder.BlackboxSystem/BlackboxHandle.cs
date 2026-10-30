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

    public readonly partial struct BlackboxHandle
    {
        // Forwarders
        public readonly object Owner => _blackbox?.Owner;
        public readonly string OwnerString => _blackbox?.OwnerString ?? InvalidHandleMessage;
        public readonly long Id => _blackbox?.Id ?? -1;
        public readonly bool IsValid => _blackbox != null && _blackbox.State.CanWrite;
        /// <summary>The bound session's key, or Guid.Empty when IsValid is false.</summary>
        public readonly Guid SessionKey => IsValid ? _session.SessionKey : Guid.Empty;
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
        // The session that owns _blackbox: target lookup, handle registration and export go through it.
        private readonly BlackboxSession _session;
        private readonly Blackbox Blackbox => _blackbox != null && _blackbox.State.CheckWrite() ? _blackbox : null;


        internal BlackboxHandle(Blackbox blackbox, BlackboxSession session)
        {
            _blackbox = blackbox;
            _session = session;
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
            Configure(logDirectory, logger, logger, strongReference, exportFormat, fullExportOption,
                openLogOption, exceptionHandlingOption, tagTargetTypes, useBlackbox);
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
            Infrastructure.Update(s =>
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
        #endregion

        #region Sessions
        /// <summary>
        /// Starts an isolated recording session for this execution flow and the Tasks/Threads it starts.
        /// </summary>
        /// <remarks>
        /// Call synchronously in the same logical scope that will dispose the session.
        /// The session copies the current settings; disposing it restores the previous binding.
        /// Explicit sessions are strongly retained until disposed, even if callers drop all references.
        /// </remarks>
        public static BlackboxSession BeginSession() => BlackboxSessionRegistry.Begin();
        /// <summary>The session key bound to this execution flow, or DefaultSessionKey when unbound.</summary>
        public static Guid CurrentSessionKey => BlackboxSessionRegistry.CurrentKey;
        /// <summary>The current default session's key. RestartDefaultSession creates a new key.</summary>
        public static Guid DefaultSessionKey => BlackboxSessionRegistry.DefaultKey;
        /// <summary>The process-wide number of undisposed explicit sessions, excluding the default session.</summary>
        /// <remarks>Uses a lock. Parallel tests observe each other's sessions.</remarks>
        public static int ActiveSessionCount => BlackboxSessionRegistry.ActiveSessionCount;
        /// <summary>Returns a snapshot of active explicit session keys in start order.</summary>
        /// <remarks>Uses a lock. The returned array is independent of the process-wide active session list.</remarks>
        public static Guid[] GetActiveSessionKeys() => BlackboxSessionRegistry.GetActiveSessionKeys();
        /// <summary>Disposes a snapshot of all active explicit sessions, leaving the default session unchanged.</summary>
        /// <remarks>
        /// Takes the snapshot under a lock, then disposes outside the lock in reverse start order
        /// so nested sessions in the calling flow restore their previous bindings.
        /// Does not change bindings in other execution flows: they remain bound to disposed sessions
        /// and further recording is rejected. Sessions started after the snapshot are not included.
        /// This is process-wide cleanup and can also dispose sessions owned by parallel tests.
        /// </remarks>
        public static void DisposeActiveSessions() => BlackboxSessionRegistry.DisposeActiveSessions();
        /// <summary>
        /// Ends the shared default session and replaces it with a new session using its settings.
        /// </summary>
        /// <remarks>
        /// Does not change an explicit session or the caller's execution-context binding.
        /// Existing default-session handles stop recording but can still export their retained logs.
        /// </remarks>
        public static void RestartDefaultSession() => BlackboxSessionRegistry.RestartDefaultSession();
        #endregion

        #region Helpers
        /// <summary>Get the subject's handle in the current session, optionally assigning an immutable display label.</summary>
        /// <param name="subject">The object whose activity is recorded.</param>
        /// <param name="label">The first nonblank label wins. Omission or blank labels preserve the existing name; conflicting labels warn.</param>
        public static BlackboxHandle Of<T>(T subject, string label = null) where T : class
        {
            var session = BlackboxSessionRegistry.Current;
            if (session.State.Settings.UseBlackbox != UseBlackboxOption.Use) return default;

            if (subject == null)
                throw new ArgumentNullException(
                    nameof(subject),
                    $"{nameof(subject)} cannot be null.");

            var blackbox = session.Subjects.GetOrAdd(subject);
            if (label != null) blackbox?.SetLabel(label);
            return new BlackboxHandle(blackbox, session);
        }

        public ScopeHandle Construct([CallerMemberName] string methodName = "") => Construct(out _, methodName);
        public ScopeHandle Construct(out BlackboxHandle blackboxHandle, [CallerMemberName] string methodName = "") =>
            Construct(string.Empty, out blackboxHandle, methodName);
        public ScopeHandle Construct(
            [InterpolatedStringHandlerArgument("")] ref WriteHandler handler,
            [CallerMemberName] string methodName = "") =>
            Construct(ref handler, out _, methodName);
        public ScopeHandle Construct(
            [InterpolatedStringHandlerArgument("")] ref WriteHandler handler,
            out BlackboxHandle blackboxHandle,
            [CallerMemberName] string methodName = "")
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

            blackboxHandle = new BlackboxHandle(blackbox, _session);
            return _session.OpenScope(blackbox, constructMessage, methodName);
        }

        public readonly BlackboxHandle When(bool condition) => condition ? this : default;
        public BlackboxHandle When(Func<bool> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(
                    nameof(predicate),
                    FormatLogMessage($"{nameof(predicate)} cannot be null."));

            return predicate() ? this : default;
        }

        public BlackboxHandle Dispose() => Dispose(string.Empty, nameof(Dispose));
        public BlackboxHandle Dispose(
            [InterpolatedStringHandlerArgument("")] ref WriteHandler handler,
            [CallerMemberName] string methodName = "")
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
            return new BlackboxHandle(blackbox, _session);
        }
        #endregion

        public readonly TagHandle Write(
            [InterpolatedStringHandlerArgument("")] ref WriteHandler handler,
            [CallerMemberName] string methodName = "")
        {
            if (!handler.ShouldLog) return default;
            return Write(handler.GetTextAndClear(), methodName);
        }
        public readonly TagHandle Write(string message, [CallerMemberName] string methodName = "")
        {
            var blackbox = Blackbox;
            return blackbox == null ? TagHandle.FromMessage(message) : blackbox.Write(message, methodName).Bind(_session);
        }

        public readonly ScopeHandle Scope(
            [InterpolatedStringHandlerArgument("")] ref WriteHandler handler,
            [CallerMemberName] string methodName = "")
        {
            if (!handler.ShouldLog) return default;
            return Scope(handler.GetTextAndClear(), methodName);
        }
        public readonly ScopeHandle Scope(string message, [CallerMemberName] string methodName = "")
        {
            var blackbox = Blackbox;
            return blackbox == null ? default : _session.OpenScope(blackbox, message, methodName);
        }

        public readonly string ExertMessage<T>(
            T other,
            [InterpolatedStringHandlerArgument("")] ref WriteHandler handler,
            [CallerMemberName] string methodName = "") where T : class
        {
            if (!handler.ShouldLog) return handler.GetTextAndClear();
            return ExertMessage(other, handler.GetTextAndClear(), methodName);
        }
        public readonly string ExertMessage<T>(T other, string message, [CallerMemberName] string methodName = "") where T : class =>
            Blackbox?.ExertMessage(_session.Subjects.GetOrAdd(other), message, methodName) ?? message;

        public readonly ExertHandle Exert<T>(
            T other,
            [InterpolatedStringHandlerArgument("")] ref WriteHandler handler,
            [CallerMemberName] string methodName = "") where T : class
        {
            if (!handler.ShouldLog) return default;
            return Exert(other, handler.GetTextAndClear(), methodName);
        }
        public readonly ExertHandle Exert<T>(T other, string message, [CallerMemberName] string methodName = "") where T : class
        {
            var blackbox = Blackbox;
            return blackbox == null ? default : _session.Exert(blackbox, _session.Subjects.GetOrAdd(other), message, methodName);
        }


        public readonly struct ErrorContainer
        {
            internal readonly (string context, object target)[] Targets;

            public ErrorContainer(params (string context, object target)[] targets) => Targets = targets;
            public static implicit operator ErrorContainer((string context, object target) target) => new ErrorContainer(target);
        }
        public readonly string WriteError(
            object message,
            ErrorContainer? others = null,
            ExceptionHandlingOption exceptionHandlingOption = ExceptionHandlingOption.BasedOnSettings,
            [CallerMemberName] string methodName = "")
        {
            var messageStr = ToMessageString(message);

            var settings = _blackbox?.State.Settings ?? BlackboxSessionRegistry.Current.State.Settings;
            if ((exceptionHandlingOption == ExceptionHandlingOption.BasedOnSettings
                ? settings.ExceptionHandlingOption
                : exceptionHandlingOption) == ExceptionHandlingOption.CrashExport)
            {
                CrashExport(messageStr, others, methodName: methodName);
                return messageStr;
            }

            WriteErrorToBlackbox(messageStr, others, methodName);
            return messageStr;
        }
        public readonly string CrashExport(
            object message,
            ErrorContainer? others = null,
            int? recursionDepth = null,
            ExportFormat format = ExportFormat.Html,
            FullExportOption fullExport = FullExportOption.BasedOnSettings,
            OpenLogOption openLog = OpenLogOption.BasedOnSettings,
            [CallerMemberName] string methodName = "")
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
                        var peer = _session.Subjects.GetOrAdd(target.target);
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

        public readonly void Export(
            int? recursionDepth = null,
            ExportFormat format = ExportFormat.BasedOnSettings,
            FullExportOption fullExportOption = FullExportOption.BasedOnSettings,
            OpenLogOption openLogOption = OpenLogOption.BasedOnSettings) =>
            ExportInternal(recursionDepth ?? (_blackbox?.State.Settings.DefaultRecursionDepth ?? DefaultRecursionDepth),
                false, format, fullExportOption, openLogOption);

        private readonly void ExportInternal(
            int recursionDepth,
            bool isCrash,
            ExportFormat format,
            FullExportOption fullExportOption,
            OpenLogOption openLogOption)
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
            _session.Subjects.Cleanup();
            if (resolvedFormat == ExportFormat.Html)
                HtmlExporter.Export(_session, blackbox, recursionDepth, isCrash, full, open, maxSequence, settings);
            else
                TxtExporter.Export(_session, blackbox, recursionDepth, isCrash, full, open, maxSequence, settings);
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
