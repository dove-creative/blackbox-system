using System;
using System.Threading;

namespace BlackThunder.BlackboxSystem
{
    // Core-facing data for one session. The owning BlackboxSession writes lifecycle state;
    // recording code reads it and draws numbers from its counters.
    // It references no session, subject store, handle tracker or registry, and invokes no callbacks.
    internal sealed class SessionState
    {
        private SessionSettings _settings;
        private CrashInfo _crash;
        private int _active = 1, _warnings;
        private long _boxId = -1, _interactionId = -1, _sequence = -1;
#if BLACKBOX_TESTS
        internal Action BeforeHandleRegistration;
        internal Action<long> BeforeLogPublication;
        internal Action AfterScopeMerge;
#endif

        // Published state views
        internal SessionSettings Settings => Volatile.Read(ref _settings);
        internal CrashInfo Crash => Volatile.Read(ref _crash);
        internal bool IsActive => Volatile.Read(ref _active) != 0;
        internal bool CanWrite => IsActive && Crash == null && Settings.UseBlackbox == UseBlackboxOption.Use;
        internal long CurrentSequence => Volatile.Read(ref _sequence);


        internal SessionState(SessionSettings settings) => _settings = settings;

        // Sequence allocation
        internal long NextBoxId() => Interlocked.Increment(ref _boxId);
        internal long NextInteractionId() => Interlocked.Increment(ref _interactionId);
        internal long NextSequence() => Interlocked.Increment(ref _sequence);

        // Configuration and warnings
        internal bool TryUpdateSettings(Action<SessionSettings> update)
        {
            if (!IsActive) return false;
            while (true)
            {
                var old = Settings;
                var next = old.Copy();
                update(next);
                if (ReferenceEquals(Interlocked.CompareExchange(ref _settings, next, old), old)) return true;
            }
        }

        // True only for the first caller of each warning cause.
        internal bool TryMarkWarned(SessionWarning reason)
        {
            int bit = (int)reason;
            while (true)
            {
                var old = Volatile.Read(ref _warnings);
                if ((old & bit) != 0) return false;
                if (Interlocked.CompareExchange(ref _warnings, old | bit, old) == old) return true;
            }
        }

        // Lifecycle transitions
        internal bool TryFreeze(CrashInfo crash) => IsActive && Interlocked.CompareExchange(ref _crash, crash, null) == null;
        // True only for the call that ended the session.
        internal bool TryDeactivate() => Interlocked.Exchange(ref _active, 0) != 0;
    }

    // Recording-side policy over SessionState: reject writes after disposal, crash or disablement,
    // and log each warning cause once.
    internal static class SessionStateGuard
    {
        internal static bool CheckWrite(this SessionState state)
        {
            if (!state.IsActive)
            {
                state.WarnOnce(SessionWarning.Disposed, "[Blackbox] Session has been disposed; recording rejected.");
                return false;
            }
            return state.Crash == null && state.Settings.UseBlackbox == UseBlackboxOption.Use;
        }

        internal static void WarnOnce(this SessionState state, SessionWarning reason, string message)
        {
            if (state.TryMarkWarned(reason)) state.Settings.WarningLogger?.Invoke(message);
        }
    }

    internal enum SessionWarning
    {
        Disposed = 1,
        CrossThreadClose = 4,
        LabelAlreadySet = 8,
        DuplicateScope = 16,
        MissingScope = 32,
        OuterScopeClose = 64,
    }

    internal sealed class CrashInfo
    {
        internal readonly string Message;
        internal readonly string Stack;

        internal CrashInfo(string message, string stack)
        {
            Message = message;
            Stack = stack;
        }
    }
}
