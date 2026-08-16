using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    internal sealed class BlackboxRuntimeTests : BlackboxTestBase
    {
        [Test]
        public void IdsIncreaseIndependently()
        {
            // Table 3-1 / Ready x GetNextBlackboxId
            Assert.That(BlackboxRuntime.GetNextBlackboxId(), Is.EqualTo(0));
            Assert.That(BlackboxRuntime.GetNextBlackboxId(), Is.EqualTo(1));

            // Table 3-1 / Ready x GetNextInteractionId
            Assert.That(BlackboxRuntime.GetNextInteractionId(), Is.EqualTo(0));
            Assert.That(BlackboxRuntime.GetNextInteractionId(), Is.EqualTo(1));

            // Table 3-1 / Ready x GetNextSequence
            Assert.That(BlackboxRuntime.GetNextSequence(), Is.EqualTo(0));
            Assert.That(BlackboxRuntime.GetNextSequence(), Is.EqualTo(1));
        }

        [Test]
        public void ResetRestartsCounters()
        {
            BlackboxRuntime.GetNextBlackboxId();
            BlackboxRuntime.GetNextInteractionId();
            BlackboxRuntime.GetNextSequence();

            // Table 3-1 / Ready x Reset
            BlackboxRuntime.Reset();

            Assert.That(BlackboxRuntime.GetNextBlackboxId(), Is.EqualTo(0));
            Assert.That(BlackboxRuntime.GetNextInteractionId(), Is.EqualTo(0));
            Assert.That(BlackboxRuntime.GetNextSequence(), Is.EqualTo(0));
        }
    }
}
