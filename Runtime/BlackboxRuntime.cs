using System.Threading;

namespace com.BlackThunder.BlackboxSystem
{
    internal static class BlackboxRuntime
    {
        public static long CurrentSequence => Interlocked.Read(ref _globalSequence);

        private static long _globalId = -1;
        private static long _globalInteractionId = -1;
        private static long _globalSequence = -1;

        public static long GetNextBlackboxId() => Interlocked.Increment(ref _globalId);
        public static long GetNextInteractionId() => Interlocked.Increment(ref _globalInteractionId);
        public static long GetNextSequence() => Interlocked.Increment(ref _globalSequence);

        /// <summary>
        /// Resets runtime ids and sequence counters for debug or test reruns.
        /// </summary>
        /// <remarks>
        /// Call this only from a safe point, normally through BlackboxRegistry.ForceReset.
        /// Existing log data and handles from before the reset are no longer supported.
        /// </remarks>
        public static void Reset()
        {
            Interlocked.Exchange(ref _globalId, -1);
            Interlocked.Exchange(ref _globalInteractionId, -1);
            Interlocked.Exchange(ref _globalSequence, -1);
        }
    }
}
