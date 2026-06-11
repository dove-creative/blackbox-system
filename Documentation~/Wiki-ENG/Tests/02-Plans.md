# Blackbox Test Plan

Blackbox is a POCO-centered recording framework, so tests keep an asmdef that can run on Unity Test Runner while being written as pure NUnit tests as much as possible.

---

# 1. Test Composition Principles

## 1-1. Execution Method

- Write test code in the `Tests` folder.
- Keep the existing `com.BlackThunder.BlackboxSystem.Tests.asmdef`.
- The `com.BlackThunder.BlackboxSystem.Tests` assembly references the `com.BlackThunder.BlackboxSystem` assembly, and verifies internal types through `InternalsVisibleTo("com.BlackThunder.BlackboxSystem.Tests")` in `AssemblyInfo.cs`.
- Unity Test Runner and the `com.BlackThunder.BlackboxSystem.Tests.asmdef` axis default to `UNITY_INCLUDE_TESTS`, `BLACKBOX`, and `BLACKBOX_TESTS` being enabled.
- The pure NUnit external execution axis uses `Tools/ExternalNUnitExecutor/ExternalNUnitExecutor.csproj`. This axis also enables `BLACKBOX` and verifies the real recording/output flow of Blackbox.
- Fallback behavior when the `BLACKBOX` symbol is off is verified in a separate no-Blackbox axis such as `Tools/ExternalNUnitExecutor/ExternalNUnitExecutor.NoBlackbox.csproj`. Because `com.BlackThunder.BlackboxSystem.Tests.asmdef` has a `BLACKBOX` define constraint, do not try to verify `SymbolOff` rows on the Unity asmdef axis.

## 1-2. Writing Rules

- Do not put Blackbox instrumentation calls into test code that verifies Blackbox itself.
- Prefer the `Assert.That(actual, constraint)` form for new asserts.
- Right before each test function or independent assert section, add a short English comment indicating which table and behavior from `01-Tables.md` it verifies.
- `X` cells in `01-Tables.md` are logically impossible combinations and are excluded from test targets.
- Every result cell except `X` is included in tests. `-`, `^`, exceptions, state changes, unique behavior, and internal behavior cells all correspond to a test function or an independent assert section within a test.
- Public API tests are written around `BlackboxHandle`; directly call `Blackbox`, `LogContext`, `LogData`, or export tools only when internal structure verification is needed.
- Verify interpolated string handler paths first through public API calls. Parts that are hard to observe with public calls alone, such as handler short-circuit state, are verified by directly constructing `BlackboxHandle.WriteHandler`.
- File output tests use temporary folders. In normal export tests, automatic opening is disabled with `OpenLogOption.Never`. The dedicated `OpenLog` cells verify the failure-warning path without launching an external process by using a test-build hook.

## 1-3. Expected Scale

Even if all non-`X` result cells in `01-Tables.md` are expanded, the expected size is about 95-115 test functions and 120-170 assert sections. Since this is below 200, tests are written against the entire table.

## 1-4. Cell Coverage Criteria

The test plan and test code use the following criteria.

| Notation | Included in test plan | Criterion |
| --- | --- | --- |
| `X` | Excluded | A state/behavior combination that cannot logically exist. |
| `-` | Included | Verify that doing nothing or preserving the existing value is intentional behavior. |
| `^` | Included | Verify that the same result as the cell above is preserved. It can be a separate assert section in the same test. |
| _Exception_ | Included | Verify the specified exception type and absence of side effects. |
| **State change** | Included | Verify the state change and its observed value. |
| `[Behavior]` | Included | Verify the result and side effects of the unique behavior. |
| `<Behavior>` | Included | Verify that internally continued behavior actually runs. |

When writing test code, add table positions to test comments whenever possible, such as `Table 2-2 / Alive x Dispose.DifferentThread`, so missing non-`X` cells can be checked again.

---

# 2. Shared Test Tools

## 2-1. `BlackboxTestDoubles.cs`

This file contains shared test fixtures and helpers.

