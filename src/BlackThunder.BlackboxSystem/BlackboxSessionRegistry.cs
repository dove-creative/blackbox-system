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
        private static readonly AsyncLocal<BlackboxSession> Ambient = new();
        private static readonly object ActiveSessionsLock = new();
        private static readonly List<BlackboxSession> ActiveSessions = new();
        private static BlackboxSession _default = new(new SessionSettings(), null, DefaultBuckets);

        internal static BlackboxSession Current => Ambient.Value ?? Volatile.Read(ref _default);
        internal static BlackboxSession Default => Volatile.Read(ref _default);
        internal static Guid CurrentKey => Current.SessionKey;
        internal static Guid DefaultKey => Default.SessionKey;
        internal static int ActiveSessionCount
        {
            get { lock (ActiveSessionsLock) return ActiveSessions.Count; }
        }

        internal static Guid[] GetActiveSessionKeys()
        {
            lock (ActiveSessionsLock)
            {
                var keys = new Guid[ActiveSessions.Count];
                for (int i = 0; i < keys.Length; i++) keys[i] = ActiveSessions[i].SessionKey;
                return keys;
            }
        }

        internal static void DisposeActiveSessions()
        {
            BlackboxSession[] sessions;
            lock (ActiveSessionsLock) sessions = ActiveSessions.ToArray();
            // Unwind nested bindings from inner to outer, without holding the list lock during disposal.
            for (int i = sessions.Length - 1; i >= 0; i--) sessions[i].Dispose();
        }

        // Synchronous so the binding stays visible to the caller's logical scope.
        internal static BlackboxSession Begin(int buckets = ExplicitBuckets)
        {
            var session = new BlackboxSession(Current.State.Settings, Ambient.Value, buckets);
            lock (ActiveSessionsLock) ActiveSessions.Add(session);
            Ambient.Value = session;
            return session;
        }
        internal static void Remove(BlackboxSession session)
        {
            lock (ActiveSessionsLock) ActiveSessions.Remove(session);
        }
        internal static void Restore(BlackboxSession session, BlackboxSession previous)
        {
            if (ReferenceEquals(Ambient.Value, session)) Ambient.Value = previous;
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
                // Neither the default nor unpublished candidates are bound to Ambient.
                old.Dispose();
                return;
            }
        }

        internal static Blackbox GetBlackbox(object subject) => Current.Subjects.GetOrAdd(subject);
        internal static bool Contains(object subject) => Current.Subjects.Contains(subject);
        internal static int Count() => Current.Subjects.Count();
#if BLACKBOX_TESTS
        internal static Action<BlackboxSession> BeforeDefaultSessionExchange;
#endif
    }
}
