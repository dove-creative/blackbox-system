using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UniTest;

namespace BlackThunder.BlackboxSystem.Tests
{
    internal sealed class BlackboxTableUniTests
    {
        private const int ScenarioDepth = 10;
        private const int ContinuousScenarioDepth = 30;
        // Toggle this off when the full UniTest node XML makes the test output too noisy.
        private const bool PrintResultXml = false;

        [TestCaseSource(typeof(BlackboxTableScenarioFactory), nameof(BlackboxTableScenarioFactory.TableProjectCases))]
        public async Task TableProjectsExecute(string projectName)
        {
            Node<BlackboxTableModel> result = null;

            try
            {
                var project = BlackboxTableScenarioFactory.CreateProject(projectName, false);
                result = await project.Execute(ScenarioDepth + 1);

                PrintResultXmlIfEnabled(project.Name, result);
                AssertProjectSucceeded(project, result);
                Assert.That(GetMaxExecutionCount(result), Is.InRange(1, ScenarioDepth + 1), project.Name);
            }
            finally
            {
                CleanupResult(result);
            }
        }

        // Continuous cases still use seeded random paths, but the project picks one candidate per step
        // because Blackbox runtime/logger state is global.
        [TestCaseSource(typeof(BlackboxTableScenarioFactory), nameof(BlackboxTableScenarioFactory.ContinuousTableProjectCases))]
        public async Task TableProjectsExecuteContinuously(string projectName, int seed)
        {
            Node<BlackboxTableModel> result = null;

            try
            {
                var project = BlackboxTableScenarioFactory.CreateProject(projectName, true, seed);
                result = await project.ExecuteContinuously(ContinuousScenarioDepth + 1, seed);

                PrintResultXmlIfEnabled(project.Name + " Seed " + seed, result);
                AssertProjectSucceeded(project, result);
                Assert.That(GetMaxExecutionCount(result), Is.InRange(1, ContinuousScenarioDepth + 1), project.Name);
            }
            finally
            {
                CleanupResult(result);
            }
        }

        private static void AssertProjectSucceeded(BlackboxTableProject project, Node<BlackboxTableModel> result)
        {
            bool cancelled;
            Assert.That(result.AllSucceed(out cancelled), Is.True, project.Name + Environment.NewLine + result.GetFailedReports().OuterXml);
            Assert.That(cancelled, Is.False, project.Name);
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
            if (result != null)
                CleanupNode(result);

            BlackboxHandle.ForceReset();
        }

        private static void CleanupNode(Node<BlackboxTableModel> node)
        {
            foreach (var directory in node.Model.TempDirectories)
            {
                try
                {
                    if (Directory.Exists(directory))
                        Directory.Delete(directory, true);
                }
                catch
                {
                }
            }

            foreach (var after in node.Afters)
                CleanupNode(after);
        }

        private static void PrintResultXmlIfEnabled(string name, Node<BlackboxTableModel> result)
        {
            if (!PrintResultXml || result == null)
                return;

            TestContext.Out.WriteLine(name);
            TestContext.Out.WriteLine(result.Report.OuterXml);
        }

    }
}
