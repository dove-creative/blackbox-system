using System;
using System.Threading;

namespace com.BlackThunder.BlackboxSystem
{
#if !BLACKBOX_LOCK_HANDLES
    /// <summary>
    /// GC-free, lock-free handle manager based on packed slot and generation ids.
    /// </summary>
    /// <remarks>
    /// Raw handle layout is applied to id - 1, so id 0 stays invalid and
    /// first-generation handles remain sequential: 1, 2, 3, ...
    ///
    /// id - 1 layout:
    /// [ generation - 1: 63 - SlotBits bits ][ slot: SlotBits bits ]
    ///
    /// The slot can be reused after disposal, but the generation changes,
    /// so stale handles are rejected.
    /// </remarks>
    internal static class HandleManager<T>
    {
        // 20 bits = 1,048,576 simultaneously allocated live/reusable slots per T.
        // Adjust this value based on the expected maximum number of concurrent handles.
        // Memory with SlotBits = 20:
        // - _slotStates: 8 MB
        // - _freeNext  : 4 MB
        // Total: about 12 MB per closed generic T that touches HandleManager<T>.
        private const int SlotBits = 20;
        private const int MaxSlots = 1 << SlotBits;
        private const long SlotMask = MaxSlots - 1L;

        // Use 63 bits total so generated handles stay positive.
        private const int GenerationBits = 63 - SlotBits;
        private const long MaxGeneration = (1L << GenerationBits) - 1L;

        // Lock-free free-list head layout.
        // Low bits store slot + 1. Zero means an empty stack.
        // Upper bits store a version tag to avoid ABA on the Treiber stack head.
        private const int FreeHeadIndexBits = SlotBits + 1;
        private const int FreeHeadVersionBits = 64 - FreeHeadIndexBits;
        private const ulong FreeHeadIndexMask = (1UL << FreeHeadIndexBits) - 1UL;
        private const ulong FreeHeadVersionMask = (1UL << FreeHeadVersionBits) - 1UL;

        // Slot state encoding:
        //   0              = never allocated
        //   +generation    = alive
        //   -generation    = disposed / free / reusable
        private static readonly long[] _slotStates = new long[MaxSlots];

        // Singly-linked stack storage for reusable slots.
        // Value is next slot + 1. Zero means end-of-stack.
        private static readonly int[] _freeNext = new int[MaxSlots];

        // Packed lock-free stack head for _freeNext.
        private static long _freeHead;

        // Number of slots that have ever been reserved since last Reset().
        private static int _slotCount;

        public static long GetHandleId()
        {
            while (true)
            {
                if (TryPopFreeSlot(out int slot))
                {
                    long state = AtomicRead(ref _slotStates[slot]);

                    // A slot reachable from the free-list must be disposed.
                    // If this ever fires, the free-list/state invariant was broken.
                    if (state >= 0L)
                    {
                        throw new InvalidOperationException(
                            "Handle free-list corruption detected: popped a non-free slot.");
                    }

                    long previousGeneration = -state;
                    long nextGeneration = previousGeneration + 1L;

                    if (nextGeneration > MaxGeneration)
                    {
                        // Practically unreachable with the default layout.
                        // Retire this slot permanently instead of wrapping generation,
                        // because wrapping would allow a stale handle to become valid again.
                        continue;
                    }

                    // No other allocator owns this slot after a successful pop, and no
                    // disposer can target the new generation before this method returns.
                    // Interlocked.Exchange keeps the 64-bit write atomic on all runtimes.
                    Interlocked.Exchange(ref _slotStates[slot], nextGeneration);
                    return Pack(slot, nextGeneration);
                }

                if (TryReserveFreshSlot(out int freshSlot))
                {
                    Interlocked.Exchange(ref _slotStates[freshSlot], 1L);
                    return Pack(freshSlot, 1L);
                }

                throw new InvalidOperationException(
                    $"Handle capacity exceeded. MaxSlots={MaxSlots}. " +
                    $"Increase {nameof(SlotBits)} if this is expected.");
            }
        }

        public static bool TryDisposeHandleId(long id)
        {
            if (!TryUnpack(id, out int slot, out long generation))
                return false;

            if (slot >= Volatile.Read(ref _slotCount))
                return false;

            // Only the currently alive handle for this exact generation may dispose.
            // Already-disposed handles, stale handles, and random ids fail here.
            if (Interlocked.CompareExchange(
                    ref _slotStates[slot],
                    -generation,
                    generation) != generation)
            {
                return false;
            }

            PushFreeSlot(slot);
            return true;
        }

        public static bool IsAlive(long id)
        {
            if (!TryUnpack(id, out int slot, out long generation))
                return false;

            if (slot >= Volatile.Read(ref _slotCount))
                return false;

            // Lock-free read is safe because the entire alive/dead/generation state is
            // represented by one 64-bit value:
            //   +same generation => alive
            //   -same generation => disposed
            //   different generation => stale/reused slot
            return AtomicRead(ref _slotStates[slot]) == generation;
        }

