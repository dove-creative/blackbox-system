using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace com.BlackThunder.BlackboxSystem
{
    internal static class BlackboxRegistry
    {
        private static ConditionalWeakTable<object, Blackbox> _subjects = new();
        private static object _lock = new();

        public static Blackbox GetBlackbox(object subject)
        {
            if (subject == null)
                throw new ArgumentNullException(nameof(subject), "[BlackboxRegistry] Subject cannot be null");

            return _subjects.GetValue(subject, key => new Blackbox(key, Infrastructure.StrongReference));
        }

        internal static bool Contains(object subject)
        {
            if (subject == null)
                throw new ArgumentNullException(nameof(subject), "[BlackboxRegistry] Subject cannot be null");

            return _subjects.TryGetValue(subject, out _);
        }

        /// <summary>
        /// Debug-only helper for counting registered blackboxes.
        /// </summary>
        /// <remarks>
        /// This method does not synchronize with GetBlackbox or Contains, so the
        /// result is only reliable when no other thread is using the registry.
        /// </remarks>
        internal static int Count()
        {
            lock (_lock)
            {
                return _subjects.Count();
            }
        }

        /// <summary>
        /// Debug-only reset helper for clearing registry and runtime state.
        /// </summary>
        /// <remarks>
        /// Call this only from a safe point where no other thread can read from or
        /// write to the registry.
        /// </remarks>
        public static void ForceReset()
        {
            lock (_lock)
            {
                _subjects = new();

                Infrastructure.ForceResetRuntimeState();
                BlackboxRuntime.Reset();

                HandleManager<ScopeHandle>.Reset();
                HandleManager<ExertHandle>.Reset();
            }
        }
    }
}
