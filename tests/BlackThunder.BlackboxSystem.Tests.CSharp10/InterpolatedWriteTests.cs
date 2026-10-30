using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests.CSharp10
{
    // Uses only the public API of the packaged netstandard2.1 library, as a C# 10 consumer would.
    internal class InterpolatedWriteTests
    {
        // Internal
        private bool _useBlackbox;
        private int _formatCalls;


        // Content --------------------
        // Fixture
        [SetUp]
        public void SetUp()
        {
            _useBlackbox = BlackboxHandle.UseBlackbox;
            _formatCalls = 0;
        }

        [TearDown]
        public void TearDown()
        {
            BlackboxHandle.UseBlackbox = _useBlackbox;
            BlackboxHandle.DisposeActiveSessions();
            BlackboxHandle.RestartDefaultSession();
        }

        private string FormatValue()
        {
            _formatCalls++;
            return "formatted";
        }

        // Write
        [Test]
        public void DisabledHandleSkipsFormattedValues()
        {
            BlackboxHandle.UseBlackbox = false;
            var handle = BlackboxHandle.Of(new object(), label: "owner");

            handle.Write($"value {FormatValue()}", "Run");

            Assert.That(_formatCalls, Is.Zero);
        }

        [Test]
        public void ValidHandleFormatsValuesOnce()
        {
            BlackboxHandle.UseBlackbox = true;
            using var session = BlackboxHandle.BeginSession();
            var handle = BlackboxHandle.Of(new object(), label: "owner");

            handle.Write($"value {FormatValue()}", "Run");

            Assert.That(handle.IsValid, Is.True);
            Assert.That(_formatCalls, Is.EqualTo(1));
        }
    }
}
