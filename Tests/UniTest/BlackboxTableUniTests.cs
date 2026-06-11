#if BLACKBOX
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UniTest;

namespace com.BlackThunder.BlackboxSystem.Tests
{
    internal sealed class BlackboxTableUniTests
    {
        private const int ScenarioDepth = 10;
        private const int ContinuousScenarioDepth = 20;
        private const int ContinuousSeed = 20260611;

        [Test]
        public async Task TableProjectsExecuteTenScenarioSteps()
        {
            foreach (var project in CreateProjects(false))
            {
                var result = await project.Execute(ScenarioDepth + 1);
                AssertProjectSucceeded(project, result);
                Assert.That(result.GetLastNode().Model.ExecutionCount, Is.EqualTo(ScenarioDepth + 1), project.Name);
            }
        }

        [Test]
        public async Task TableProjectsExecuteContinuously()
        {
            foreach (var project in CreateProjects(true))
            {
                var result = await project.ExecuteContinuously(ContinuousScenarioDepth + 1, ContinuousSeed, false);
                Assert.That(GetMaxExecutionCount(result), Is.InRange(1, ContinuousScenarioDepth + 1), project.Name);
            }
        }

        private static IEnumerable<BlackboxTableProject> CreateProjects(bool continuous)
        {
            yield return Project("LogData", continuous,
                CreateScenario<LogDataTests>("ConstructorStoresValueMetadata", t => t.ConstructorStoresValueMetadata()),
                CreateScenario<LogDataTests>("ConstructorStoresTagMetadata", t => t.ConstructorStoresTagMetadata()),
                CreateScenario<LogDataTests>("ConstructorStoresInteractionMetadata", t => t.ConstructorStoresInteractionMetadata()),
                CreateScenario<LogDataTests>("MutableExportFieldsCanBeUpdated", t => t.MutableExportFieldsCanBeUpdated()),
                CreateScenario<LogDataTests>("ToStringUsesFormatter", t => t.ToStringUsesFormatter()));

            yield return Project("LogFormatter", continuous,
                CreateScenario<LogFormatterTests>("RenderMessageReplacesTagPlaceholders", t => t.RenderMessageReplacesTagPlaceholders()),
                CreateScenario<LogFormatterTests>("RenderMessageAppendsUnusedTags", t => t.RenderMessageAppendsUnusedTags()),
                CreateScenario<LogFormatterTests>("RenderTaggedMessageHonorsTargetTypes", t => t.RenderTaggedMessageHonorsTargetTypes()),
                CreateScenario<LogFormatterTests>("RenderTextLineIncludesSequenceThreadScopeAndInteraction", t => t.RenderTextLineIncludesSequenceThreadScopeAndInteraction()),
                CreateScenario<LogFormatterTests>("RenderMethodLabelDependsOnScopeType", t => t.RenderMethodLabelDependsOnScopeType()),
                CreateScenario<LogFormatterTests>("ArrowAndTagRefHandleInteractionIds", t => t.ArrowAndTagRefHandleInteractionIds()));

            yield return Project("WriteHandler", continuous,
                CreateScenario<BlackboxHandleTests>("WriteHandlerFormatsInterpolatedValuesForValidHandle", t => t.WriteHandlerFormatsInterpolatedValuesForValidHandle()),
                CreateScenario<BlackboxHandleTests>("WriteHandlerSkipsFormattingForInvalidHandle", t => t.WriteHandlerSkipsFormattingForInvalidHandle()));

            yield return Project("TagHandle", continuous,
                CreateScenario<TagHandleTests>("DefaultWithAndStringConversionDoNothing", t => t.DefaultWithAndStringConversionDoNothing()),
                CreateScenario<TagHandleTests>("FallbackMessageConvertsToOriginalMessage", t => t.FallbackMessageConvertsToOriginalMessage()),
                CreateScenario<TagHandleTests>("WithValidTargetAddsSourceAndTargetTags", t => t.WithValidTargetAddsSourceAndTargetTags()),
                CreateScenario<TagHandleTests>("WithNullTargetTagsNull", t => t.WithNullTargetTagsNull()),
                CreateScenario<TagHandleTests>("WithNullArrayTagsNull", t => t.WithNullArrayTagsNull()),
                CreateScenario<TagHandleTests>("WithTargetTypesLastOverridesTargetLogPolicy", t => t.WithTargetTypesLastOverridesTargetLogPolicy()));

            yield return Project("ScopeHandle", continuous,
                CreateScenario<ScopeHandleTests>("DefaultHandleOperationsDoNothing", t => t.DefaultHandleOperationsDoNothing()),
                CreateScenario<ScopeHandleTests>("AliveDisposeClosesScope", t => t.AliveDisposeClosesScope()),
                CreateScenario<ScopeHandleTests>("DisposedDisposeDoesNotAddLogAgain", t => t.DisposedDisposeDoesNotAddLogAgain()),
                CreateScenario<ScopeHandleTests>("WithAddsTagsToOpenLog", t => t.WithAddsTagsToOpenLog()),
                CreateScenario<ScopeHandleTests>("DifferentThreadDisposeWarns", t => t.DifferentThreadDisposeWarns()),
                CreateScenario<ScopeHandleTests>("PrintedDisposeDoesNotCloseScope", t => t.PrintedDisposeDoesNotCloseScope()));

            yield return Project("ExertHandle", continuous,
                CreateScenario<ExertHandleTests>("DefaultDisposeDoesNothing", t => t.DefaultDisposeDoesNothing()),
                CreateScenario<ExertHandleTests>("DisposeMergesAdjacentTargetScope", t => t.DisposeMergesAdjacentTargetScope()),
                CreateScenario<ExertHandleTests>("DisposeWithoutMergeTargetOnlyDisposes", t => t.DisposeWithoutMergeTargetOnlyDisposes()),
                CreateScenario<ExertHandleTests>("DisposedDisposeDoesNotMergeAgain", t => t.DisposedDisposeDoesNotMergeAgain()),
                CreateScenario<ExertHandleTests>("DifferentThreadDisposeWarns", t => t.DifferentThreadDisposeWarns()),
                CreateScenario<ExertHandleTests>("PrintedDisposeDoesNotMerge", t => t.PrintedDisposeDoesNotMerge()));

            yield return Project("BlackboxRuntime", continuous,
                CreateScenario<BlackboxRuntimeTests>("IdsIncreaseIndependently", t => t.IdsIncreaseIndependently()),
                CreateScenario<BlackboxRuntimeTests>("ResetRestartsCounters", t => t.ResetRestartsCounters()));

            yield return Project("BlackboxRegistry", continuous,
                CreateScenario<BlackboxRegistryTests>("GetBlackboxRejectsNull", t => t.GetBlackboxRejectsNull()),
                CreateScenario<BlackboxRegistryTests>("GetBlackboxCreatesAndReusesOwner", t => t.GetBlackboxCreatesAndReusesOwner()),
                CreateScenario<BlackboxRegistryTests>("ContainsAndCountTrackRegisteredOwners", t => t.ContainsAndCountTrackRegisteredOwners()),
                CreateScenario<BlackboxRegistryTests>("ForceResetClearsRegistryRuntimeAndHandles", t => t.ForceResetClearsRegistryRuntimeAndHandles()),
                CreateScenario<BlackboxRegistryTests>("StrongReferencePolicyKeepsOwnerReference", t => t.StrongReferencePolicyKeepsOwnerReference()),
                CreateScenario<BlackboxRegistryTests>("WeakReferencePolicyStoresWeakOwnerReference", t => t.WeakReferencePolicyStoresWeakOwnerReference()));

            yield return Project("Infrastructure", continuous,
                CreateScenario<InfrastructureTests>("ConfigureStoresSettings", t => t.ConfigureStoresSettings()),
                CreateScenario<InfrastructureTests>("ResolveUsesConfiguredDefaultsAndExplicitValues", t => t.ResolveUsesConfiguredDefaultsAndExplicitValues()),
                CreateScenario<InfrastructureTests>("LogDispatchesToMatchingLogger", t => t.LogDispatchesToMatchingLogger()),
                CreateScenario<InfrastructureTests>("TryMarkPrintedSucceedsOnceAndResetAllowsPrintingAgain", t => t.TryMarkPrintedSucceedsOnceAndResetAllowsPrintingAgain()));

            yield return Project("LogContext", continuous,
                CreateScenario<LogContextTests>("EnqueueLogStoresLogData", t => t.EnqueueLogStoresLogData()),
                CreateScenario<LogContextTests>("RingBufferKeepsRecentLogs", t => t.RingBufferKeepsRecentLogs()),
                CreateScenario<LogContextTests>("OpenAndCloseScopeStoresScopePair", t => t.OpenAndCloseScopeStoresScopePair()),
                CreateScenario<LogContextTests>("CloseMissingScopeWarns", t => t.CloseMissingScopeWarns()),
                CreateScenario<LogContextTests>("CloseOuterFirstAutoClosesNewerScopes", t => t.CloseOuterFirstAutoClosesNewerScopes()),
                CreateScenario<LogContextTests>("CloseFromDifferentThreadWarnsWithoutAutoClosingNewerScopes", t => t.CloseFromDifferentThreadWarnsWithoutAutoClosingNewerScopes()),
                CreateScenario<LogContextTests>("ResolveWithAddsSourceAndTargetTags", t => t.ResolveWithAddsSourceAndTargetTags()),
                CreateScenario<LogContextTests>("TryMergeScopeMergesAdjacentInteractionAndOpenScope", t => t.TryMergeScopeMergesAdjacentInteractionAndOpenScope()),
                CreateScenario<LogContextTests>("TryMergeScopeRejectsNonAdjacentOrMissingTarget", t => t.TryMergeScopeRejectsNonAdjacentOrMissingTarget()),
                CreateScenario<LogContextTests>("GetLogsStopsAtMaxSequence", t => t.GetLogsStopsAtMaxSequence()));

            yield return Project("Blackbox", continuous,
                CreateScenario<BlackboxTests>("WriteStoresSelfLog", t => t.WriteStoresSelfLog()),
                CreateScenario<BlackboxTests>("WriteScopeReturnsScopeHandleAndOpenLog", t => t.WriteScopeReturnsScopeHandleAndOpenLog()),
                CreateScenario<BlackboxTests>("ExertMessageRejectsNullTarget", t => t.ExertMessageRejectsNullTarget()),
                CreateScenario<BlackboxTests>("ExertMessageStoresSelfInteraction", t => t.ExertMessageStoresSelfInteraction()),
                CreateScenario<BlackboxTests>("ExertMessageStoresTwoSidedPeerInteraction", t => t.ExertMessageStoresTwoSidedPeerInteraction()),
                CreateScenario<BlackboxTests>("ExertReturnsHandleForPeerInteraction", t => t.ExertReturnsHandleForPeerInteraction()),
                CreateScenario<BlackboxTests>("PrintedStateStopsNewLogs", t => t.PrintedStateStopsNewLogs()),
                CreateScenario<BlackboxTests>("GetLogsSortsAcrossThreadContextsBySequence", t => t.GetLogsSortsAcrossThreadContextsBySequence()),
                CreateScenario<BlackboxTests>("GetLogsByContextReturnsSeparateThreadBuckets", t => t.GetLogsByContextReturnsSeparateThreadBuckets()),
                CreateScenario<BlackboxTests>("OwnerStringFallsBackWhenOwnerReferenceIsLost", t => t.OwnerStringFallsBackWhenOwnerReferenceIsLost()));

            yield return Project("BlackboxHandle", continuous,
                CreateScenario<BlackboxHandleTests>("OfCreatesValidHandleAndRejectsNull", t => t.OfCreatesValidHandleAndRejectsNull()),
                CreateScenario<BlackboxHandleTests>("InvalidHandleReturnsFallbacks", t => t.InvalidHandleReturnsFallbacks()),
                CreateScenario<BlackboxHandleTests>("ConstructWritesCtorScopeAndCachesHandle", t => t.ConstructWritesCtorScopeAndCachesHandle()),
                CreateScenario<BlackboxHandleTests>("WhenReturnsValidOrSkippedHandle", t => t.WhenReturnsValidOrSkippedHandle()),
                CreateScenario<BlackboxHandleTests>("DisposeWritesDisposedLog", t => t.DisposeWritesDisposedLog()),
                CreateScenario<BlackboxHandleTests>("WriteReturnsMessageAndStoresLog", t => t.WriteReturnsMessageAndStoresLog()),
                CreateScenario<BlackboxHandleTests>("WriteScopeReturnsAliveScopeHandle", t => t.WriteScopeReturnsAliveScopeHandle()),
                CreateScenario<BlackboxHandleTests>("ExertMessageRejectsNullAndStoresPeerInteraction", t => t.ExertMessageRejectsNullAndStoresPeerInteraction()),
                CreateScenario<BlackboxHandleTests>("ExertRejectsNullAndReturnsHandleForPeer", t => t.ExertRejectsNullAndReturnsHandleForPeer()),
                CreateScenario<BlackboxHandleTests>("WriteErrorRecordsErrorAndTargets", t => t.WriteErrorRecordsErrorAndTargets()),
                CreateScenario<BlackboxHandleTests>("WriteErrorCanTriggerCrashExport", t => t.WriteErrorCanTriggerCrashExport()),
                CreateScenario<BlackboxHandleTests>("CrashExportWritesStackTraceAndExportsOnce", t => t.CrashExportWritesStackTraceAndExportsOnce()),
                CreateScenario<BlackboxHandleTests>("ExportWarnsForInvalidOrDuplicateExport", t => t.ExportWarnsForInvalidOrDuplicateExport()),
                CreateScenario<BlackboxHandleTests>("ForceResetRestoresDefaultRuntimeState", t => t.ForceResetRestoresDefaultRuntimeState()));

            yield return Project("ExportTools", continuous,
                CreateScenario<ExportToolsTests>("BuildExportGraphBuildsRootOnly", t => t.BuildExportGraphBuildsRootOnly()),
                CreateScenario<ExportToolsTests>("BuildExportGraphIncludesFocusedIncomingPeers", t => t.BuildExportGraphIncludesFocusedIncomingPeers()),
                CreateScenario<ExportToolsTests>("BuildExportGraphIncludesFullOutgoingPeers", t => t.BuildExportGraphIncludesFullOutgoingPeers()),
                CreateScenario<ExportToolsTests>("BuildExportGraphRespectsDepthLimit", t => t.BuildExportGraphRespectsDepthLimit()),
                CreateScenario<ExportToolsTests>("FlattenStepsConvertsEmptyScopePair", t => t.FlattenStepsConvertsEmptyScopePair()),
                CreateScenario<ExportToolsTests>("FlattenStepsPreservesNonEmptyScope", t => t.FlattenStepsPreservesNonEmptyScope()),
                CreateScenario<ExportToolsTests>("ResolveScopeDepthsAssignsNestedDepths", t => t.ResolveScopeDepthsAssignsNestedDepths()),
                CreateScenario<ExportToolsTests>("TrimSmartSanitizesFileNameParts", t => t.TrimSmartSanitizesFileNameParts()));

            yield return Project("Exporter", continuous,
                CreateScenario<ExporterTests>("TxtExporterRejectsMissingLogDirectory", t => t.TxtExporterRejectsMissingLogDirectory()),
                CreateScenario<ExporterTests>("TxtExporterCreatesNormalAndCrashFiles", t => t.TxtExporterCreatesNormalAndCrashFiles()),
                CreateScenario<ExporterTests>("HtmlExporterRejectsMissingLogDirectory", t => t.HtmlExporterRejectsMissingLogDirectory()),
                CreateScenario<ExporterTests>("HtmlExporterCreatesNormalAndCrashFiles", t => t.HtmlExporterCreatesNormalAndCrashFiles()),
                CreateScenario<ExporterTests>("HtmlExporterWritesInteractionLinks", t => t.HtmlExporterWritesInteractionLinks()),
                CreateScenario<ExporterTests>("HtmlExporterWritesTagLinksWithoutInlineTagIds", t => t.HtmlExporterWritesTagLinksWithoutInlineTagIds()),
                CreateScenario<ExporterTests>("OpenLogFailureIsReportedAsWarning", t => t.OpenLogFailureIsReportedAsWarning()));

            yield return Project("Integration", continuous,
                CreateScenario<IntegrationTests>("SingleOwnerHistoryExportsReadableFile", t => t.SingleOwnerHistoryExportsReadableFile()),
                CreateScenario<IntegrationTests>("TwoOwnerInteractionExportsConnectedLogs", t => t.TwoOwnerInteractionExportsConnectedLogs()),
                CreateScenario<IntegrationTests>("TagTargetFlowWritesSourceAndTargetReferences", t => t.TagTargetFlowWritesSourceAndTargetReferences()),
                CreateScenario<IntegrationTests>("ErrorFlowExportsErrorAndCrashContext", t => t.ErrorFlowExportsErrorAndCrashContext()),
                CreateScenario<IntegrationTests>("ResetAndRerunStartsClean", t => t.ResetAndRerunStartsClean()),
                CreateScenario<IntegrationTests>("RingBufferLimitKeepsRecentLogsInPublicFlow", t => t.RingBufferLimitKeepsRecentLogsInPublicFlow()));
        }

