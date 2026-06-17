using System.Linq;
using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    internal sealed class TagHandleTests : BlackboxTestBase
    {
        [Test]
        public void DefaultWithAndStringConversionDoNothing()
        {
            var target = BlackboxTestDoubles.Owner("target");

            // Table 2-1 / Default x With.ValidTarget
            var result = default(TagHandle).With(target);

            // Table 2-1 / Default x ToString
            string text = default(TagHandle);

            Assert.That(result, Is.EqualTo(string.Empty));
            Assert.That(text, Is.EqualTo(string.Empty));
            Assert.That(BlackboxRegistry.Count(), Is.EqualTo(0));
        }

        [Test]
        public void FallbackMessageConvertsToOriginalMessage()
        {
            // Table 2-1 / FallbackMessage x ToString
            var handle = TagHandle.FromMessage("fallback");

            string text = handle;
            var withResult = handle.With(BlackboxTestDoubles.Owner("target"));

            Assert.That(text, Is.EqualTo("fallback"));
            Assert.That(withResult, Is.EqualTo("fallback"));
        }

        [Test]
        public void WithValidTargetAddsSourceAndTargetTags()
        {
            var source = BlackboxTestDoubles.Blackbox("source");
            var targetOwner = BlackboxTestDoubles.Owner("target");
            var target = BlackboxTestDoubles.BlackboxFor(targetOwner);

            // Table 2-1 / SourceLog x With.ValidTarget
            var rendered = source.Write("hello %0", "Run").With(targetOwner, TargetTypes.Full);

            Assert.That(rendered, Does.Contain("hello"));
            Assert.That(rendered, Does.Contain("target"));
            Assert.That(BlackboxTestDoubles.Lines(target).Single(), Does.Contain("tagged by"));
            Assert.That(BlackboxTestDoubles.Lines(target).Single(), Does.Contain("hello"));
        }

        [Test]
        public void WithNullTargetTagsNull()
        {
            var source = BlackboxTestDoubles.Blackbox("source");

            // Table 2-1 / SourceLog x With.NullTarget
            var rendered = source.Write("hello %0", "Run").With(null);

            Assert.That(rendered, Does.Contain("hello null"));
            Assert.That(BlackboxRegistry.Count(), Is.EqualTo(1));
        }

        [Test]
        public void WithNullArrayTagsNull()
        {
            var source = BlackboxTestDoubles.Blackbox("source");
            var handle = source.Write("hello", "Run");

            // Table 2-1 / SourceLog x With.NullArray
            var rendered = handle.With((object[])null);

            Assert.That(rendered, Does.Contain("hello"));
            Assert.That(rendered, Does.Contain("null"));
            Assert.That(BlackboxRegistry.Count(), Is.EqualTo(1));
        }

        [Test]
        public void WithTargetTypesLastOverridesTargetLogPolicy()
        {
            BlackboxHandle.TagTargetTypes = TargetTypes.None;
            var source = BlackboxTestDoubles.Blackbox("source");
            var targetOwner = BlackboxTestDoubles.Owner("target");
            var target = BlackboxTestDoubles.BlackboxFor(targetOwner);

            // Table 2-1 / SourceLog x With.TargetTypesLast
            source.Write("hello", "Run").With(targetOwner, TargetTypes.Name);

            var targetLine = BlackboxTestDoubles.Lines(target).Single();
            Assert.That(targetLine, Does.Contain("tagged by"));
            Assert.That(targetLine, Does.Not.Contain("hello"));
        }
    }
}
