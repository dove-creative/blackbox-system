using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using BlackThunder.UniTest;

namespace BlackThunder.BlackboxSystem.Tests
{
    [NonParallelizable]
    [UniTestProject]
    internal sealed class BlackboxTableUniTests
    {
        private const int ContinuousScenarioDepth = 30;
        // Toggle this on when the complete UniTest JSON report is useful for diagnosis.
        private const bool PrintResultJson = false;

        [TestCaseSource(typeof(BlackboxTableScenarioFactory), nameof(BlackboxTableScenarioFactory.TableProjectCases))]
        [RunWithUniTest]
        public async Task TableProjectsExecute(string projectName)
        {
            Node<BlackboxTableModel> result = null;

            try
            {
                var project = BlackboxTableScenarioFactory.CreateProject(projectName, false);
                result = await project.Execute(project.CompleteExecutionDepth);

                PrintResultJsonIfEnabled(project.Name, result);
                AssertProjectSucceeded(project, result);
                AssertCompleteCoverage(project, result);
            }
            finally
            {
                CleanupResult(result);
            }
        }

        // Continuous cases still use seeded random paths, but the project picks one candidate per step
        // because Blackbox runtime/logger state is global.
        [TestCaseSource(typeof(BlackboxTableScenarioFactory), nameof(BlackboxTableScenarioFactory.ContinuousTableProjectCases))]
        [RunWithUniTest]
        public async Task TableProjectsExecuteContinuously(string projectName, int seed)
        {
            Node<BlackboxTableModel> result = null;

            try
            {
                var project = BlackboxTableScenarioFactory.CreateProject(projectName, true, seed);
                result = await project.ExecuteContinuously(ContinuousScenarioDepth + 1, seed);

                PrintResultJsonIfEnabled(project.Name + " Seed " + seed, result);
                AssertProjectSucceeded(project, result);
                Assert.That(GetMaxExecutionCount(result), Is.InRange(1, ContinuousScenarioDepth + 1), project.Name);
            }
            finally
            {
                CleanupResult(result);
            }
        }

        [Test]
        [RunWithUniTest]
        public async Task CompleteCoverageVisitsEveryRegisteredScenarioAfterFreeze()
        {
            // Table coverage / a terminal state must not filter later registered cases or truncate after ten steps.
            var scenarios = Enumerable.Range(0, 14).Select(index => new TableScenario(
                "Case" + index,
                model =>
                {
                    Assert.That(BlackboxSessionRegistry.Current.State.CanWrite, Is.True);
                    if (index == 0)
                    {
                        BlackboxSessionRegistry.Current.State.TryFreeze(new CrashInfo("terminal", string.Empty));
                        model.Printed = true;
                    }
                }, model => !model.Printed)).ToArray();
            var project = new BlackboxTableProject("CoverageRegression", false, 0, scenarios);
            Node<BlackboxTableModel> result = null;

            try
            {
                result = await project.Execute(project.CompleteExecutionDepth);
                AssertProjectSucceeded(project, result);
                AssertCompleteCoverage(project, result);
            }
            finally
            {
                CleanupResult(result);
            }
        }

        [Test]
        [RunWithUniTest]
        public async Task RegisteredScenarioThatCannotRunFailsInsteadOfDisappearing()
        {
            // Table coverage / impossible prerequisites are a failed definition, not successful partial coverage.
            var project = new BlackboxTableProject("BlockedCoverage", false, 0,
                new[] { new TableScenario("Blocked", _ => Assert.Fail("A blocked action must not run."), _ => false) });
            Node<BlackboxTableModel> result = null;

            try
            {
                result = await project.Execute(project.CompleteExecutionDepth);
                Assert.That(result.AllSucceed(out _), Is.False);
                Assert.That(result.GetFailedReports().ToJson(), Does.Contain("BlockedCoverage/Blocked"));
                Assert.That(GetMostExecutedModel(result).ExecutedScenarioIds, Is.Empty);
            }
            finally
            {
                CleanupResult(result);
            }
        }

        [Test]
        [RunWithUniTest]
        public void DuplicateScenarioIdsAreRejected()
        {
            // Table coverage / duplicate IDs must not conceal a missing scenario in the execution ledger.
            var scenario = new TableScenario("duplicate", _ => { }, _ => true);
            Assert.Throws<ArgumentException>(() => new BlackboxTableProject("Duplicates", false, 0,
                new[] { scenario, scenario }));
        }

        private static void AssertProjectSucceeded(BlackboxTableProject project, Node<BlackboxTableModel> result)
        {
            bool cancelled;
            Assert.That(result.AllSucceed(out cancelled), Is.True, project.Name + Environment.NewLine + result.GetFailedReports().ToJson());
            Assert.That(cancelled, Is.False, project.Name);
        }

        private static void AssertCompleteCoverage(BlackboxTableProject project, Node<BlackboxTableModel> result)
        {
            var model = GetMostExecutedModel(result);
            Assert.That(model.ExecutionCount, Is.EqualTo(project.CompleteExecutionDepth), project.Name);
            Assert.That(model.ExecutedScenarioIds, Is.EqualTo(project.ScenarioIds),
                "Every registered scenario must complete once in declaration order: " + project.Name);
        }

        private static BlackboxTableModel GetMostExecutedModel(Node<BlackboxTableModel> node)
        {
            var model = node.Model;
            foreach (var after in node.Afters)
            {
                var candidate = GetMostExecutedModel(after);
                if (candidate.ExecutionCount > model.ExecutionCount)
                    model = candidate;
            }
            return model;
        }

        private static int GetMaxExecutionCount(Node<BlackboxTableModel> node)
        {
            var max = node.Model.ExecutionCount;

            foreach (var after in node.Afters)
                max = Math.Max(max, GetMaxExecutionCount(after));

            return max;
        }

        private static void CleanupResult(Node<BlackboxTableModel> result)
        {
            try
            {
                if (result != null)
                    CleanupNode(result);
                Assert.That(BlackboxHandle.ActiveSessionCount, Is.Zero, "A table project leaked an explicit session.");
            }
            finally
            {
                BlackboxHandle.DisposeActiveSessions();
                BlackboxHandle.RestartDefaultSession();
            }
        }

        private static void CleanupNode(Node<BlackboxTableModel> node)
        {
            foreach (var directory in node.Model.TempDirectories)
            {
                try
                {
                    var target = Path.GetFullPath(directory);
                    var parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (!string.Equals(Path.GetDirectoryName(target), parent, StringComparison.OrdinalIgnoreCase)
                        || !Path.GetFileName(target).StartsWith("BlackboxUniTest_", StringComparison.Ordinal))
                        throw new InvalidOperationException("Table cleanup escaped its owned temporary directory.");
                    if (Directory.Exists(target))
                        Directory.Delete(target, true);
                }
                catch
                {
                }
            }

            foreach (var after in node.Afters)
                CleanupNode(after);
        }

        private static void PrintResultJsonIfEnabled(string name, Node<BlackboxTableModel> result)
        {
            if (!PrintResultJson || result == null)
                return;

            TestContext.Out.WriteLine(name);
            TestContext.Out.WriteLine(result.GetReport().ToJson());
        }

    }
}