        private static BlackboxTableProject Project(string name, bool continuous, params TableScenario[] scenarios)
        {
            return new BlackboxTableProject(name, scenarios, continuous);
        }

        private static TableScenario CreateScenario<TFixture>(string id, Action<TFixture> action) where TFixture : BlackboxTestBase
        {
            return new TableScenario(id, () =>
            {
                var fixture = (TFixture)Activator.CreateInstance(typeof(TFixture));
                fixture.SetUpBlackboxTest();

                try
                {
                    action(fixture);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Blackbox table scenario failed: " + id, ex);
                }
                finally
                {
                    fixture.TearDownBlackboxTest();
                }
            });
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

        private sealed class BlackboxTableProject : Project<BlackboxTableModel>
        {
            private readonly TableScenario[] _scenarios;
            private readonly bool _continuous;

            public string Name { get; private set; }

            public BlackboxTableProject(string name, TableScenario[] scenarios, bool continuous)
            {
                Name = name;
                _scenarios = scenarios;
                _continuous = continuous;
            }

            public override IEnumerable<ILab<BlackboxTableModel>> CreateLabs(BlackboxTableModel model)
            {
                if (model.Subject == null)
                {
                    yield return new Lab<BlackboxTableModel>(
                        "Ignite" + Name,
                        actor: (m, _) => m.Subject = Name);
                    yield break;
                }

                if (_continuous)
                {
                    foreach (var scenario in _scenarios)
                        yield return CreateLab(scenario);

                    yield break;
                }

                if (model.ExecutionCount <= ScenarioDepth)
                {
                    var scenario = _scenarios[(model.ExecutionCount - 1) % _scenarios.Length];
                    yield return CreateLab(scenario);
                }
            }

            private Lab<BlackboxTableModel> CreateLab(TableScenario scenario)
            {
                return new Lab<BlackboxTableModel>(
                    Name + "_" + scenario.ID,
                    actor: (_, __) => scenario.Execute());
            }
        }

        private sealed class BlackboxTableModel : Model
        {
        }

        private sealed class TableScenario
        {
            public readonly string ID;
            public readonly Action Execute;

            public TableScenario(string id, Action execute)
            {
                ID = id;
                Execute = execute;
            }
        }
    }
}
#endif
