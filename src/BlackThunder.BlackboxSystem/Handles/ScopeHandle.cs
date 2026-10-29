namespace BlackThunder.BlackboxSystem
{
    public readonly struct ScopeHandle : System.IDisposable
    {
        private readonly HandleLease<ScopeHandle> _lease;
        private readonly LogContext _context;
        private readonly long _scopeId;
        private readonly TagHandle _tagHandle;
        public bool IsAlive => _context != null && _context.State.IsCurrent && HandleManager<ScopeHandle>.IsAlive(_lease.Id);
        public bool IsDisposed => !IsAlive;
        public long ScopeId => IsAlive ? _scopeId : -1;
        internal ScopeHandle(LogContext context, long scopeId, TagHandle tagHandle)
        {
            _context = context;
            _scopeId = scopeId;
            _tagHandle = tagHandle;
            _lease = context.ScopeHandles.Register(context.State);
        }
        public ScopeHandle With(params object[] tags) { _tagHandle.With(tags); return this; }
        public void Dispose()
        {
            if (_context == null) return;
            var released = _lease.Release();
            if (!_context.State.CheckWrite() || !released) return;
            _context.CloseScope(_scopeId);
        }
    }
}
