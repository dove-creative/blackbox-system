using System;
using System.Threading;

namespace com.BlackThunder.BlackboxSystem
{
#if BLACKBOX_LOCK_HANDLES
    /// <summary>
    /// GC-free handle manager based on packed slot and generation ids.
    /// </summary>
    /// <remarks>
    /// This version uses a single lock around slot allocation and release.
    ///
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
        // - _freeSlots : 4 MB
        // Total: about 12 MB per closed generic T that touches HandleManager<T>.
        private const int SlotBits = 20;
        private const int MaxSlots = 1 << SlotBits;
        private const long SlotMask = MaxSlots - 1L;

        // Use 63 bits total so generated handles stay positive.
        private const int GenerationBits = 63 - SlotBits;
        private const long MaxGeneration = (1L << GenerationBits) - 1L;

        private static readonly object _gate = new object();

        // Slot state encoding:
        //   0              = never allocated
        //   +generation    = alive
        //   -generation    = disposed / free
        private static readonly long[] _slotStates = new long[MaxSlots];

        // Stack of reusable slots. This is preallocated to avoid per-operation GC.
        private static readonly int[] _freeSlots = new int[MaxSlots];

        // Number of slots that have ever been created since last Reset().
        private static int _slotCount;

        // Number of entries currently stored in _freeSlots.
        private static int _freeCount;

        public static long GetHandleId()
        {
            lock (_gate)
            {
                int slot;
                bool reusedSlot;

                if (_freeCount > 0)
                {
                    slot = _freeSlots[--_freeCount];
                    reusedSlot = true;
                }
                else
                {
                    if (_slotCount >= MaxSlots)
                    {
                        throw new InvalidOperationException(
                            $"Handle capacity exceeded. MaxSlots={MaxSlots}. " +
                            $"Increase {nameof(SlotBits)} if this is expected.");
                    }

                    slot = _slotCount++;
                    reusedSlot = false;
                }

                long previousState = _slotStates[slot];
                long previousGeneration = previousState >= 0L
                    ? previousState
                    : -previousState;

                long nextGeneration = previousGeneration + 1L;
                if (nextGeneration > MaxGeneration)
                {
                    // Practically unreachable with the default layout, but keep the
                    // slot bookkeeping valid if the limit is ever hit.
                    if (reusedSlot)
                    {
                        _freeSlots[_freeCount++] = slot;
                    }
                    else
                    {
                        _slotCount--;
                    }

                    throw new InvalidOperationException(
                        $"Handle generation exhausted for slot {slot}. " +
                        $"MaxGeneration={MaxGeneration}.");
                }

                // Mark alive by storing a positive generation.
                Volatile.Write(ref _slotStates[slot], nextGeneration);

                return Pack(slot, nextGeneration);
            }
        }

        public static bool TryDisposeHandleId(long id)
        {
            if (!TryUnpack(id, out int slot, out long generation))
                return false;

            lock (_gate)
            {
                if (slot >= _slotCount)
                    return false;

                long state = _slotStates[slot];

                // Only the currently alive handle for this exact generation may dispose.
                // Already-disposed handles, stale handles, and random ids fail here.
                if (state != generation)
                    return false;

                // Mark dead by storing a negative generation.
                Volatile.Write(ref _slotStates[slot], -generation);

                _freeSlots[_freeCount++] = slot;
                return true;
            }
        }

        public static bool IsAlive(long id)
        {
            if (!TryUnpack(id, out int slot, out long generation))
                return false;

            if (slot >= Volatile.Read(ref _slotCount))
                return false;

            // Lock-free read is safe because the entire alive/dead/generation state is
            // represented by one long value:
            //   +same generation => alive
            //   -same generation => disposed
            //   different generation => stale/reused slot
            return Volatile.Read(ref _slotStates[slot]) == generation;
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
        /// Disposing or checking a stale handle after Reset() is unsupported because a new handle may
        /// reuse the same slot and generation.
        /// </remarks>
        public static void Reset()
        {
            lock (_gate)
            {
                Array.Clear(_slotStates, 0, _slotStates.Length);

                // No need to clear _freeSlots; _freeCount=0 makes previous contents unreachable.
                _slotCount = 0;
                _freeCount = 0;
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
    }
#endif
}
