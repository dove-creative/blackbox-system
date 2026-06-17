using System;

namespace BlackThunder.BlackboxSystem
{
    internal enum ScopeType
    {
        None,
        Open,
        Close,
        Step,
    }

    internal struct LogData
    {
        // Front ----------
        // Property
        public Blackbox Owner { get; }
        public bool IsValid { get; internal set; }

        // Message
        public string Message { get; internal set; }
        public DateTime Time { get; }
        public int ScopeDepth { get; internal set; } // Set during output generation.
        public long Sequence { get; }

        // Scope
        public string MethodName { get; }
        public ScopeType ScopeType { get; internal set; }
        public long ScopeId { get; }

        // Thread
        public int ThreadId { get; }
        public string ThreadName { get; }

        // Exertion
        public long InteractionId { get; internal set; }
        public Blackbox ExertingTo { get; internal set; }
        public Blackbox ExertedBy { get; internal set; }

        // Interaction
        public Blackbox TaggedBy { get; internal set; }
        public readonly bool IsTagged => TaggedBy != null;
        public TargetTypes TagTargetTypes { get; internal set; }

        public readonly struct Tag
        {
            public long Id { get; }
            public Blackbox Target { get; }

            public Tag(long id, Blackbox target)
            {
                Id = id;
                Target = target;
            }
        }

        public Tag[] Tags { get; internal set; }

        // Content ----------
        internal LogData(
            Blackbox owner,
            string message,
            DateTime time,
            long sequence,
            string methodName,
            ScopeType scopeType,
            long scopeId,
            int threadId,
            string threadName,
            long interactionId,
            Blackbox exertingTo,
            Blackbox exertedBy,
            Tag[] tags,
            Blackbox taggedBy,
            TargetTypes tagTargetTypes = TargetTypes.Full)
        {
            Owner = owner;
            IsValid = true;

            Message = message;
            Time = time;
            ScopeDepth = 0;
            Sequence = sequence;

            ThreadId = threadId;
            ThreadName = threadName;

            MethodName = methodName;
            ScopeType = scopeType;
            ScopeId = scopeId;

            InteractionId = interactionId;
            ExertingTo = exertingTo;
            ExertedBy = exertedBy;

            TaggedBy = taggedBy;
            TagTargetTypes = tagTargetTypes;
            Tags = tags;
        }

        public override readonly string ToString() => LogFormatter.RenderTextLine(this, ScopeDepth);
        public readonly string ToString(int scopeDepth) => LogFormatter.RenderTextLine(this, scopeDepth);
    }
}
