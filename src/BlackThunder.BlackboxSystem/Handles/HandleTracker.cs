using System.Threading;

namespace BlackThunder.BlackboxSystem
{
    // Shared by contexts in one session stripe; never holds a context or subject reference.
    internal sealed class HandleTracker<T>
    {
        internal sealed class Segment
        {
            internal readonly long[] Ids = new long[32];
            internal readonly Segment Next;
            internal Segment(Segment next) { Next = next; }
        }
        private static readonly Segment Closed = new(null);
        private Segment _head = new(null);
        internal HandleLease<T> Register(SessionState state)
        {
            if (!state.CheckWrite()) return default;
            var id = HandleManager<T>.GetHandleId();
            try
            {
#if BLACKBOX_TESTS
                state.BeforeHandleRegistration?.Invoke();
#endif
                while (true)
                {
                    var head = Volatile.Read(ref _head);
                    if (ReferenceEquals(head, Closed) || !state.CanWrite)
                    {
                        HandleManager<T>.TryDisposeHandleId(id);
                        return default;
                    }
                    for (var segment = head; segment != null; segment = segment.Next)
                    for (int i = 0; i < segment.Ids.Length; i++)
                    {
                        if (Interlocked.CompareExchange(ref segment.Ids[i], id, 0) != 0) continue;
                        var lease = new HandleLease<T>(segment, i, id);
                        if (!state.CanWrite || ReferenceEquals(Volatile.Read(ref _head), Closed))
                        {
                            lease.Release();
                            return default;
                        }
                        return lease;
                    }
                    Interlocked.CompareExchange(ref _head, new Segment(head), head);
                }
            }
            catch
            {
                HandleManager<T>.TryDisposeHandleId(id);
                throw;
            }
        }
        public void Close()
        {
            var head = Interlocked.Exchange(ref _head, Closed);
            for (var segment = head; segment != null && !ReferenceEquals(segment, Closed); segment = segment.Next)
            for (int i = 0; i < segment.Ids.Length; i++)
            {
                // Closed cells cannot be reused by a registrar which captured an older head.
                var id = Interlocked.Exchange(ref segment.Ids[i], -1);
                if (id > 0) HandleManager<T>.TryDisposeHandleId(id);
            }
        }
    }
    internal readonly struct HandleLease<T>
    {
        private readonly HandleTracker<T>.Segment _segment;
        private readonly int _index;
        internal readonly long Id;
        internal HandleLease(HandleTracker<T>.Segment segment, int index, long id)
        { _segment = segment; _index = index; Id = id; }
        internal bool Release()
        {
            if (Id <= 0) return false;
            Interlocked.CompareExchange(ref _segment.Ids[_index], 0, Id);
            return HandleManager<T>.TryDisposeHandleId(Id);
        }
    }
}
