using System;
using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    internal sealed class BlackboxSessionRegistryTests : BlackboxTestBase
    {
        [Test]
        [RunWithUniTest]
        public void GetBlackboxRejectsNull()
        {
            // Table 3-2 / Empty x GetBlackbox.NullSubject
            Assert.That(() => BlackboxTestDoubles.BlackboxFor(null), Throws.TypeOf<ArgumentNullException>());

            // Table 3-2 / Registered x Contains.NullSubject
            Assert.That(() => BlackboxSessionRegistry.Contains(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        [RunWithUniTest]
        public void GetBlackboxCreatesAndReusesOwner()
        {
            var owner = BlackboxTestDoubles.Owner("owner");

            // Table 3-2 / Empty x GetBlackbox.ValidSubject
            var first = BlackboxTestDoubles.BlackboxFor(owner);

            // Table 3-2 / Registered x GetBlackbox.ValidSubject
            var second = BlackboxTestDoubles.BlackboxFor(owner);

            Assert.That(first, Is.SameAs(second));
            Assert.That(first.Owner, Is.SameAs(owner));
            Assert.That(BlackboxSessionRegistry.Count(), Is.EqualTo(1));
        }

        [Test]
        [RunWithUniTest]
        public void ContainsAndCountTrackRegisteredOwners()
        {
            var owner = BlackboxTestDoubles.Owner("owner");

            // Table 3-2 / Empty x Contains.ValidSubject
            Assert.That(BlackboxSessionRegistry.Contains(owner), Is.False);

            BlackboxTestDoubles.BlackboxFor(owner);

            // Table 3-2 / Registered x Contains.ValidSubject
            Assert.That(BlackboxSessionRegistry.Contains(owner), Is.True);

            // Table 3-2 / Registered x Count
            Assert.That(BlackboxSessionRegistry.Count(), Is.EqualTo(1));
        }

        [Test]
        [RunWithUniTest]
        public void RestartDefaultSessionClearsRegistryRuntimeAndHandles()
        {
            var owner = BlackboxTestDoubles.Owner("owner");
            var blackbox = BlackboxTestDoubles.BlackboxFor(owner);
            var scope = BlackboxTestDoubles.Scope(blackbox, "scope", "Run");
            Assert.That(scope.IsAlive, Is.True);
            Assert.That(BlackboxSessionRegistry.Current.State.NextSequence(), Is.GreaterThanOrEqualTo(0));

            // Table 3-2 / Registered x RestartDefaultSession
            BlackboxHandle.RestartDefaultSession();

            Assert.That(BlackboxSessionRegistry.Count(), Is.EqualTo(0));
            Assert.That(scope.IsAlive, Is.False);
            Assert.That(BlackboxSessionRegistry.Current.State.NextSequence(), Is.EqualTo(0));
            Assert.That((BlackboxSessionRegistry.Current.State.Crash != null), Is.False);
        }

        [Test]
        [RunWithUniTest]
        public void StrongReferencePolicyKeepsOwnerReference()
        {
            BlackboxHandle.StrongReference = true;
            var owner = BlackboxTestDoubles.Owner("owner");

            // Table 3-2 / Empty x GetBlackbox.StrongReferenceOn
            var blackbox = BlackboxTestDoubles.BlackboxFor(owner);

            Assert.That(blackbox.Owner, Is.SameAs(owner));
            Assert.That(blackbox.OwnerString, Is.EqualTo("owner"));
        }

        [Test]
        [RunWithUniTest]
        public void WeakReferencePolicyStoresWeakOwnerReference()
        {
            var weakCase = CreateWeakOwnerCase();
            BlackboxTestDoubles.ForceFullCollection();

            // Table 3-2 / Empty x GetBlackbox.StrongReferenceOff
            Assert.That(weakCase.WeakReference.IsAlive, Is.False);
            Assert.That(GetField(weakCase.Blackbox, "_strongOwner"), Is.Null);
            Assert.That(GetField(weakCase.Blackbox, "_weakOwner"), Is.Not.Null);
            Assert.That(weakCase.Blackbox.OwnerString, Is.EqualTo("weak-owner"));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static WeakOwnerCase CreateWeakOwnerCase()
        {
            BlackboxHandle.StrongReference = false;
            var owner = BlackboxTestDoubles.Owner("weak-owner");
            var weakReference = new WeakReference(owner);
            var blackbox = BlackboxTestDoubles.BlackboxFor(owner);
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
}
