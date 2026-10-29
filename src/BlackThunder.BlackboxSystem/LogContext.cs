using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

namespace BlackThunder.BlackboxSystem
{
    internal class LogContext
    {
        // The value is copied for updates/export; the published wrapper and its tag array never change.
        private sealed class PublishedLog
        {
            internal readonly long Index;
            internal readonly LogData Value;
            internal PublishedLog(long index, LogData value) { Index = index; Value = value; }
        }
        private sealed class ScopeInfo
        {
            internal readonly long Id;
            internal readonly string Method;
            internal readonly ScopeInfo Next;
            internal int Closed;
            internal ScopeInfo(long id, string method, ScopeInfo next) { Id = id; Method = method; Next = next; }
        }
        private readonly Blackbox _blackbox;
        private readonly PublishedLog[] _logBuffer;
        private long _currentLogIndex = -1;
        private readonly int _threadId;
        private readonly string _threadName;
        private readonly ThreadToken _token;
        private ScopeInfo _openScopes;
        internal SessionState State => _blackbox.State;
        internal readonly HandleTracker<ScopeHandle> ScopeHandles;
        internal readonly HandleTracker<ExertHandle> ExertHandles;
        internal bool IsCurrentThread => ReferenceEquals(_token, ThreadToken.Current);

        public LogContext(Blackbox blackbox, int bufferSize)
        {
            if (bufferSize < 1) throw new ArgumentOutOfRangeException(nameof(bufferSize));
            _blackbox = blackbox;
            _logBuffer = new PublishedLog[bufferSize];
            _threadId = Environment.CurrentManagedThreadId;
            _threadName = Thread.CurrentThread.Name;
            _token = ThreadToken.Current;
            ScopeHandles = State.GetScopeTracker(_token);
            ExertHandles = State.GetExertTracker(_token);
        }

        public TagHandle EnqueueLog(string message, long sequence, string methodName, long scopeId = -1, long interactionId = -1, Blackbox exertingTo = null, Blackbox exertedBy = null)
        {
            var index = Enqueue(message, sequence, methodName, ScopeType.None, scopeId, interactionId, exertingTo, exertedBy);
            return index < 0 ? TagHandle.FromMessage(message) : new TagHandle(this, index, message);
        }
        public void EnqueueTagLog(string message, long sequence, string methodName, long interactionId, Blackbox taggedBy = null) =>
            EnqueueTagLog(message, sequence, methodName, interactionId, taggedBy, new[] { new LogData.Tag(interactionId, taggedBy) }, TargetTypes.BasedOnSettings);
        public void EnqueueTagLog(string message, long sequence, string methodName, long interactionId, Blackbox taggedBy, LogData.Tag[] tags, TargetTypes targetTypes) =>
            Enqueue(message, sequence, methodName, ScopeType.None, -1, interactionId, null, null, tags, taggedBy, State.Settings.Resolve(targetTypes));

        public TagHandle OpenScope(string message, long sequence, string methodName, long scopeId)
        {
            if (!State.CheckWrite()) return TagHandle.FromMessage(message);
            while (true)
            {
                var head = Volatile.Read(ref _openScopes);
                for (var n = head; n != null; n = n.Next)
                    if (n.Id == scopeId && Volatile.Read(ref n.Closed) == 0)
                    {
                        State.WarnOnce(SessionWarning.DuplicateScope, $"[LogContext] Scope '{scopeId}' is already open from '{n.Method}'.");
                        return TagHandle.FromMessage(message);
                    }
                if (ReferenceEquals(Interlocked.CompareExchange(ref _openScopes, new ScopeInfo(scopeId, methodName, head), head), head)) break;
            }
            var index = Enqueue(message, sequence, methodName, ScopeType.Open, scopeId);
            return index < 0 ? TagHandle.FromMessage(message) : new TagHandle(this, index, message);
        }

        public void CloseScope(long scopeId, long sequence = -1)
        {
            if (!State.CheckWrite()) return;
            var head = Volatile.Read(ref _openScopes);
            var found = head;
            while (found != null && found.Id != scopeId) found = found.Next;
            if (found == null)
            {
                State.WarnOnce(SessionWarning.MissingScope, $"[LogContext] Scope '{scopeId}' cannot be closed because it is not open.");
                return;
            }
            if (Volatile.Read(ref found.Closed) != 0) { PruneScopes(); return; }
            if (!IsCurrentThread) WarnThreadMismatch();
            else if (!ReferenceEquals(head, found))
            {
                bool autoClosed = false;
                for (var n = head; !ReferenceEquals(n, found); n = n.Next)
                    if (Interlocked.Exchange(ref n.Closed, 1) == 0)
                    {
                        autoClosed = true;
                        Enqueue("Closed automatically because an outer scope closed first.", sequence, n.Method, ScopeType.Close, n.Id);
                        sequence = -1;
                    }
                if (autoClosed) State.WarnOnce(SessionWarning.OuterScopeClose, "[LogContext] Newer scopes found. They will be closed automatically.");
            }
            if (Interlocked.Exchange(ref found.Closed, 1) == 0)
                Enqueue(string.Empty, sequence, found.Method, ScopeType.Close, found.Id);
            PruneScopes();
        }
        private void PruneScopes()
        {
            while (true)
            {
                var head = Volatile.Read(ref _openScopes);
                if (head == null || Volatile.Read(ref head.Closed) == 0) return;
                Interlocked.CompareExchange(ref _openScopes, head.Next, head);
            }
        }
        internal void WarnThreadMismatch()
        {
            if (!IsCurrentThread) State.WarnOnce(SessionWarning.CrossThreadClose,
                "[Blackbox] Handle disposed on a different thread; newer scopes remain open.");
        }

