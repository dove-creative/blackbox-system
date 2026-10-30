using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    internal sealed class SessionCounterTests : BlackboxTestBase
    {
        [Test]
        public void IdsIncreaseIndependently()
        {
            // Table 3-1 / Ready x NextBoxId
            Assert.That(BlackboxSessionRegistry.Current.State.NextBoxId(), Is.EqualTo(0));
            Assert.That(BlackboxSessionRegistry.Current.State.NextBoxId(), Is.EqualTo(1));

            // Table 3-1 / Ready x NextInteractionId
            Assert.That(BlackboxSessionRegistry.Current.State.NextInteractionId(), Is.EqualTo(0));
            Assert.That(BlackboxSessionRegistry.Current.State.NextInteractionId(), Is.EqualTo(1));

            // Table 3-1 / Ready x NextSequence
            Assert.That(BlackboxSessionRegistry.Current.State.NextSequence(), Is.EqualTo(0));
            Assert.That(BlackboxSessionRegistry.Current.State.NextSequence(), Is.EqualTo(1));
        }

        [Test]
        public void ResetRestartsCounters()
        {
            BlackboxSessionRegistry.Current.State.NextBoxId();
            BlackboxSessionRegistry.Current.State.NextInteractionId();
            BlackboxSessionRegistry.Current.State.NextSequence();

            // Table 3-1 / Ready x Reset
            BlackboxHandle.RestartDefaultSession();

            Assert.That(BlackboxSessionRegistry.Current.State.NextBoxId(), Is.EqualTo(0));
            Assert.That(BlackboxSessionRegistry.Current.State.NextInteractionId(), Is.EqualTo(0));
            Assert.That(BlackboxSessionRegistry.Current.State.NextSequence(), Is.EqualTo(0));
        }
    }
}
