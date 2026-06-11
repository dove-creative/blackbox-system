using System;

namespace com.BlackThunder.BlackboxSystem
{
    public readonly struct ExertHandle : IDisposable
    {
        public readonly bool IsAlive => HandleManager<ExertHandle>.IsAlive(_id);
        public readonly bool IsDisposed => !IsAlive;

        private readonly long _id;
        private readonly int _createdThreadId;

        private readonly LogContext _targetContext;
        private readonly long _interactionId;


        internal ExertHandle(LogContext targetContext, long interactionId)
        {
            _id = HandleManager<ExertHandle>.GetHandleId();
            _createdThreadId = Environment.CurrentManagedThreadId;

            _targetContext = targetContext;
            _interactionId = interactionId;
        }

        public readonly void Dispose()
        {
            if (_id <= 0 || !HandleManager<ExertHandle>.TryDisposeHandleId(_id))
                return;

            if (Infrastructure.IsPrinted)
                return;

            HandleManager<ExertHandle>.WarnIfThreadMismatch(_createdThreadId, Environment.CurrentManagedThreadId);
            _targetContext.TryMergeScope(_interactionId);
        }
    }
}
