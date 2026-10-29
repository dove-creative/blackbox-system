namespace BlackThunder.BlackboxSystem
{
    // Convenience access for internal callers without a bound Blackbox.
    internal static class BlackboxRuntime
    {
        public static long CurrentSequence => BlackboxSession.CurrentState.CurrentSequence;
        public static long GetNextBlackboxId() => BlackboxSession.CurrentState.NextBoxId();
        public static long GetNextInteractionId() => BlackboxSession.CurrentState.NextInteractionId();
        public static long GetNextSequence() => BlackboxSession.CurrentState.NextSequence();
        public static void Reset() => BlackboxSession.ResetCurrent();
    }
}
