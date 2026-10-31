using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    internal class PublicApiCoverageTests : BlackboxTestBase
    {
        [Test, RunWithUniTest]
        public void EveryPublicSignatureHasAReviewedTestFamily()
        {
            // Coverage inventory: adding or changing an API requires reviewing its behavior tests as well.
            var assembly = typeof(BlackboxHandle).Assembly;
            using var stream = assembly.GetManifestResourceStream("Blackbox.PublicApiCoverage.txt");
            Assert.That(stream, Is.Not.Null);
            using var reader = new StreamReader(stream);
            var entries = reader.ReadToEnd().Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith("#", StringComparison.Ordinal))
                .Select(line => line.Split('|')).ToArray();
            Assert.That(entries.All(parts => parts.Length == 4), Is.True, "Every signature needs a test-family mapping.");
            var expected = entries.Select(parts => string.Join("|", parts.Take(3))).ToArray();
            Assert.That(expected.Distinct().Count(), Is.EqualTo(expected.Length), "Duplicate API mappings hide omissions.");

            var actual = assembly.GetExportedTypes()
                .Where(type => type.Namespace == typeof(BlackboxHandle).Namespace)
                .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(IsContractMember)
                    .Select(member => type.FullName + "|" + member.MemberType + "|" + member))
                .ToArray();
            Assert.That(actual, Is.EquivalentTo(expected),
                "Review the added/changed public contract, add UniTest assertions, then update PublicApiCoverage.txt.");

            foreach (var entry in entries)
            {
                var fixture = assembly.GetType(typeof(PublicApiCoverageTests).Namespace + "." + entry[3]);
                Assert.That(fixture, Is.Not.Null, "Unknown test family for " + entry[2]);
                Assert.That(fixture.GetMethods().Any(method => method.IsDefined(typeof(RunWithUniTestAttribute), true)),
                    Is.True, "Mapped family must execute through UniTest: " + entry[3]);
            }
        }

        private static bool IsContractMember(MemberInfo member)
        {
            if (member is MethodInfo method)
                return !method.IsSpecialName || method.Name.StartsWith("op_", StringComparison.Ordinal);
            return member is ConstructorInfo || member is PropertyInfo || member is FieldInfo && member.Name != "value__";
        }
    }
}