        public static void WarnIfThreadMismatch(int createdThreadId, int currentThreadId)
        {
            if (createdThreadId != currentThreadId)
                Infrastructure.Log(
                    "[Blackbox] Handle was disposed on a different thread than it was created. " +
                    "Log scope or merge data may be unstable.",
                    LogLevel.Warning);
        }

        /// <summary>
        /// Clears live handle ids and restarts the handle slot/generation state for debug or test reruns.
        /// </summary>
        /// <remarks>
        /// Call this only from a safe point where all previously issued handles are no longer used.
        /// This method intentionally has no lock; concurrent Reset() with Get/Dispose/IsAlive is unsupported.
        /// Disposing or checking a stale handle after Reset() is unsupported because a new handle may
        /// reuse the same slot and generation.
        /// </remarks>
        public static void Reset()
        {
            Array.Clear(_slotStates, 0, _slotStates.Length);
            Array.Clear(_freeNext, 0, _freeNext.Length);
            Interlocked.Exchange(ref _freeHead, 0L);
            Interlocked.Exchange(ref _slotCount, 0);
        }

        private static bool TryReserveFreshSlot(out int slot)
        {
            while (true)
            {
                int count = Volatile.Read(ref _slotCount);
                if (count >= MaxSlots)
                {
                    slot = -1;
                    return false;
                }

                if (Interlocked.CompareExchange(ref _slotCount, count + 1, count) == count)
                {
                    slot = count;
                    return true;
                }
            }
        }

        private static bool TryPopFreeSlot(out int slot)
        {
            while (true)
            {
                long oldHead = AtomicRead(ref _freeHead);
                int indexPlusOne = GetFreeHeadIndexPlusOne(oldHead);

                if (indexPlusOne == 0)
                {
                    slot = -1;
                    return false;
                }

                int candidateSlot = indexPlusOne - 1;
                int nextIndexPlusOne = Volatile.Read(ref _freeNext[candidateSlot]);

                ulong nextVersion = NextFreeHeadVersion(GetFreeHeadVersion(oldHead));
                long newHead = PackFreeHead(nextVersion, nextIndexPlusOne);

                if (Interlocked.CompareExchange(ref _freeHead, newHead, oldHead) == oldHead)
                {
                    // Not required for correctness, but helps avoid stale diagnostic state.
                    Volatile.Write(ref _freeNext[candidateSlot], 0);
                    slot = candidateSlot;
                    return true;
                }
            }
        }

        private static void PushFreeSlot(int slot)
        {
            int indexPlusOne = slot + 1;

            while (true)
            {
                long oldHead = AtomicRead(ref _freeHead);
                int oldIndexPlusOne = GetFreeHeadIndexPlusOne(oldHead);

                // Link this slot to the current stack head before publishing it as the new head.
                Volatile.Write(ref _freeNext[slot], oldIndexPlusOne);

                ulong nextVersion = NextFreeHeadVersion(GetFreeHeadVersion(oldHead));
                long newHead = PackFreeHead(nextVersion, indexPlusOne);

                if (Interlocked.CompareExchange(ref _freeHead, newHead, oldHead) == oldHead)
                    return;
            }
        }

        private static long Pack(int slot, long generation)
        {
            // Use a 1-based public id so 0 remains an invalid/null handle.
            // This also preserves the old first-generation shape: 1, 2, 3, ...
            long raw = ((generation - 1L) << SlotBits) | (long)(uint)slot;
            return raw + 1L;
        }

        private static bool TryUnpack(long id, out int slot, out long generation)
        {
            if (id <= 0L)
            {
                slot = 0;
                generation = 0L;
                return false;
            }

            long raw = id - 1L;
            slot = (int)(raw & SlotMask);
            generation = (raw >> SlotBits) + 1L;

            return generation > 0L && generation <= MaxGeneration;
        }

        private static long AtomicRead(ref long value)
        {
            // Atomic 64-bit read that works even on runtimes where plain long reads
            // may not be atomic. CompareExchange with equal comparand/value is a no-op.
            return Interlocked.CompareExchange(ref value, 0L, 0L);
        }

        private static int GetFreeHeadIndexPlusOne(long head)
        {
            return (int)(unchecked((ulong)head) & FreeHeadIndexMask);
        }

        private static ulong GetFreeHeadVersion(long head)
        {
            return unchecked((ulong)head) >> FreeHeadIndexBits;
        }

        private static ulong NextFreeHeadVersion(ulong version)
        {
            return (version + 1UL) & FreeHeadVersionMask;
        }

        private static long PackFreeHead(ulong version, int indexPlusOne)
        {
            ulong packed = ((version & FreeHeadVersionMask) << FreeHeadIndexBits) |
                           ((ulong)indexPlusOne & FreeHeadIndexMask);
            return unchecked((long)packed);
        }
    }
#endif
}
