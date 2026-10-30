namespace BlackThunder.BlackboxSystem
{
    public readonly struct ExertHandle : System.IDisposable
    {
        private readonly HandleLease<ExertHandle> _lease;
        private readonly LogContext _targetContext;
        private readonly long _interactionId;
        public bool IsAlive => _targetContext != null && _targetContext.State.IsActive && HandleManager<ExertHandle>.IsAlive(_lease.Id);
        public bool IsDisposed => !IsAlive;
        internal ExertHandle(LogContext targetContext, long interactionId, HandleLease<ExertHandle> lease)
        {
            _targetContext = targetContext;
            _interactionId = interactionId;
            _lease = lease;
        }
        public void Dispose()
        {
            if (_targetContext == null) return;
            var released = _lease.Release();
            if (!_targetContext.State.CheckWrite() || !released) return;
            _targetContext.WarnThreadMismatch();
            _targetContext.TryMergeScope(_interactionId);
        }
    }
}
