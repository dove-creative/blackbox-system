using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

namespace com.BlackThunder.BlackboxSystem
{
    internal class LogContext
    {
        // Internal
        private readonly Blackbox _blackbox;
        private readonly LogData[] _logBuffer;
        private long _currentLogIndex = -1;

        private readonly int _threadId;
        private readonly string _threadName;

        private readonly List<ScopeInfo> _openScopes = new();
        private struct ScopeInfo
        {
            public readonly bool HasValue;

            public long ScopeId;
            public string MethodName;
            public bool IsAlive;

            public ScopeInfo(long scopeId, string methodName)
            {
                HasValue = true;

                ScopeId = scopeId;
                MethodName = methodName;
                IsAlive = true;
            }
        }


        // Content
        public LogContext(Blackbox blackbox, int bufferSize)
        {
            _blackbox = blackbox;
            _logBuffer = new LogData[bufferSize];
            _threadId = Thread.CurrentThread.ManagedThreadId;
            _threadName = Thread.CurrentThread.Name;
        }

        #region Enqueue Log
        public TagHandle EnqueueLog(string message, long sequence, string methodName, long scopeId = -1, long interactionId = -1, Blackbox exertingTo = null, Blackbox exertedBy = null)
        {
            EnqueueLog(message, sequence, methodName, ScopeType.None, scopeId, out var logIndex, interactionId, exertingTo, exertedBy);
            return new TagHandle(this, logIndex, message);
        }
        public void EnqueueTagLog(string message, long sequence, string methodName, long interactionId, Blackbox taggedBy = null) =>
            EnqueueTagLog(message, sequence, methodName, interactionId, taggedBy, new[] { new LogData.Tag(interactionId, taggedBy) }, TargetTypes.BasedOnSettings);

        public void EnqueueTagLog(string message, long sequence, string methodName, long interactionId, Blackbox taggedBy, LogData.Tag[] tags, TargetTypes tagTargetTypes) =>
            EnqueueLog(message, sequence, methodName, ScopeType.None, -1, out _, interactionId, null, null, tags, taggedBy, tagTargetTypes.Resolve());

        public TagHandle OpenScope(string message, long sequence, string methodName, long scopeId)
        {
            var foundScope = _openScopes.FirstOrDefault(scope => scope.ScopeId == scopeId);
            if (foundScope.HasValue)
            {
                Infrastructure.Log(
                    $"[LogContext] Scope '{scopeId}' is already open from '{foundScope.MethodName}'.",
                    LogLevel.Warning);
                return TagHandle.FromMessage(message);
            }

            _openScopes.Add(new ScopeInfo(scopeId, methodName));
            EnqueueLog(message, sequence, methodName, ScopeType.Open, scopeId, out var logIndex);
            return new TagHandle(this, logIndex, message);
        }

        public void CloseScope(long scopeId, long sequence)
        {
            // Sequence Provider
            bool inputSequenceUsed = false;
            long GetSequence()
            {
                if (!inputSequenceUsed)
                {
                    inputSequenceUsed = true;
                    return sequence;
                }
                else
                    return BlackboxRuntime.GetNextSequence();
            }


            ScopeInfo foundScope = default;
            int index = _openScopes.Count - 1;
            string warningMessage = string.Empty;

            for (; index >= 0; index--)
            {
                foundScope = _openScopes[index];
                if (foundScope.ScopeId == scopeId)
                    break;
            }

            if (index < 0)
            {
                Infrastructure.Log(
                    $"[LogContext] Scope '{scopeId}' cannot be closed because it is not open.",
                    LogLevel.Warning);
                return;
            }

            if (!foundScope.IsAlive)
            {
                _openScopes.Remove(foundScope);
                return;
            }

            var isClosedFromDifferentThread = Environment.CurrentManagedThreadId != _threadId;
            if (isClosedFromDifferentThread)
                warningMessage += $"[LogContext] Scope '{scopeId}' is being closed from a different thread.";

            int lastIndex = _openScopes.Count - 1;
            if (index < lastIndex)
            {
                if (isClosedFromDifferentThread)
                    warningMessage += $"\n- Newer scopes remain open because cross-thread close does not auto-close them.";
                else
                {
                    if (!string.IsNullOrEmpty(warningMessage)) warningMessage += "\n- ";
                    warningMessage += "Newer scopes found. They will be closed automatically.";

                    for (int i = lastIndex; i > index; i--)
                    {
                        var scope = _openScopes[i];
                        if (!scope.IsAlive) continue;

                        scope.IsAlive = false;
                        _openScopes[i] = scope;

                        EnqueueLog("Closed automatically because an outer scope closed first.", GetSequence(), scope.MethodName, ScopeType.Close, scope.ScopeId, out _);
                    }
                }
            }

            _openScopes.Remove(foundScope);
            EnqueueLog(string.Empty, GetSequence(), foundScope.MethodName, ScopeType.Close, scopeId, out _);

            if (!string.IsNullOrEmpty(warningMessage))
                Infrastructure.Log(warningMessage, LogLevel.Warning);
        }


