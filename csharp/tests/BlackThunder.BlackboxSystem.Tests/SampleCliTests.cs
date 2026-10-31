using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    [NonParallelizable]
    internal class SampleCliTests
    {
        private const string SampleAssemblyName = "BlackThunder.BlackboxSystem.Samples.dll";
        private const string TemporaryDirectoryPrefix = "BlackboxSampleCli_";
        private string _applicationDirectory;


        [SetUp]
        public void CopySampleApplication()
        {
            var sourceDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, "SampleApp");
            Assert.That(File.Exists(Path.Combine(sourceDirectory, SampleAssemblyName)), Is.True,
                "Build the test project so its SampleApp output is available.");

            _applicationDirectory = Path.Combine(Path.GetTempPath(), TemporaryDirectoryPrefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_applicationDirectory);
            foreach (var sourcePath in Directory.GetFiles(sourceDirectory))
                File.Copy(sourcePath, Path.Combine(_applicationDirectory, Path.GetFileName(sourcePath)));
        }

        [TearDown]
        public void DeleteSampleApplication()
        {
            if (_applicationDirectory == null || !Directory.Exists(_applicationDirectory)) return;

            var applicationPath = Path.GetFullPath(_applicationDirectory);
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!applicationPath.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(applicationPath).StartsWith(TemporaryDirectoryPrefix, StringComparison.Ordinal))
                throw new InvalidOperationException("The sample cleanup path is outside its temporary directory.");

            Directory.Delete(applicationPath, true);
            _applicationDirectory = null;
        }

        [TestCase("help")]
        [TestCase("exit")]
        [RunWithUniTest]
        public void InformationCommandsExitWithoutReadingInputOrCreatingOutput(string command)
        {
            // Sample CLI / One-shot x Help/Exit: stdin stays open to detect an accidental prompt loop.
            var result = RunSample(new[] { command });

            Assert.That(result.ExitCode, Is.Zero);
            Assert.That(result.Error, Is.Empty);
            Assert.That(result.Output, Does.Not.Contain("sample>"));
            Assert.That(Directory.Exists(Path.Combine(_applicationDirectory, "SampleOutput")), Is.False);
            if (command == "help") Assert.That(result.Output, Does.Contain("write").And.Contain("exception"));
        }

        [TestCase("write", "Write")]
        [TestCase("1", "Write")]
        [TestCase("exert", "Exert")]
        [TestCase("2", "Exert")]
        [TestCase("tag", "Tag")]
        [TestCase("3", "Tag")]
        [TestCase("exception", "Exception")]
        [TestCase("4", "Exception")]
        [RunWithUniTest]
        public void ScenariosAndNumericAliasesExportAndExitWithInputStillOpen(string command, string scenarioName)
        {
            // Sample CLI / One-shot x Scenario: production binaries run in an isolated output directory.
            var result = RunSample(new[] { command });
            var outputDirectory = Path.Combine(_applicationDirectory, "SampleOutput", scenarioName);

            Assert.That(result.ExitCode, Is.Zero, result.Error);
            Assert.That(result.Output, Does.Not.Contain("sample>").And.Not.Contain("[HtmlExporter]").And.Not.Contain("HP Change"));
            Assert.That(result.Output, Does.Contain("Completed").And.Contain(outputDirectory));
            Assert.That(Directory.Exists(outputDirectory), Is.True);

            var files = Directory.GetFiles(outputDirectory, "*.html");
            Assert.That(files, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(files[0]), Does.Contain("<html"));
            if (scenarioName == "Exception")
            {
                Assert.That(result.Output, Does.Contain("Expected exception: Potion cannot be null."));
                Assert.That(Path.GetFileName(files[0]), Does.StartWith("[CRASH]"));
                Assert.That(File.ReadAllText(files[0]), Does.Contain("Potion cannot be null."));
            }
        }

        [TestCase("unknown", null)]
        [TestCase("write", "extra")]
        [TestCase("help", "extra")]
        [TestCase("exit", "extra")]
        [RunWithUniTest]
        public void InvalidCommandsReturnUsageErrorWithoutRunningAScenario(string command, string extraArgument)
        {
            // Sample CLI / One-shot x InvalidInput: reject ignored arguments and report errors on stderr.
            var args = extraArgument == null ? new[] { command } : new[] { command, extraArgument };
            var result = RunSample(args);

            Assert.That(result.ExitCode, Is.EqualTo(2));
            Assert.That(result.Output, Is.Empty);
            Assert.That(result.Error, Does.Contain("Commands:"));
            Assert.That(Directory.Exists(Path.Combine(_applicationDirectory, "SampleOutput")), Is.False);
        }

        [Test]
        [RunWithUniTest]
        public void InteractiveModeContinuesAfterEmptyInputAndInvalidCommands()
        {
            // Sample CLI / Interactive x Empty/Invalid/Help/Exit: failed commands do not close the prompt.
            var result = RunSample(Array.Empty<string>(), "\nunknown\nhelp\nexit\n");

            Assert.That(result.ExitCode, Is.Zero);
            Assert.That(result.Error, Does.Contain("Unknown command: unknown"));
            Assert.That(result.Output, Does.Contain("sample>").And.Contain("Commands:"));
            Assert.That(result.Output.Split(new[] { "sample>" }, StringSplitOptions.None).Length, Is.EqualTo(5));
            Assert.That(Directory.Exists(Path.Combine(_applicationDirectory, "SampleOutput")), Is.False);
        }

        [Test]
        [RunWithUniTest]
        public void UnexpectedOutputFailureReturnsFailureAndDoesNotReportSuccess()
        {
            // Sample CLI / One-shot x OutputFailure: a file blocks directory creation before the scenario runs.
            File.WriteAllText(Path.Combine(_applicationDirectory, "SampleOutput"), "blocked output directory");
            var result = RunSample(new[] { "write" });

            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.Error, Does.Contain("Failed to run 'write'"));
            Assert.That(result.Output, Does.Not.Contain("Completed").And.Not.Contain("sample>"));
        }

        [Test]
        [RunWithUniTest]
        public void InteractiveModeContinuesAfterUnexpectedOutputFailure()
        {
            // Sample CLI / Interactive x OutputFailure: the failed command leaves help and exit available.
            File.WriteAllText(Path.Combine(_applicationDirectory, "SampleOutput"), "blocked output directory");
            var result = RunSample(Array.Empty<string>(), "write\nhelp\nexit\n");

            Assert.That(result.ExitCode, Is.Zero);
            Assert.That(result.Error, Does.Contain("Failed to run 'write'"));
            Assert.That(result.Output, Does.Contain("Commands:").And.Not.Contain("Completed"));
            Assert.That(result.Output.Split(new[] { "sample>" }, StringSplitOptions.None).Length, Is.EqualTo(4));
        }

        private ProcessResult RunSample(string[] args, string interactiveInput = null)
        {
            var startInfo = new ProcessStartInfo(FindDotnetHost())
            {
                WorkingDirectory = _applicationDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add(Path.Combine(_applicationDirectory, SampleAssemblyName));
            foreach (var argument in args) startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            try
            {
                if (interactiveInput != null)
                {
                    process.StandardInput.Write(interactiveInput);
                    process.StandardInput.Flush();
                }

                // Do not close stdin: one-shot commands must terminate without waiting for EOF.
                Assert.That(process.WaitForExit(15000), Is.True, "The sample did not exit while stdin remained open.");
                return new ProcessResult(process.ExitCode, outputTask.GetAwaiter().GetResult(), errorTask.GetAwaiter().GetResult());
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                    process.WaitForExit(5000);
                }
            }
        }

        private static string FindDotnetHost()
        {
            var configuredHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            if (!string.IsNullOrEmpty(configuredHost) && File.Exists(configuredHost)) return configuredHost;

            var processPath = Environment.ProcessPath;
            var executableName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
            if (!string.IsNullOrEmpty(processPath)
                && string.Equals(Path.GetFileName(processPath), executableName, StringComparison.OrdinalIgnoreCase))
                return processPath;

            // Resolve the host that owns the current .NET 10 runtime, including a portable SDK installation.
            var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
            var runtimeHost = Path.GetFullPath(Path.Combine(runtimeDirectory, "..", "..", "..", executableName));
            if (File.Exists(runtimeHost)) return runtimeHost;

            throw new InvalidOperationException("Cannot locate the .NET host used by the current test runtime.");
        }

        private readonly struct ProcessResult
        {
            public int ExitCode { get; }
            public string Output { get; }
            public string Error { get; }


            public ProcessResult(int exitCode, string output, string error)
            {
                ExitCode = exitCode;
                Output = output;
                Error = error;
            }
        }
    }
}