| Tool | Role |
| --- | --- |
| `NamedOwner` | Owner object with stable `ToString()` |
| `Reset()` | Reset `BlackboxHandle.ForceReset()`, loggers, and settings |
| `Owner(name)` | Create a new `NamedOwner` |
| `Blackbox(name)` | Create the internal `Blackbox` corresponding to an owner |
| `BlackboxFor(owner)` | Create the internal `Blackbox` corresponding to an already-created owner |
| `Logs(blackbox)` | Collect `blackbox.GetLogs()` as a list |
| `Lines(blackbox)` | Render `blackbox.GetLogs()` results as a string list |
| `CreateTempDirectory()` | Create a temporary folder for export tests |
| `GetFiles(directory, pattern)` | Search output files |
| `CleanupTempDirectories()` | Delete temporary folders created during tests |
| `ForceFullCollection()` | Force GC for weak reference policy verification |
| `NormalLogs` / `WarningLogs` | Store logger call results |

This file is a test helper, so it does not include `[Test]` methods.

---

# 3. Unit Test Plan

## 3-1. `LogDataTests.cs`

Corresponding table: `1-1. LogData`

| Test | Verification |
| --- | --- |
| `ConstructorStoresValueMetadata` | Stores owner, message, method, scope, sequence, and thread metadata when creating Value state. |
| `ConstructorStoresTagMetadata` | Stores `Tags`, `TaggedBy`, and `TagTargetTypes` when creating Tagged state. |
| `ConstructorStoresInteractionMetadata` | Stores `InteractionId`, `ExertedBy`, and `ExertingTo` when creating Interaction state. |
| `MutableExportFieldsCanBeUpdated` | `ScopeDepth`, `IsValid`, `Message`, and interaction information are updated during export/merge. |
| `ToStringUsesFormatter` | `ToString()` produces the result of the `LogFormatter.RenderTextLine(...)` path. |

## 3-2. `LogFormatterTests.cs`

Corresponding table: `1-2. LogFormatter`

| Test | Verification |
| --- | --- |
| `RenderMessageReplacesTagPlaceholders` | `%0` and `%1` placeholders are replaced with target references. |
| `RenderMessageAppendsUnusedTags` | Tags not used in the message are appended. |
| `RenderTaggedMessageHonorsTargetTypes` | Target log message detail changes according to `TargetTypes` combinations. |
| `RenderTextLineIncludesSequenceThreadScopeAndInteraction` | One-line text includes sequence, thread, scope, and interaction information. |
| `RenderMethodLabelDependsOnScopeType` | Open/close/step/normal labels render according to scope type. |
| `ArrowAndTagRefHandleInteractionIds` | Arrow and tag-ref expressions differ depending on whether an interaction id exists. |

## 3-3. `TagHandleTests.cs`

Corresponding table: `2-1. TagHandle`

| Test | Verification |
| --- | --- |
| `DefaultWithAndStringConversionDoNothing` | `With(...)` and string conversion on a default handle create no record and return an empty string. |
| `FallbackMessageConvertsToOriginalMessage` | String conversion on a fallback handle returns the original message. |
| `WithValidTargetAddsSourceAndTargetTags` | Tag information is reflected in both source and target logs. |
| `WithNullTargetTagsNull` | `With(null)` renders as a null target. |
| `WithNullArrayTagsNull` | `With((object[])null)` is also normalized as a null target. |
| `WithTargetTypesLastOverridesTargetLogPolicy` | A final `TargetTypes` argument is used as the target log display policy. |

## 3-4. `ScopeHandleTests.cs`

Corresponding table: `2-2. ScopeHandle`

| Test | Verification |
| --- | --- |
| `DefaultHandleOperationsDoNothing` | `With(...)` and dispose on a default handle create no logs and remain disposed. |
| `AliveDisposeClosesScope` | Disposing an alive handle adds a close log and changes it to disposed state. |
| `DisposedDisposeDoesNotAddLogAgain` | Duplicate dispose does not add another close log. |
| `WithAddsTagsToOpenLog` | `With(...)` attaches a target to the scope-open log and keeps the same handle. |
| `DifferentThreadDisposeWarns` | Disposing from a thread different from the creation thread leaves a warning. |
| `PrintedDisposeDoesNotCloseScope` | Dispose after export does not add a close log. |

## 3-5. `ExertHandleTests.cs`

Corresponding table: `2-3. ExertHandle`

| Test | Verification |
| --- | --- |
| `DefaultDisposeDoesNothing` | Disposing a default exert handle does nothing. |
| `DisposeMergesAdjacentTargetScope` | Merges when an open scope exists immediately after the interaction. |
| `DisposeWithoutMergeTargetOnlyDisposes` | If there is no merge target, logs are unchanged and only disposed state changes. |
| `DisposedDisposeDoesNotMergeAgain` | Duplicate dispose does not repeat the merge. |
| `DifferentThreadDisposeWarns` | Dispose from another thread leaves a warning. |
| `PrintedDisposeDoesNotMerge` | Dispose after export does not merge. |