        private void EnqueueLog(
            string message,
            long sequence,
            string methodName,
            ScopeType scopeType,
            long scopeId,
            out long logIndex,
            long interactionId = -1,
            Blackbox exertingTo = null,
            Blackbox exertedBy = null,
            LogData.Tag[] tags = null,
            Blackbox taggedBy = null,
            TargetTypes tagTargetTypes = TargetTypes.Full)
        {
            var time = DateTime.UtcNow;
            logIndex = ++_currentLogIndex;

            SetLog(logIndex, new LogData(
                _blackbox,
                message,
                time,
                sequence,
                methodName,
                scopeType,
                scopeId,
                _threadId,
                _threadName,
                interactionId,
                exertingTo,
                exertedBy,
                tags,
                taggedBy,
                tagTargetTypes));
        }
        #endregion


        public IEnumerable<LogData> GetLogs(long maxSequence = -1)
        {
            GetBufferedLogIndexRange(out var firstLogIndex, out var lastLogIndex);

            for (var logIndex = firstLogIndex; logIndex <= lastLogIndex; logIndex++)
            {
                var log = GetLog(logIndex);
                if (!log.IsValid) continue;

                if (maxSequence >= 0 && log.Sequence > maxSequence)
                    yield break;

                yield return log;
            }
        }

        #region Resolve Tags
        public string RenderMessage(long logIndex, string fallbackMessage)
        {
            if (!TryGetLog(logIndex, out var log))
                return fallbackMessage ?? string.Empty;

            return LogFormatter.RenderMessage(log);
        }

        public void ResolveWith(long logIndex, IEnumerable<Blackbox> tags, TargetTypes targetTypes)
        {
            if (!TryGetLog(logIndex, out var log))
                return;

            var resolvedTags = tags?.ToList() ?? new List<Blackbox>();
            if (resolvedTags.Count == 0) return;

            var resolvedTargetTypes = targetTypes.Resolve();

            var logTags = new LogData.Tag[resolvedTags.Count];
            for (int i = 0; i < resolvedTags.Count; i++)
            {
                var target = resolvedTags[i];
                var interactionId = target != null && target != _blackbox
                    ? BlackboxRuntime.GetNextInteractionId()
                    : -1;

                logTags[i] = new LogData.Tag(interactionId, target);
            }

            log.Tags = logTags;
            SetLog(logIndex, log);

            for (int i = 0; i < logTags.Length; i++)
            {
                var tag = logTags[i];
                var target = tag.Target;
                if (target == null || target == _blackbox)
                    continue;

                target.Tag(
                    log.Message,
                    log.MethodName,
                    _blackbox,
                    tag.Id,
                    logTags,
                    resolvedTargetTypes);
            }
        }
        #endregion

        #region Merge Scopes
        public bool TryMergeScope(long interactionId)
        {
            if (interactionId < 0)
                return false;

            GetBufferedLogIndexRange(out var firstLogIndex, out var lastLogIndex);
            for (var interactionIndex = lastLogIndex - 1; interactionIndex >= firstLogIndex; interactionIndex--)
            {
                var scopeIndex = interactionIndex + 1;

                var interactionLog = GetLog(interactionIndex);
                var scopeLog = GetLog(scopeIndex);

                if (!CanMergeScope(interactionId, interactionLog, scopeLog))
                    continue;

                if (!string.IsNullOrEmpty(interactionLog.Message))
                {
                    if (string.IsNullOrEmpty(scopeLog.Message))
                        scopeLog.Message = interactionLog.Message;
                    else if (scopeLog.Message != interactionLog.Message)
                        scopeLog.Message = $"{scopeLog.Message} (from: {interactionLog.Message})";
                }

                scopeLog.InteractionId = interactionLog.InteractionId;
                scopeLog.ExertedBy = interactionLog.ExertedBy ?? scopeLog.ExertedBy;
                scopeLog.ExertingTo = interactionLog.ExertingTo ?? scopeLog.ExertingTo;

                interactionLog.IsValid = false;
                SetLog(interactionIndex, interactionLog);
                SetLog(scopeIndex, scopeLog);

                return true;
            }

            return false;

            bool CanMergeScope(
                long interactionId,
                LogData interactionLog,
                LogData scopeLog)
            {
                return interactionLog.IsValid
                    && scopeLog.IsValid
                    && interactionLog.Owner == _blackbox
                    && scopeLog.Owner == _blackbox
                    && interactionLog.ScopeType == ScopeType.None
                    && scopeLog.ScopeType == ScopeType.Open
                    && interactionLog.InteractionId == interactionId
                    && scopeLog.ScopeId >= 0;
            }
        }
        #endregion


        // Ring buffer helpers
        private void GetBufferedLogIndexRange(out long firstLogIndex, out long lastLogIndex)
        {
            lastLogIndex = _currentLogIndex;
            firstLogIndex = Math.Max(0, lastLogIndex - _logBuffer.Length + 1);
        }

        private LogData GetLog(long logIndex) => _logBuffer[ToBufferIndex(logIndex)];
        private void SetLog(long logIndex, LogData log) => _logBuffer[ToBufferIndex(logIndex)] = log;

        private bool TryGetLog(long logIndex, out LogData log)
        {
            GetBufferedLogIndexRange(out var firstLogIndex, out var lastLogIndex);

            if (logIndex < firstLogIndex || logIndex > lastLogIndex)
            {
                log = default;
                return false;
            }

            log = GetLog(logIndex);
            return log.IsValid;
        }

        private int ToBufferIndex(long logIndex) => (int)(logIndex % _logBuffer.Length);
    }
}
