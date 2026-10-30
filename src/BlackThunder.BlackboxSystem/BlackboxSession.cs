using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace BlackThunder.BlackboxSystem
{
    /// <summary>
    /// A test/debug recording lifetime inherited through ExecutionContext.
    /// Start one with <see cref="BlackboxHandle.BeginSession"/>.
    /// </summary>
    /// <remarks>Explicit sessions are strongly retained until disposed, even if callers drop all references.</remarks>
    public sealed class BlackboxSession : IDisposable
    {
        // Session ownership
        private readonly BlackboxSession _previous;
        // Session-owned stripes bound tracking overhead independently of context churn.
        // Segments are reused and grow only when outstanding handles need more cells.
        private const int TrackerStripeCount = 16;
        private readonly HandleTracker<ScopeHandle>[] _scopeTrackers = CreateTrackers<ScopeHandle>();
        private readonly HandleTracker<ExertHandle>[] _exertTrackers = CreateTrackers<ExertHandle>();
        private long _exportOrdinal;
        // The only session part recording code sees.
        internal readonly SessionState State;
        internal readonly SubjectStore Subjects;

        public Guid SessionKey { get; } = Guid.NewGuid();
        internal bool IsDisposed => !State.IsActive;


        // Construction
        internal BlackboxSession(SessionSettings settings, BlackboxSession previous, int buckets)
        {
            _previous = previous;
            State = new SessionState(settings);
            Subjects = new SubjectStore(State, buckets);
        }
        private static HandleTracker<T>[] CreateTrackers<T>()
        {
            var trackers = new HandleTracker<T>[TrackerStripeCount];
            for (int i = 0; i < trackers.Length; i++)
                trackers[i] = new HandleTracker<T>();
            return trackers;
        }

        // Front-side handle creation: the Blackbox records, this session registers the handle in its trackers.
        internal ScopeHandle OpenScope(Blackbox box, string message, string methodName)
        {
            if (!box.WriteScope(message, methodName, out var context, out var scopeId, out var tag)) return default;
            var lease = GetScopeTracker(ThreadToken.Current).Register(State);
            return new ScopeHandle(context, scopeId, tag.Bind(this), lease);
        }

        internal ExertHandle Exert(Blackbox source, Blackbox target, string message, string methodName)
        {
            var interactionId = source.Exert(target, message, methodName, out var targetContext);
            if (targetContext == null) return default;
            var lease = GetExertTracker(ThreadToken.Current).Register(State);
            return new ExertHandle(targetContext, interactionId, lease);
        }

        internal HandleTracker<ScopeHandle> GetScopeTracker(ThreadToken token) =>
            _scopeTrackers[RuntimeHelpers.GetHashCode(token) & (TrackerStripeCount - 1)];
        internal HandleTracker<ExertHandle> GetExertTracker(ThreadToken token) =>
            _exertTrackers[RuntimeHelpers.GetHashCode(token) & (TrackerStripeCount - 1)];

        // Export identity
        internal long NextExportOrdinal() => Interlocked.Increment(ref _exportOrdinal);

        // Retirement
        /// <summary>Ends this recording lifetime, reclaims outstanding handle IDs and removes the active session.</summary>
        /// <remarks>
        /// Only the first call ends the session and removes it from the active list.
        /// Every call restores the previous binding in the calling flow if that flow is bound to this session,
        /// so a flow can unbind itself even after another flow disposed the session first.
        /// Other execution flows keep their bindings to this disposed session until they dispose it themselves.
        /// </remarks>
        public void Dispose()
        {
            if (State.TryDeactivate())
            {
                Retire();
                BlackboxSessionRegistry.Remove(this);
            }
            BlackboxSessionRegistry.Restore(this, _previous);
        }

        private void Retire()
        {
            foreach (var tracker in _scopeTrackers) tracker.Close();
            foreach (var tracker in _exertTrackers) tracker.Close();
        }
    }
}