## 3-6. `BlackboxRuntimeTests.cs`

Corresponding table: `3-1. BlackboxRuntime`

| Test | Verification |
| --- | --- |
| `IdsIncreaseIndependently` | Blackbox id, interaction id, and sequence increase independently. |
| `ResetRestartsCounters` | After reset, each counter starts again from its initial value. |

## 3-7. `BlackboxRegistryTests.cs`

Corresponding table: `3-2. BlackboxRegistry`

| Test | Verification |
| --- | --- |
| `GetBlackboxRejectsNull` | Null subject throws `ArgumentNullException`. |
| `GetBlackboxCreatesAndReusesOwner` | The same owner reuses the same `Blackbox`. |
| `ContainsAndCountTrackRegisteredOwners` | `Contains(...)` and registered owner count change together before/after registration. |
| `ForceResetClearsRegistryRuntimeAndHandles` | Reset clears registry, runtime, and handle state. |
| `StrongReferencePolicyKeepsOwnerReference` | Strong reference setting keeps the owner strongly. |
| `WeakReferencePolicyStoresWeakOwnerReference` | With weak reference settings, the owner can disappear while fallback owner string is preserved. |

## 3-8. `InfrastructureTests.cs`

Corresponding table: `3-3. Infrastructure`

| Test | Verification |
| --- | --- |
| `ConfigureStoresSettings` | `Configure(...)` stores logger and export settings. |
| `ResolveUsesConfiguredDefaultsAndExplicitValues` | `BasedOnSettings` values resolve to current settings, and explicit values are not overwritten by settings. |
| `LogDispatchesToMatchingLogger` | Normal and warning loggers are called separately. |
| `TryMarkPrintedSucceedsOnceAndResetAllowsPrintingAgain` | Export-completed state succeeds only once, and reset allows output state again. |

## 3-9. `LogContextTests.cs`

Corresponding table: `4-1. LogContext`

| Test | Verification |
| --- | --- |
| `EnqueueLogStoresLogData` | Normal logs are stored as `LogData`. |
| `RingBufferKeepsRecentLogs` | When buffer size is exceeded, only recent logs are kept. |
| `OpenAndCloseScopeStoresScopePair` | Open/close logs are stored with the same scope id. |
| `CloseMissingScopeWarns` | Closing an unopened scope leaves a warning. |
| `CloseOuterFirstAutoClosesNewerScopes` | Closing outer first on the same thread automatically closes newer scopes. |
| `CloseFromDifferentThreadWarnsWithoutAutoClosingNewerScopes` | Cross-thread close leaves only a warning and does not automatically close newer scopes. |
| `ResolveWithAddsSourceAndTargetTags` | Tag information is reflected in source and target logs. |
| `TryMergeScopeMergesAdjacentInteractionAndOpenScope` | Adjacent interaction/open scope is merged. |
| `TryMergeScopeRejectsNonAdjacentOrMissingTarget` | Existing logs remain when there is no merge target. |
| `GetLogsStopsAtMaxSequence` | Logs after `maxSequence` are not returned. |

## 3-10. `BlackboxTests.cs`

Corresponding table: `4-2. Blackbox`

| Test | Verification |
| --- | --- |
| `WriteStoresSelfLog` | `Write(...)` stores an owner log. |
| `ScopeReturnsScopeHandleAndOpenLog` | `Scope(...)` creates an open log and handle. |
| `ExertMessageRejectsNullTarget` | Null target throws `ArgumentNullException`. |
| `ExertMessageStoresSelfInteraction` | Self-target interaction is stored in one log. |
| `ExertMessageStoresTwoSidedPeerInteraction` | Peer interaction is stored on both owners. |
| `ExertReturnsHandleForPeerInteraction` | `Exert(...)` stores an interaction and returns a handle. |
| `PrintedStateStopsNewLogs` | No new logs are added in printed state. |
| `GetLogsSortsAcrossThreadContextsBySequence` | Logs from multiple contexts are sorted by sequence. |
| `GetLogsByContextReturnsSeparateThreadBuckets` | Thread-specific context log groups are returned separately. |
| `OwnerStringFallsBackWhenOwnerReferenceIsLost` | Fallback owner string is used when a weak owner disappears. |

## 3-11. `BlackboxHandleTests.cs`

Corresponding table: `5-1. BlackboxHandle`, `1-3. BlackboxHandle.WriteHandler`

