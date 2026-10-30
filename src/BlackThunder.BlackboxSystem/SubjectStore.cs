using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace BlackThunder.BlackboxSystem
{
    internal sealed class SubjectStore
    {
        // Front
#if BLACKBOX_TESTS
        internal Action<int> BeforeCleanupPublish;
#endif

        // Internal
        private const int CleanupBucketsPerInsertion = 16;

        private sealed class Node
        {
            internal readonly int Hash;
            internal readonly Blackbox Box;
            internal readonly Node Next;

            internal Node(int hash, Blackbox box, Node next)
            {
                Hash = hash;
                Box = box;
                Next = next;
            }
        }

        private readonly Node[] _buckets;
        private readonly SessionState _state;
        private int _cleanupCursor;


        // Content --------------------
        // Construction
        internal SubjectStore(SessionState state, int capacity)
        {
            if (capacity < 1 || (capacity & (capacity - 1)) != 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            _state = state;
            _buckets = new Node[capacity];
        }

        // Registration and lookup
        internal Blackbox GetOrAdd(object subject)
        {
            if (subject == null) throw new ArgumentNullException(nameof(subject));
            try
            {
                int hash = RuntimeHelpers.GetHashCode(subject);
                ref var slot = ref _buckets[hash & (_buckets.Length - 1)];
                Blackbox created = null;
                while (true)
                {
                    var head = Volatile.Read(ref slot);
                    for (var n = head; n != null; n = n.Next)
                        if (n.Hash == hash && ReferenceEquals(n.Box.Owner, subject)) return n.Box;
                    if (!_state.CheckWrite()) return null;

                    // Successful lookups never inspect unrelated weak owners or perform cleanup.
                    // On a miss, remove dead entries before publishing a new canonical box.
                    var cleaned = RemoveDead(head);
                    if (!ReferenceEquals(cleaned, head))
                    {
                        if (!ReferenceEquals(Interlocked.CompareExchange(ref slot, cleaned, head), head)) continue;
                        head = cleaned;
                    }
                    created ??= new Blackbox(subject, _state.Settings.StrongReference, _state);
                    if (ReferenceEquals(Interlocked.CompareExchange(ref slot, new Node(hash, created, head), head), head))
                    {
                        CleanupNextBuckets();
                        return created;
                    }
                }
            }
            finally { GC.KeepAlive(subject); }
        }

        internal bool Contains(object subject)
        {
            if (subject == null) throw new ArgumentNullException(nameof(subject));
            try
            {
                int hash = RuntimeHelpers.GetHashCode(subject);
                for (var n = Volatile.Read(ref _buckets[hash & (_buckets.Length - 1)]); n != null; n = n.Next)
                    if (n.Hash == hash && ReferenceEquals(n.Box.Owner, subject)) return true;
                return false;
            }
            finally { GC.KeepAlive(subject); }
        }

        internal int Count()
        {
            int count = 0;
            for (int i = 0; i < _buckets.Length; i++)
                for (var n = Volatile.Read(ref _buckets[i]); n != null; n = n.Next)
                    if (n.Box.Owner != null) count++;
            return count;
        }

        // Dead-entry maintenance
        internal void Cleanup()
        {
            for (int i = 0; i < _buckets.Length; i++) CleanupBucket(i);
        }

        private void CleanupNextBuckets()
        {
            // Reserve a disjoint range once per successful insertion, never for a hit or CAS retry.
            // Overflow only wraps the cursor; masking still visits every bucket on each lap.
            int count = Math.Min(CleanupBucketsPerInsertion, _buckets.Length);
            int first = unchecked(Interlocked.Add(ref _cleanupCursor, count) - count);
            for (int i = 0; i < count; i++) CleanupBucket(unchecked(first + i) & (_buckets.Length - 1));
        }

        private void CleanupBucket(int index)
        {
            var head = Volatile.Read(ref _buckets[index]);
            var cleaned = RemoveDead(head);
            if (ReferenceEquals(cleaned, head)) return;
#if BLACKBOX_TESTS
            BeforeCleanupPublish?.Invoke(index);
#endif
            // An insertion or another cleaner won: leave its chain intact and try on a later lap.
            Interlocked.CompareExchange(ref _buckets[index], cleaned, head);
        }

        private static Node RemoveDead(Node head)
        {
            Node lastDead = null;
            for (var n = head; n != null; n = n.Next)
                if (n.Box.Owner == null) lastDead = n;
            if (lastDead == null) return head;
            var prefix = new List<Node>();
            for (var n = head; !ReferenceEquals(n, lastDead); n = n.Next)
                if (n.Box.Owner != null) prefix.Add(n);
            var tail = lastDead.Next;
            for (int i = prefix.Count - 1; i >= 0; i--)
                tail = new Node(prefix[i].Hash, prefix[i].Box, tail);
            return tail;
        }
    }
}
