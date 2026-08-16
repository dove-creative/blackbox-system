using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    internal sealed class InfrastructureTests : BlackboxTestBase
    {
        [Test]
        public void ConfigureStoresSettings()
        {
            var directory = BlackboxTestDoubles.CreateTempDirectory();

            // Table 3-3 / Ready x Configure.Set
            BlackboxHandle.Configure(
                directory,
                _ => { },
                _ => { },
                true,
                ExportFormat.Html,
                FullExportOption.Focused,
                OpenLogOption.Open,
                ExceptionHandlingOption.CrashExport,
                TargetTypes.Name,
                UseBlackboxOption.DoNotUse);

            Assert.That(Infrastructure.LogDirectory, Is.EqualTo(directory));
            Assert.That(Infrastructure.StrongReference, Is.True);
            Assert.That(Infrastructure.UseBlackbox, Is.EqualTo(UseBlackboxOption.DoNotUse));
            Assert.That(Infrastructure.ExportFormat, Is.EqualTo(ExportFormat.Html));
            Assert.That(Infrastructure.FullExportOption, Is.EqualTo(FullExportOption.Focused));
            Assert.That(Infrastructure.OpenLogOption, Is.EqualTo(OpenLogOption.Open));
            Assert.That(Infrastructure.ExceptionHandlingOption, Is.EqualTo(ExceptionHandlingOption.CrashExport));
            Assert.That(Infrastructure.TagTargetTypes, Is.EqualTo(TargetTypes.Name));
        }

        [Test]
        public void ResolveUsesConfiguredDefaultsAndExplicitValues()
        {
            BlackboxHandle.ExportFormat = ExportFormat.Html;
            BlackboxHandle.FullExportOption = FullExportOption.Focused;
            BlackboxHandle.OpenLogOption = OpenLogOption.Never;
            BlackboxHandle.TagTargetTypes = TargetTypes.Name;

            // Table 3-3 / Ready x Resolve
            Assert.That(ExportFormat.BasedOnSettings.Resolve(), Is.EqualTo(ExportFormat.Html));
            Assert.That(FullExportOption.BasedOnSettings.Resolve(), Is.EqualTo(FullExportOption.Focused));
            Assert.That(OpenLogOption.BasedOnSettings.Resolve(), Is.EqualTo(OpenLogOption.Never));
            Assert.That(TargetTypes.BasedOnSettings.Resolve(), Is.EqualTo(TargetTypes.Name));

            // Table 3-3 / Ready x Resolve
            Assert.That(ExportFormat.Txt.Resolve(), Is.EqualTo(ExportFormat.Txt));
            Assert.That(FullExportOption.Full.Resolve(), Is.EqualTo(FullExportOption.Full));
            Assert.That(OpenLogOption.Open.Resolve(), Is.EqualTo(OpenLogOption.Open));
            Assert.That(TargetTypes.Message.Resolve(), Is.EqualTo(TargetTypes.Message));
        }

        [Test]
        public void LogDispatchesToMatchingLogger()
        {
            // Table 3-3 / Ready x Log
            Assert.That(Infrastructure.Log("normal"), Is.True);
            Assert.That(Infrastructure.Log("warning", LogLevel.Warning), Is.True);

            Assert.That(BlackboxTestDoubles.NormalLogs, Does.Contain("normal"));
            Assert.That(BlackboxTestDoubles.WarningLogs, Does.Contain("warning"));
        }

        [Test]
        public void TryMarkPrintedSucceedsOnceAndResetAllowsPrintingAgain()
        {
            // Table 3-3 / Ready x TryMarkPrinted
            Assert.That(Infrastructure.TryMarkPrinted(), Is.True);
            Assert.That(Infrastructure.IsPrinted, Is.True);

            // Table 3-3 / Printed x TryMarkPrinted
            Assert.That(Infrastructure.TryMarkPrinted(), Is.False);

            // Table 3-3 / Printed x ForceResetRuntimeState
            Infrastructure.ForceResetRuntimeState();
            Assert.That(Infrastructure.IsPrinted, Is.False);
            Assert.That(Infrastructure.TryMarkPrinted(), Is.True);
        }
    }
}
