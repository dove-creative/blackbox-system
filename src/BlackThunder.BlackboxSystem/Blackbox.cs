using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

namespace BlackThunder.BlackboxSystem
{
    internal sealed class ThreadToken
    {
        [ThreadStatic] private static ThreadToken _current;
        internal static ThreadToken Current => _current ??= new ThreadToken();
    }

    internal class Blackbox
    {
        private sealed class ContextNode
        {
            internal readonly ThreadToken Token;
            internal readonly LogContext Context;
            internal readonly ContextNode Next;
            internal ContextNode(ThreadToken token, LogContext context, ContextNode next)
            { Token = token; Context = context; Next = next; }
        }
        private readonly object _strongOwner;
        private readonly WeakReference<object> _weakOwner;
        private readonly int _bufferSize;
        private ContextNode _contexts;
        private string _label;
        private long _scopeId = -1;
        internal readonly SessionState State;
        internal readonly string TypeName;
        public long Id { get; }
        public object Owner => _strongOwner ?? (_weakOwner?.TryGetTarget(out var owner) == true ? owner : null);
        public string OwnerString => Volatile.Read(ref _label) ?? TypeName;
        internal string DisplayName
        {
            get
            {
                var label = Volatile.Read(ref _label);
                return label == null ? $"{TypeName} #{Id}" : $"{label} ({TypeName} #{Id})";
            }
        }
        internal IList<LogContext> LogContexts
        {
            get
            {
                var result = new List<LogContext>();
                for (var n = Volatile.Read(ref _contexts); n != null; n = n.Next) result.Add(n.Context);
                result.Reverse();
                return result;
            }
        }
        internal LogContext CurrentContext
        {
            get
            {
                var token = ThreadToken.Current;
                var head = Volatile.Read(ref _contexts);
                for (var n = head; n != null; n = n.Next)
                    if (ReferenceEquals(n.Token, token)) return n.Context;
                var context = new LogContext(this, _bufferSize);
                while (true)
                {
                    var node = new ContextNode(token, context, head);
                    var observed = Interlocked.CompareExchange(ref _contexts, node, head);
                    if (ReferenceEquals(observed, head)) return context;
                    head = observed;
                }
            }
        }
        internal Blackbox(object owner, bool strongReference, SessionState state)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            State = state;
            if (strongReference) _strongOwner = owner;
            else _weakOwner = new WeakReference<object>(owner);
            TypeName = TypeDisplayName.Format(owner.GetType());
            Id = state.NextBoxId();
            _bufferSize = state.Settings.MaxLogCount;
        }
        internal void SetLabel(string label)
        {
            if (!State.CheckWrite() || string.IsNullOrWhiteSpace(label)) return;
            // A string Subject must not become strongly rooted by its own label.
            if (_strongOwner == null && ReferenceEquals(Owner, label)) label = new string(label.ToCharArray());
            var old = Interlocked.CompareExchange(ref _label, label, null);
            if (old != null && !string.Equals(old, label, StringComparison.Ordinal))
                State.WarnOnce(SessionWarning.LabelAlreadySet, "[Blackbox] Label was already assigned; keeping the first label.");
        }
        public TagHandle Write(string message, string methodName)
        {
            if (!State.CheckWrite()) return TagHandle.FromMessage(message);
            return CurrentContext.EnqueueLog(message, -1, methodName);
        }
        internal string Tag(string message, string methodName, Blackbox taggedBy, long interactionId, LogData.Tag[] tags, TargetTypes targetTypes)
        {
            if (State.CheckWrite()) CurrentContext.EnqueueTagLog(message, -1, methodName, interactionId, taggedBy, tags, targetTypes);
            return message;
        }
        // Records the scope-open log. The owning session turns the result into a registered ScopeHandle.
        internal bool WriteScope(string message, string methodName, out LogContext context, out long scopeId, out TagHandle tag)
        {
            context = null;
            scopeId = -1;
            tag = TagHandle.FromMessage(message);
            if (!State.CheckWrite()) return false;
            context = CurrentContext;
            scopeId = Interlocked.Increment(ref _scopeId);
            tag = context.OpenScope(message, -1, methodName, scopeId);
            return true;
        }
        public string ExertMessage(Blackbox other, string message, string methodName)
        {
            if (State.CheckWrite()) EnqueueInteraction(other, out _, message, methodName);
            return message;
        }
        // Records the interaction. The owning session registers an ExertHandle for the target context.
        internal long Exert(Blackbox other, string message, string methodName, out LogContext target)
        {
            target = null;
            if (!State.CheckWrite()) return -1;
            return EnqueueInteraction(other, out target, message, methodName);
        }
        private long EnqueueInteraction(Blackbox other, out LogContext target, string message, string methodName)
        {
            target = null;
            if (!State.CanWrite) return -1;
            if (other == null) throw new ArgumentNullException(nameof(other));
            if (!ReferenceEquals(other.State, State)) return -1;
            var context = CurrentContext;
            target = other.CurrentContext;
            var id = State.NextInteractionId();
            if (other != this)
            {
                target.EnqueueLog(message, -1, methodName, -1, id, null, this);
                context.EnqueueLog(message, -1, methodName, -1, id, other);
            }
            else context.EnqueueLog(message, -1, methodName, -1, id, this, this);
            return id;
        }
        public List<LogData>[] GetLogsByContext(long maxSequence = -1)
        {
            var contexts = LogContexts;
            var logs = new List<LogData>[contexts.Count];
            for (int i = 0; i < logs.Length; i++)
                logs[i] = contexts[i].GetLogs(maxSequence).OrderBy(log => log.Sequence).ToList();
            return logs;
        }
        public List<LogData> GetLogs(long maxSequence = -1) => MergeLogContext(GetLogsByContext(maxSequence));
        public static List<LogData> MergeLogContext(IEnumerable<IEnumerable<LogData>> contextLogs)
        {
            var logs = contextLogs.SelectMany(logs => logs).ToList();
            logs.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
            return logs;
        }
    }
}
