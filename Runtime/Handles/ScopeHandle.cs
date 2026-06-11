using System;

namespace com.BlackThunder.BlackboxSystem
{
    public readonly struct ScopeHandle : IDisposable
    {
        public readonly bool IsAlive => HandleManager<ScopeHandle>.IsAlive(_id);
        public readonly bool IsDisposed => !IsAlive;
        public readonly long ScopeId => IsAlive ? _scopeId : -1;

        private readonly long _id;
        private readonly int _createdThreadId;

        private readonly LogContext _context;
        private readonly long _scopeId;
        private readonly TagHandle _tagHandle;


        internal ScopeHandle(LogContext context, long scopeId, TagHandle tagHandle)
        {
            _id = HandleManager<ScopeHandle>.GetHandleId();
            _createdThreadId = Environment.CurrentManagedThreadId;

            _context = context;
            _scopeId = scopeId;
            _tagHandle = tagHandle;
        }

        /// <summary>
        /// Attaches related targets to the scope open log.
        /// </summary>
        /// <param name="tags">
        /// Objects to tag. Passing null tags a null target.
        /// </param>
        /// <returns>The same scope handle so using statements can keep owning the scope lifetime.</returns>
        public readonly ScopeHandle With(params object[] tags)
        {
            _tagHandle.With(tags);
            return this;
        }

        public readonly void Dispose()
        {
            if (_id <= 0 || !HandleManager<ScopeHandle>.TryDisposeHandleId(_id))
                return;

            if (Infrastructure.IsPrinted) return;
            if (_context == null) return;

            HandleManager<ScopeHandle>.WarnIfThreadMismatch(_createdThreadId, Environment.CurrentManagedThreadId);
            _context.CloseScope(_scopeId, BlackboxRuntime.GetNextSequence());
        }
    }
}
