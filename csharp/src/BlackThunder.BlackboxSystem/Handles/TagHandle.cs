using System;

namespace BlackThunder.BlackboxSystem
{
    /// <summary>
    /// Defines which source details are written to tagged target logs.
    /// </summary>
    [Flags]
    public enum TargetTypes
    {
        /// <summary>
        /// Writes only a minimal tag marker to the target log.
        /// </summary>
        None = 0,

        /// <summary>
        /// Writes the source object name and id to the target log.
        /// </summary>
        Name = 1 << 0,

        /// <summary>
        /// Writes the tag interaction id to the target log.
        /// </summary>
        InteractionId = 1 << 1,

        /// <summary>
        /// Writes the source message to the target log.
        /// </summary>
        Message = 1 << 2,

        /// <summary>
        /// Writes every source detail to the target log.
        /// </summary>
        Full = Name | InteractionId | Message,

        /// <summary>
        /// Uses the default target log policy from Blackbox settings.
        /// </summary>
        BasedOnSettings = 1 << 100,
    }

    /// <summary>
    /// Handle returned from a write operation so related targets can be attached to the log.
    /// </summary>
    /// <remarks>
    /// Use this handle immediately in a fluent call after the write operation.
    /// </remarks>
    public readonly struct TagHandle
    {
        private readonly LogContext _context;
        private readonly long _logIndex;
        private readonly string _fallbackMessage;
        // Targets are looked up in this session's store. Recording code creates unbound handles;
        // the front-side caller binds the session that owns the written Blackbox.
        private readonly BlackboxSession _session;


        internal TagHandle(LogContext context, long logIndex, string fallbackMessage, BlackboxSession session = null)
        {
            _context = context;
            _logIndex = logIndex;
            _fallbackMessage = fallbackMessage;
            _session = session;
        }

        internal static TagHandle FromMessage(string message) => new TagHandle(null, -1, message);
        internal TagHandle Bind(BlackboxSession session) => new TagHandle(_context, _logIndex, _fallbackMessage, session);

        /// <summary>
        /// Attaches related targets to the written log.
        /// </summary>
        /// <param name="tags">
        /// Objects to tag. If the last argument is <see cref="TargetTypes"/>, it controls what is written
        /// to target logs. Passing null tags a null target.
        /// </param>
        public string With(params object[] tags)
        {
            if (_context == null || !_context.State.CheckWrite())
                return _fallbackMessage ?? string.Empty;
            if (_session == null)
                return (string)this;

            tags ??= new object[1];

            var targetTypes = TargetTypes.BasedOnSettings;
            var tagCount = tags.Length;
            if (tagCount > 0 && tags[tagCount - 1] is TargetTypes types)
            {
                targetTypes = types;
                tagCount--;
            }

            var targets = new Blackbox[tagCount];
            for (int i = 0; i < tagCount; i++)
            {
                var tag = tags[i];
                if (tag != null)
                    targets[i] = _session.Subjects.GetOrAdd(tag);
            }

            _context.ResolveWith(
                _logIndex,
                targets,
                targetTypes);

            return (string)this;
        }

        public static implicit operator string(TagHandle handle)
        {
            if (handle._context == null)
                return handle._fallbackMessage ?? string.Empty;

            return handle._context.RenderMessage(handle._logIndex, handle._fallbackMessage);
        }
    }
}