| Test | Verification |
| --- | --- |
| `OfCreatesValidHandleAndRejectsNull` | Valid subject creates a handle and null throws an exception. |
| `InvalidHandleReturnsFallbacks` | Default/invalid handle creates no log and returns a message or disposed handle. |
| `ConstructWritesCtorScopeAndCachesHandle` | Creates a construction scope and cached handle. |
| `WhenReturnsValidOrSkippedHandle` | Returns valid/default handle depending on condition. |
| `DisposeWritesDisposedLog` | Dispose API leaves a disposed message. |
| `WriteReturnsMessageAndStoresLog` | Write call preserves the original message and stores a log. |
| `ScopeReturnsAliveScopeHandle` | Scope API returns an alive handle. |
| `ExertMessageRejectsNullAndStoresPeerInteraction` | Verifies null and peer conditions separately. |
| `ExertRejectsNullAndReturnsHandleForPeer` | Verifies null and peer conditions separately. |
| `WriteErrorRecordsErrorAndTargets` | Stores an error log and target tags. |
| `WriteErrorCanTriggerCrashExport` | `WriteError` can lead to crash export depending on settings. |
| `CrashExportWritesStackTraceAndExportsOnce` | Creates a stack trace log and crash file. |
| `ExportWarnsForInvalidOrDuplicateExport` | Verifies invalid handle and duplicate export warnings. |
| `ForceResetRestoresDefaultRuntimeState` | After reset, a new execution can start. |
| `WriteHandlerFormatsInterpolatedValuesForValidHandle` | With a valid handle, interpolated string literal, formatted value, and null value are assembled into a message. |
| `WriteHandlerSkipsFormattingForInvalidHandle` | With an invalid handle, the handler enters skipped state and omits string assembly and log storage. |
| `SymbolOffReturnsInvalidHandlesAndOriginalMessages` | On the axis without the `BLACKBOX` symbol, public API returns invalid/default handles and original messages. |

## 3-12. `ExportToolsTests.cs`

Corresponding table: `6-1. export tools`

| Test | Verification |
| --- | --- |
| `BuildExportGraphBuildsRootOnly` | Creates only the root node when there is no connection. |
| `BuildExportGraphIncludesFocusedIncomingPeers` | Focused export includes only required peers. |
| `BuildExportGraphIncludesFullOutgoingPeers` | Full export includes outgoing peers too. |
| `BuildExportGraphRespectsDepthLimit` | Respects recursion depth limit. |
| `FlattenStepsConvertsEmptyScopePair` | Folds an empty open/close scope into a step. |
| `FlattenStepsPreservesNonEmptyScope` | Keeps a scope that has internal logs. |
| `ResolveScopeDepthsAssignsNestedDepths` | Calculates nested scope depth. |
| `TrimSmartSanitizesFileNameParts` | Normalizes empty or file-name-invalid input to `null`, and shortens long input while preserving the front/back. |

## 3-13. `ExporterTests.cs`

Corresponding table: `6-2. exporters`

| Test | Verification |
| --- | --- |
| `TxtExporterRejectsMissingLogDirectory` | Empty path throws an exception in txt export. |
| `TxtExporterCreatesNormalAndCrashFiles` | Creates normal/crash txt files. |
| `HtmlExporterRejectsMissingLogDirectory` | Empty path throws an exception in html export. |
| `HtmlExporterCreatesNormalAndCrashFiles` | Creates normal/crash html files. |
| `HtmlExporterWritesInteractionLinks` | HTML export includes interaction anchors/links. |
| `OpenLogFailureIsReportedAsWarning` | Automatic open failure is reported as a warning. |

---

# 4. Integration Test Plan

## 4-1. `IntegrationTests.cs`

Corresponding table: `7. Integration Flow`

| Test | Verification |
| --- | --- |
| `SingleOwnerHistoryExportsReadableFile` | Verifies one owner's construction, scope, write, and export flow in a file. |
| `TwoOwnerInteractionExportsConnectedLogs` | Source and target interactions are connected in export. |
| `TagTargetFlowWritesSourceAndTargetReferences` | `.With(...)` creates source/target displays on both sides. |
| `ErrorFlowExportsErrorAndCrashContext` | Error/crash flow leads to error log and file output. |
| `ResetAndRerunStartsClean` | After export, reset and record a new run independently. |
| `RingBufferLimitKeepsRecentLogsInPublicFlow` | Buffer limits keep recent logs through public API flow. |
