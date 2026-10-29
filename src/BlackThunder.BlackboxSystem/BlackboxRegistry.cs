using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace BlackThunder.BlackboxSystem
{
    internal static class BlackboxRegistry
    {
        internal static Blackbox GetBlackbox(object subject) => BlackboxSession.CurrentState.Subjects.GetOrAdd(subject);
        internal static bool Contains(object subject) => BlackboxSession.CurrentState.Subjects.Contains(subject);
        internal static int Count() => BlackboxSession.CurrentState.Subjects.Count();
        public static void ForceReset() => BlackboxSession.ResetCurrent();
    }

    internal sealed class SubjectStore
    {
        private sealed class Node
        {
            internal readonly int Hash;
            internal readonly Blackbox Box;
            internal readonly Node Next;
            internal Node(int hash, Blackbox box, Node next) { Hash = hash; Box = box; Next = next; }
        }
        private readonly Node[] _buckets;
        private readonly SessionState _state;
        internal SubjectStore(SessionState state, int capacity)
        {
            if (capacity < 1 || (capacity & (capacity - 1)) != 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _state = state;
            _buckets = new Node[capacity];
        }
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
                    bool dead = false;
                    for (var n = head; n != null; n = n.Next)
                    {
                        var owner = n.Box.Owner;
                        if (n.Hash == hash && ReferenceEquals(owner, subject)) return n.Box;
                        dead |= owner == null;
                    }
                    if (!_state.CheckWrite()) return null;
                    if (dead)
                    {
                        var cleaned = RemoveDead(head);
                        if (!ReferenceEquals(Interlocked.CompareExchange(ref slot, cleaned, head), head)) continue;
                        head = cleaned;
                    }
                    created ??= new Blackbox(subject, _state.Settings.StrongReference, _state);
                    if (ReferenceEquals(Interlocked.CompareExchange(ref slot, new Node(hash, created, head), head), head)) return created;
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
                for (var n = Volatile.Read(ref _buckets[i]); n != null; n = n.Next) if (n.Box.Owner != null) count++;
            return count;
        }
        internal void Cleanup()
        {
            for (int i = 0; i < _buckets.Length; i++)
            {
                var head = Volatile.Read(ref _buckets[i]);
                var cleaned = RemoveDead(head);
                if (!ReferenceEquals(cleaned, head)) Interlocked.CompareExchange(ref _buckets[i], cleaned, head);
            }
        }
        private static Node RemoveDead(Node head)
        {
            Node lastDead = null;
            for (var n = head; n != null; n = n.Next) if (n.Box.Owner == null) lastDead = n;
            if (lastDead == null) return head;
            var prefix = new List<Node>();
            for (var n = head; !ReferenceEquals(n, lastDead); n = n.Next)
                if (n.Box.Owner != null) prefix.Add(n);
            var tail = lastDead.Next;
            for (int i = prefix.Count - 1; i >= 0; i--) tail = new Node(prefix[i].Hash, prefix[i].Box, tail);
            return tail;
        }
    }
}
