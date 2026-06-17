using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

namespace BlackThunder.BlackboxSystem
{
    internal class Blackbox
    {
        // Front
        public object Owner
        {
            get
            {
                if (_strongOwner != null) return _strongOwner;
                if (_weakOwner?.TryGetTarget(out var owner) ?? false) return owner;
                return null;
            }
        }

        public string OwnerString
        {
            get
            {
                try
                {
                    var owner = Owner;

                    if (owner != null)
                        return owner.ToString() ?? "null";

                    if (!string.IsNullOrEmpty(_ownerDescription))
                        return $"{_ownerDescription} (Reference Lost)";

                    return "null";
                }
                catch (Exception ex)
                {
                    if (!string.IsNullOrEmpty(_ownerDescription))
                        return $"{_ownerDescription} (Fallback: {ex.GetType().Name})";

                    return "null";
                }
            }
        }

        public long Id { get; }

        internal IList<LogContext> LogContexts => _logContext.Values;


        // Internal
        private readonly object _strongOwner;
        private readonly WeakReference<object> _weakOwner;
        private readonly string _ownerDescription;

        private readonly ThreadLocal<LogContext> _logContext;
        private long _scopeId = -1;


        // Content
        public Blackbox(object owner, bool strongReference)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner), "[Blackbox] Owner must not be null");

            if (strongReference)
                _strongOwner = owner;
            else
                _weakOwner = new(owner);

            Id = BlackboxRuntime.GetNextBlackboxId();
            _ownerDescription = owner.ToString();

            var bufferSize = Infrastructure.MaxLogCount;
            _logContext = new ThreadLocal<LogContext>(() => new LogContext(this, bufferSize), true);
        }

        public static long GetCurrentSequence() => BlackboxRuntime.CurrentSequence;


        public TagHandle Write(string message, string methodName)
        {
            if (Infrastructure.IsPrinted)
                return TagHandle.FromMessage(message);

            return EnqueueLog(message, methodName);
        }

        internal string Tag(string message, string methodName, Blackbox taggedBy, long interactionId, LogData.Tag[] tags, TargetTypes targetTypes)
        {
            if (Infrastructure.IsPrinted)
                return message;

            EnqueueTagLog(message, methodName, interactionId, taggedBy, tags, targetTypes);
            return message;
        }

        public ScopeHandle WriteScope(string message, string methodName)
        {
            if (Infrastructure.IsPrinted)
                return default;

            var context = _logContext.Value;
            var scopeId = Interlocked.Increment(ref _scopeId);

            var tagHandle = context.OpenScope(message, BlackboxRuntime.GetNextSequence(), methodName, scopeId);
            return new ScopeHandle(context, scopeId, tagHandle);
        }

        public string ExertMessage(Blackbox other, string message, string methodName)
        {
            if (Infrastructure.IsPrinted)
                return message;

            EnqueueInteraction(other, out _, message, methodName);
            return message;
        }

        public ExertHandle Exert(Blackbox other, string message, string methodName)
        {
            if (Infrastructure.IsPrinted)
                return default;

            var interactionId = EnqueueInteraction(other, out var targetContext, message, methodName);
            return new ExertHandle(targetContext, interactionId);
        }

        private long EnqueueInteraction(Blackbox other, out LogContext targetContext, string message, string methodName)
        {
            if (Infrastructure.IsPrinted)
            {
                targetContext = default;
                return -1;
            }

            if (other == null)
                throw new ArgumentNullException(nameof(other));

            var context = _logContext.Value;
            var interactionId = BlackboxRuntime.GetNextInteractionId();

            if (other != this)
            {
                other.EnqueueLog(out targetContext, message, methodName, interactionId, null, this);
                EnqueueLog(context, message, methodName, interactionId, other, null);
            }
            else
            {
                targetContext = context;
                EnqueueLog(context, message, methodName, interactionId, this, this);
            }

            return interactionId;
        }


        #region Enqueue Log
        private TagHandle EnqueueLog(string message, string methodName) =>
            EnqueueLog(_logContext.Value, message, methodName, -1, null, null);
        private void EnqueueLog(out LogContext context, string message, string methodName, long interactionId, Blackbox exertingTo, Blackbox exertedBy)
        {
            context = _logContext.Value;
            EnqueueLog(context, message, methodName, interactionId, exertingTo, exertedBy);
        }
        private TagHandle EnqueueLog(LogContext context, string message, string methodName, long interactionId, Blackbox exertingTo, Blackbox exertedBy) =>
            context.EnqueueLog(message, BlackboxRuntime.GetNextSequence(), methodName, -1, interactionId, exertingTo, exertedBy);

        private void EnqueueTagLog(string message, string methodName, long interactionId, Blackbox taggedBy, LogData.Tag[] tags, TargetTypes targetTypes) =>
            _logContext.Value.EnqueueTagLog(message, BlackboxRuntime.GetNextSequence(), methodName, interactionId, taggedBy, tags, targetTypes);
        #endregion


        public List<LogData>[] GetLogsByContext(long maxSequence = -1)
        {
            var contexts = _logContext.Values;
            var logs = new List<LogData>[contexts.Count];

            for (int i = 0; i < logs.Length; i++)
                logs[i] = contexts[i].GetLogs(maxSequence).ToList();

            return logs;
        }

        public List<LogData> GetLogs(long maxSequence = -1) => MergeLogContext(GetLogsByContext(maxSequence));

        public static List<LogData> MergeLogContext(IEnumerable<IEnumerable<LogData>> contextLogs)
        {
            var logs = contextLogs.SelectMany(contextLogs => contextLogs).ToList();
            logs.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));

            return logs;
        }
    }
}
