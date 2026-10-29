using System;
using System.Threading;

namespace BlackThunder.BlackboxSystem
{
    /// <summary>A test/debug recording lifetime inherited through ExecutionContext.</summary>
    public sealed class BlackboxSession : IDisposable
    {
        private sealed class Binding
        {
            internal readonly BlackboxSession Session;
            internal readonly SessionState State;
            internal Binding(BlackboxSession session, SessionState state) { Session = session; State = state; }
        }
        private static readonly AsyncLocal<Binding> Ambient = new();
        private static readonly BlackboxSession Default = new(new SessionSettings(), null, 4096);
        private readonly Binding _previous;
        private readonly int _bucketCount;
        private SessionState _current;
        private long _exportOrdinal;

        public Guid SessionKey { get; } = Guid.NewGuid();
        internal SessionState Current => Volatile.Read(ref _current);
        internal static SessionState CurrentState => Ambient.Value?.State ?? Default.Current;
        internal long NextExportOrdinal() => Interlocked.Increment(ref _exportOrdinal);
        private BlackboxSession(SessionSettings settings, Binding previous, int buckets)
        {
            _previous = previous;
            _bucketCount = buckets;
            _current = new SessionState(this, settings, buckets);
        }
        /// <summary>Begin synchronously in the same logical scope that will dispose the session.</summary>
        public static BlackboxSession Begin()
        {
            var session = new BlackboxSession(CurrentState.Settings, Ambient.Value, 256);
            Ambient.Value = new Binding(session, session.Current);
            return session;
        }
#if BLACKBOX_TESTS
        internal static BlackboxSession BeginForTests(int buckets)
        {
            var session = new BlackboxSession(CurrentState.Settings, Ambient.Value, buckets);
            Ambient.Value = new Binding(session, session.Current);
            return session;
        }
#endif
        public void Dispose()
        {
            Interlocked.Exchange(ref _current, null)?.Retire();
            if (ReferenceEquals(Ambient.Value?.Session, this)) Ambient.Value = _previous;
        }
        internal static void ResetCurrent()
        {
            var binding = Ambient.Value;
            var session = binding?.Session ?? Default;
            while (true)
            {
                var old = session.Current;
                if (old == null || (binding != null && !ReferenceEquals(binding.State, old)))
                {
                    binding?.State.CheckWrite();
                    return;
                }
                var next = new SessionState(session, old.Settings, session._bucketCount);
                if (!ReferenceEquals(Interlocked.CompareExchange(ref session._current, next, old), old)) continue;
                old.Retire();
                // Default callers remain unbound, including NUnit SetUp callers.
                if (binding != null) Ambient.Value = new Binding(session, next);
                return;
            }
        }
    }
    internal enum SessionWarning
    {
        Disposed = 1, Reset = 2, CrossThreadClose = 4, LabelAlreadySet = 8,
        DuplicateScope = 16, MissingScope = 32, OuterScopeClose = 64,
    }
    internal sealed class CrashInfo
    {
        internal readonly string Message, Stack;
        internal CrashInfo(string message, string stack) { Message = message; Stack = stack; }
    }
    internal sealed class SessionState
    {
        private sealed class TrackerNode
        {
            internal readonly IHandleTracker Tracker;
            internal readonly TrackerNode Next;
            internal TrackerNode(IHandleTracker tracker, TrackerNode next) { Tracker = tracker; Next = next; }
        }
        private static readonly TrackerNode Closed = new(null, null);
        private TrackerNode _trackers;
        private SessionSettings _settings;
        private CrashInfo _crash;
        private int _warnings;
        private long _boxId = -1, _interactionId = -1, _sequence = -1;
        internal readonly BlackboxSession Session;
        internal readonly SubjectStore Subjects;
        internal SessionSettings Settings => Volatile.Read(ref _settings);
        internal CrashInfo Crash => Volatile.Read(ref _crash);
        internal bool IsCurrent => ReferenceEquals(Session.Current, this);
        internal bool CanWrite => IsCurrent && Crash == null && Settings.UseBlackbox == UseBlackboxOption.Use;
        internal long CurrentSequence => Volatile.Read(ref _sequence);
        internal long NextBoxId() => Interlocked.Increment(ref _boxId);
        internal long NextInteractionId() => Interlocked.Increment(ref _interactionId);
        internal long NextSequence() => Interlocked.Increment(ref _sequence);
        internal SessionState(BlackboxSession session, SessionSettings settings, int buckets)
        {
            Session = session;
            _settings = settings;
            Subjects = new SubjectStore(this, buckets);
        }
        internal bool CheckWrite()
        {
            if (!IsCurrent)
            {
                var disposed = Session.Current == null;
                WarnOnce(disposed ? SessionWarning.Disposed : SessionWarning.Reset,
                    disposed ? "[Blackbox] Session has been disposed; recording rejected." : "[Blackbox] Handle or execution context belongs to a reset generation; recording rejected.");
                return false;
            }
            return Crash == null && Settings.UseBlackbox == UseBlackboxOption.Use;
        }
        internal bool TryFreeze(CrashInfo crash) => IsCurrent && Interlocked.CompareExchange(ref _crash, crash, null) == null;
        internal void UpdateSettings(Action<SessionSettings> update)
        {
            if (!IsCurrent) { CheckWrite(); return; }
            while (true)
            {
                var old = Settings;
                var next = old.Copy();
                update(next);
                if (ReferenceEquals(Interlocked.CompareExchange(ref _settings, next, old), old)) return;
            }
        }
        internal void WarnOnce(SessionWarning reason, string message)
        {
            int bit = (int)reason;
            while (true)
            {
                var old = Volatile.Read(ref _warnings);
                if ((old & bit) != 0) return;
                if (Interlocked.CompareExchange(ref _warnings, old | bit, old) == old) break;
            }
            Settings.WarningLogger?.Invoke(message);
        }
        internal void RegisterTracker(IHandleTracker tracker)
        {
            while (true)
            {
                var head = Volatile.Read(ref _trackers);
                if (ReferenceEquals(head, Closed)) { tracker.Close(); return; }
                if (ReferenceEquals(Interlocked.CompareExchange(ref _trackers, new TrackerNode(tracker, head), head), head)) return;
            }
        }
        internal void Retire()
        {
            var node = Interlocked.Exchange(ref _trackers, Closed);
            while (node != null && !ReferenceEquals(node, Closed))
            {
                node.Tracker.Close();
                node = node.Next;
            }
        }
#if BLACKBOX_TESTS
        internal Action BeforeHandleRegistration;
        internal Action<long> BeforeLogPublication;
        internal Action AfterScopeMerge;
#endif
    }
}