        private long Enqueue(string message, long sequence, string methodName, ScopeType scopeType, long scopeId,
            long interactionId = -1, Blackbox exertingTo = null, Blackbox exertedBy = null,
            LogData.Tag[] tags = null, Blackbox taggedBy = null, TargetTypes targetTypes = TargetTypes.Full)
        {
            if (!State.CheckWrite()) return -1;
            long index = Interlocked.Increment(ref _currentLogIndex);
            if (sequence < 0) sequence = State.NextSequence();
            var value = new LogData(_blackbox, message, DateTime.UtcNow, sequence, methodName, scopeType, scopeId,
                _threadId, _threadName, interactionId, exertingTo, exertedBy,
                tags == null ? null : (LogData.Tag[])tags.Clone(), taggedBy, targetTypes);
            var entry = new PublishedLog(index, value);
#if BLACKBOX_TESTS
            State.BeforeLogPublication?.Invoke(index);
#endif
            ref var slot = ref _logBuffer[ToBufferIndex(index)];
            while (true)
            {
                var old = Volatile.Read(ref slot);
                if (old != null && old.Index >= index) return -1;
                if (ReferenceEquals(Interlocked.CompareExchange(ref slot, entry, old), old)) return index;
            }
        }

        public IEnumerable<LogData> GetLogs(long maxSequence = -1)
        {
            GetRange(out var first, out var last);
            for (long i = first; i <= last; i++)
            {
                var entry = Read(i);
                if (entry == null || !entry.Value.IsValid) continue;
                var log = entry.Value;
                if (maxSequence >= 0 && log.Sequence > maxSequence) continue;
                if (log.Tags != null) log.Tags = (LogData.Tag[])log.Tags.Clone();
                yield return log;
            }
        }
        public string RenderMessage(long index, string fallback)
        {
            var entry = Read(index);
            return entry != null && entry.Value.IsValid ? LogFormatter.RenderMessage(entry.Value) : fallback ?? string.Empty;
        }
        public void ResolveWith(long index, IEnumerable<Blackbox> targets, TargetTypes targetTypes)
        {
            if (!State.CheckWrite() || Read(index) == null) return;
            var resolved = targets?.ToArray() ?? Array.Empty<Blackbox>();
            if (resolved.Length == 0) return;
            var types = State.Settings.Resolve(targetTypes);
            var tags = new LogData.Tag[resolved.Length];
            for (int i = 0; i < tags.Length; i++)
            {
                var target = resolved[i];
                if (target != null && !ReferenceEquals(target.State, State)) return;
                tags[i] = new LogData.Tag(target != null && target != _blackbox ? State.NextInteractionId() : -1, target);
            }
            LogData updated;
            while (true)
            {
                var old = Read(index);
                if (old == null || !old.Value.IsValid) return;
                updated = old.Value;
                updated.Tags = tags;
                if (Replace(old, updated)) break;
            }
            foreach (var tag in tags)
                if (tag.Target != null && tag.Target != _blackbox)
                    tag.Target.Tag(updated.Message, updated.MethodName, _blackbox, tag.Id, tags, types);
        }

        public bool TryMergeScope(long interactionId)
        {
            if (interactionId < 0 || !State.CheckWrite()) return false;
            GetRange(out var first, out var last);
            for (long index = last - 1; index >= first; index--)
            {
                while (true)
                {
                    var interaction = Read(index);
                    var scope = Read(index + 1);
                    if (interaction == null || scope == null) break;
                    var from = interaction.Value;
                    var into = scope.Value;
                    if (!from.IsValid || !into.IsValid || from.ScopeType != ScopeType.None ||
                        into.ScopeType != ScopeType.Open || from.InteractionId != interactionId || into.ScopeId < 0) break;
                    if (!string.IsNullOrEmpty(from.Message))
                        into.Message = string.IsNullOrEmpty(into.Message) ? from.Message :
                            into.Message == from.Message ? into.Message : $"{into.Message} (from: {from.Message})";
                    into.InteractionId = from.InteractionId;
                    into.ExertedBy = from.ExertedBy ?? into.ExertedBy;
                    into.ExertingTo = from.ExertingTo ?? into.ExertingTo;
                    if (!Replace(scope, into)) continue;
#if BLACKBOX_TESTS
                    State.AfterScopeMerge?.Invoke();
#endif
                    // Publish the replacement before invalidating the interaction.
                    while ((interaction = Read(index)) != null && interaction.Value.IsValid)
                    {
                        from = interaction.Value;
                        from.IsValid = false;
                        if (Replace(interaction, from)) break;
                    }
                    return true;
                }
            }
            return false;
        }
        private void GetRange(out long first, out long last)
        {
            last = Volatile.Read(ref _currentLogIndex);
            first = Math.Max(0, last - _logBuffer.Length + 1);
        }
        private PublishedLog Read(long index)
        {
            if (index < 0) return null;
            GetRange(out var first, out var last);
            if (index < first || index > last) return null;
            var entry = Volatile.Read(ref _logBuffer[ToBufferIndex(index)]);
            // A reservation may still contain the preceding lap's entry.
            return entry != null && entry.Index == index ? entry : null;
        }
        private bool Replace(PublishedLog old, LogData value) =>
            ReferenceEquals(Interlocked.CompareExchange(ref _logBuffer[ToBufferIndex(old.Index)], new PublishedLog(old.Index, value), old), old);
        private int ToBufferIndex(long index) => (int)(index % _logBuffer.Length);
    }
}
