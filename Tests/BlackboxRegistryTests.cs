using System;
using NUnit.Framework;

namespace com.BlackThunder.BlackboxSystem.Tests
{
#if BLACKBOX
    internal sealed class BlackboxRegistryTests : BlackboxTestBase
    {
        [Test]
        public void GetBlackboxRejectsNull()
        {
            // Table 3-2 / Empty x GetBlackbox.NullSubject
            Assert.That(() => BlackboxRegistry.GetBlackbox(null), Throws.TypeOf<ArgumentNullException>());

            // Table 3-2 / Registered x Contains.NullSubject
            Assert.That(() => BlackboxRegistry.Contains(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void GetBlackboxCreatesAndReusesOwner()
        {
            var owner = BlackboxTestDoubles.Owner("owner");

            // Table 3-2 / Empty x GetBlackbox.ValidSubject
            var first = BlackboxRegistry.GetBlackbox(owner);

            // Table 3-2 / Registered x GetBlackbox.ValidSubject
            var second = BlackboxRegistry.GetBlackbox(owner);

            Assert.That(first, Is.SameAs(second));
            Assert.That(first.Owner, Is.SameAs(owner));
            Assert.That(BlackboxRegistry.Count(), Is.EqualTo(1));
        }

        [Test]
        public void ContainsAndCountTrackRegisteredOwners()
        {
            var owner = BlackboxTestDoubles.Owner("owner");

            // Table 3-2 / Empty x Contains.ValidSubject
            Assert.That(BlackboxRegistry.Contains(owner), Is.False);

            BlackboxRegistry.GetBlackbox(owner);

            // Table 3-2 / Registered x Contains.ValidSubject
            Assert.That(BlackboxRegistry.Contains(owner), Is.True);

            // Table 3-2 / Registered x Count
            Assert.That(BlackboxRegistry.Count(), Is.EqualTo(1));
        }

        [Test]
        public void ForceResetClearsRegistryRuntimeAndHandles()
        {
            var owner = BlackboxTestDoubles.Owner("owner");
            var blackbox = BlackboxRegistry.GetBlackbox(owner);
            var scope = blackbox.WriteScope("scope", "Run");
            Assert.That(scope.IsAlive, Is.True);
            Assert.That(BlackboxRuntime.GetNextSequence(), Is.GreaterThanOrEqualTo(0));

            // Table 3-2 / Registered x ForceReset
            BlackboxRegistry.ForceReset();

            Assert.That(BlackboxRegistry.Count(), Is.EqualTo(0));
            Assert.That(scope.IsAlive, Is.False);
            Assert.That(BlackboxRuntime.GetNextSequence(), Is.EqualTo(0));
            Assert.That(Infrastructure.IsPrinted, Is.False);
        }

        [Test]
        public void StrongReferencePolicyKeepsOwnerReference()
        {
            BlackboxHandle.StrongReference = true;
            var owner = BlackboxTestDoubles.Owner("owner");

            // Table 3-2 / Empty x GetBlackbox.StrongReferenceOn
            var blackbox = BlackboxRegistry.GetBlackbox(owner);

            Assert.That(blackbox.Owner, Is.SameAs(owner));
            Assert.That(blackbox.OwnerString, Is.EqualTo("owner"));
        }

        [Test]
        public void WeakReferencePolicyStoresWeakOwnerReference()
        {
            var weakCase = CreateWeakOwnerCase();

            // Table 3-2 / Empty x GetBlackbox.StrongReferenceOff
            Assert.That(weakCase.WeakReference.Target, Is.Not.Null);
            Assert.That(GetField(weakCase.Blackbox, "_strongOwner"), Is.Null);
            Assert.That(GetField(weakCase.Blackbox, "_weakOwner"), Is.Not.Null);
            Assert.That(weakCase.Blackbox.OwnerString, Is.EqualTo("weak-owner"));
        }

        private static WeakOwnerCase CreateWeakOwnerCase()
        {
            BlackboxHandle.StrongReference = false;
            var owner = BlackboxTestDoubles.Owner("weak-owner");
            var weakReference = new WeakReference(owner);
            var blackbox = BlackboxRegistry.GetBlackbox(owner);
            owner = null;

            return new WeakOwnerCase(weakReference, blackbox);
        }

        private readonly struct WeakOwnerCase
        {
            public readonly WeakReference WeakReference;
            public readonly Blackbox Blackbox;

            public WeakOwnerCase(WeakReference weakReference, Blackbox blackbox)
            {
                WeakReference = weakReference;
                Blackbox = blackbox;
            }
        }

        private static object GetField(object instance, string name)
        {
            return instance
                .GetType()
                .GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(instance);
        }
    }
#endif
}
