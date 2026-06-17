using System.Runtime.CompilerServices;
using System.Text;

namespace BlackThunder.BlackboxSystem
{
    public partial struct BlackboxHandle
    {
        [InterpolatedStringHandler]
        public ref struct WriteHandler
        {
            private StringBuilder _builder;
            public bool ShouldLog { get; }

            public WriteHandler(int literalLength, int formattedCount, BlackboxHandle handle, out bool shouldLog)
            {
                ShouldLog = handle.IsValid;
                shouldLog = ShouldLog;

                if (ShouldLog)
                    _builder = new StringBuilder(literalLength + formattedCount * 16);
                else
                    _builder = null;
            }

            public readonly void AppendLiteral(string value) => _builder?.Append(value);
            public readonly void AppendFormatted<T>(T value)
            {
                if (_builder == null) return;

                if (value is null)
                    _builder.Append("null");
                else
                    _builder.Append(value.ToString());
            }

            internal string GetTextAndClear()
            {
                var result = _builder?.ToString() ?? string.Empty;
                _builder = null;
                return result;
            }
        }
    }
}
