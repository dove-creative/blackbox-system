using System;
using System.Collections.Generic;
using System.Threading;

namespace BlackThunder.BlackboxSystem
{
    // Resolves the session the current execution flow records into: its ambient binding, or the shared default.
    // Explicit sessions stay strongly rooted until disposed so abandoned sessions remain diagnosable.
    internal static class BlackboxSessionRegistry
    {
        private const int DefaultBuckets = 65536, ExplicitBuckets = 256;
        private static readonly AsyncLocal<BlackboxSession> _ambient = new();
        private static readonly object _activeSessionsLock = new();
        private static readonly List<BlackboxSession> _activeSessions = new();
        private static BlackboxSession _default = new(new SessionSettings(), null, DefaultBuckets);

        internal static BlackboxSession Current => _ambient.Value ?? Volatile.Read(ref _default);
        internal static BlackboxSession Default => Volatile.Read(ref _default);
        internal static Guid CurrentKey => Current.SessionKey;
        internal static Guid DefaultKey => Default.SessionKey;

        internal static int ActiveSessionCount
        {
            get
            {
                lock (_activeSessionsLock)
                    return _activeSessions.Count;
            }
        }
#if BLACKBOX_TESTS
        internal static Action<BlackboxSession> BeforeDefaultSessionExchange;
#endif


        // Session creation and binding
        // Synchronous so the binding stays visible to the caller's logical scope.
        internal static BlackboxSession Begin(int buckets = ExplicitBuckets)
        {
            var session = new BlackboxSession(Current.State.Settings, _ambient.Value, buckets);
            lock (_activeSessionsLock) _activeSessions.Add(session);
            _ambient.Value = session;
            return session;
        }

        internal static void Restore(BlackboxSession session, BlackboxSession previous)
        {
            if (ReferenceEquals(_ambient.Value, session)) _ambient.Value = previous;
        }

        // Lookup and diagnostics
        internal static Blackbox GetBlackbox(object subject) => Current.Subjects.GetOrAdd(subject);
        internal static bool Contains(object subject) => Current.Subjects.Contains(subject);
        internal static int Count() => Current.Subjects.Count();

        internal static Guid[] GetActiveSessionKeys()
        {
            lock (_activeSessionsLock)
            {
                var keys = new Guid[_activeSessions.Count];
                for (int i = 0; i < keys.Length; i++)
                    keys[i] = _activeSessions[i].SessionKey;
                return keys;
            }
        }

        // Disposal and replacement
        internal static void DisposeActiveSessions()
        {
            BlackboxSession[] sessions;
            lock (_activeSessionsLock) sessions = _activeSessions.ToArray();
            // Unwind nested bindings from inner to outer, without holding the list lock during disposal.
            for (int i = sessions.Length - 1; i >= 0; i--)
                sessions[i].Dispose();
        }

        internal static void Remove(BlackboxSession session)
        {
            lock (_activeSessionsLock) _activeSessions.Remove(session);
        }

        internal static void RestartDefaultSession()
        {
            while (true)
            {
                var old = Volatile.Read(ref _default);
                var next = new BlackboxSession(old.State.Settings, null, DefaultBuckets);
#if BLACKBOX_TESTS
                BeforeDefaultSessionExchange?.Invoke(next);
#endif
                if (!ReferenceEquals(Interlocked.CompareExchange(ref _default, next, old), old))
                {
                    next.Dispose();
                    continue;
                }
                // Neither the default nor unpublished candidates are bound to the ambient flow.
                old.Dispose();
                return;
            }
        }
    }
}
