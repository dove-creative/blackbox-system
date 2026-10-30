# Blackbox 테스트 계획

> 세션 수명 기준: RestartDefaultSession은 BlackboxHandle의 공개 API이며 공유 기본 세션만 교체한다. Runtime/Registry/HandleManager의 Reset API는 제공하지 않는다. 기본 설정은 유지하고 키·기록 상태를 새로 시작하며, 명시적 세션은 Dispose 후 Begin으로 교체한다. 일반 Export는 반복 가능하고 CrashExport만 소속 세션을 동결한다. 자세한 계약은 [세션 문서](../Wiki/05-Sessions.md)를 따른다.

Blackbox는 POCO 중심의 기록 프레임워크이므로, 테스트는 .NET에서 순수 NUnit 테스트로 실행한다.

## 1. 테스트 구성 원칙

### 1-1. 실행 방식

- 테스트 코드는 `tests/BlackThunder.BlackboxSystem.Tests`에 작성한다.
- .NET 10 SDK와 저장소 공통 C# 9 설정을 사용한다. 기본 핸들 구성과 `-p:BlackboxLockHandles=true` 구성을 검증한다.
- 저장소 루트에서 `dotnet test tests/BlackThunder.BlackboxSystem.Tests/BlackThunder.BlackboxSystem.Tests.csproj`를 실행한다.
- 테스트 프로젝트는 exporter hook을 위해 Blackbox 런타임 소스를 링크하고 `BLACKBOX_TESTS`로 컴파일하여 internal 타입도 검증한다.
- UniTest는 `BlackThunder.UniTest` 안정 버전 `0.2.0` 패키지를 정확히 고정하여 참조한다. `BlackboxTableProject`는 `ExecutionMode.Inline`을 선택하여 전역 Blackbox 상태를 사용하는 기존 실행 방식을 유지한다.
- NUnit 3.14.0을 이 프로젝트의 테스트 API 계약으로 사용한다.
- 기록 비활성 fallback 동작은 별도 빌드 축이 아니라 `UseBlackbox=false` 런타임 설정으로 확인한다.

### 1-2. 작성 규칙

- Blackbox 자체를 검증하는 테스트 코드에는 Blackbox 계측 호출을 넣지 않는다.
- 새 assert는 `Assert.That(actual, constraint)` 형식을 우선 사용한다.
- 각 테스트 함수 또는 독립 assert 구간 직전에는 `01-Tables.md`의 어느 표와 동작을 검증하는지 짧은 영어 주석을 둔다.
- `01-Tables.md`의 `X` 칸은 논리적으로 존재할 수 없는 조합이므로 테스트 대상에서 제외한다.
- `X`를 제외한 모든 결과 칸은 테스트에 포함한다. `-`, `^`, 예외, 상태 변화, 고유 동작, 내부 동작 칸은 모두 테스트 함수 또는 한 테스트 안의 독립 assert 구간으로 대응시킨다.
- public API 테스트는 `BlackboxHandle` 기준으로 작성하고, internal 구조 검증이 필요한 경우에만 `Blackbox`, `LogContext`, `LogData`, export tools를 직접 호출한다.
- C# 9 테스트에서는 `BlackboxHandle.WriteHandler`를 직접 구성해 handler 상태와 ref 오버로드를 검증한다. 일반 interpolated string 호출은 식을 먼저 평가하고 string 오버로드로 전달하는 fallback 경로로 검증한다. 자동 handler 변환과 식 평가 생략은 C# 10으로 컴파일하는 `tests/BlackThunder.BlackboxSystem.Tests.CSharp10`에서 공개 API만으로 확인한다.
- 파일 출력 테스트는 임시 폴더를 사용한다. 일반 export 테스트에서는 자동 open을 `OpenLogOption.Never`로 끄고, `OpenLog` 전용 칸은 테스트 빌드 hook으로 외부 프로세스 실행 없이 실패 warning 경로를 검증한다.

### 1-3. 초기 예상 규모와 현재 검증

초기 계획에서는 `01-Tables.md`의 `X`를 제외한 모든 결과 칸을 약 95-115개 테스트 함수, 120-170개 assert 구간으로 검증할 것으로 예상했다. 이 수치는 세션·동시성·수명 검증을 확장하기 전의 계획이며 현재 테스트 수의 상한이 아니다. 전체 표의 상태·동작 커버리지를 유지하면서 필요한 회귀 검증을 추가하고, 실제 실행 결과는 [테스트 결과](03-Results.md)와 [세션 구현 검증](04-Sessions.md)에 기록한다.

### 1-4. 칸 커버리지 기준

테스트 계획과 테스트 코드에서는 아래 기준을 사용한다.

| 표기 | 테스트 계획 포함 여부 | 기준 |
| --- | --- | --- |
| `X` | 제외 | 논리적으로 존재할 수 없는 상태/동작 조합이다. |
| `-` | 포함 | 아무 일도 일어나지 않는 것이 의도된 동작인지 검증한다. |
| `^` | 포함 | 위쪽 칸과 같은 결과가 유지되는지 검증한다. 같은 테스트 안에서 별도 assert 구간으로 둘 수 있다. |
| _예외_ | 포함 | 지정된 예외 타입과 부작용 없음을 검증한다. |
| **상태 변화** | 포함 | 상태 변화와 그 변화의 관찰값을 검증한다. |
| `[동작]` | 포함 | 고유 동작의 결과와 부작용을 검증한다. |
| `<동작>` | 포함 | 내부적으로 이어지는 동작이 실제로 실행되는지 검증한다. |

테스트 코드 작성 시 각 테스트 주석에는 가능한 한 `Table 2-2 / Alive x Dispose.DifferentThread`처럼 표 위치를 적어, non-X 칸 누락 여부를 다시 확인할 수 있게 한다.

## 2. 공통 테스트 도구

### 2-1. BlackboxTestDoubles.cs

공통 테스트 fixture와 helper를 둔다.

| 도구 | 역할 |
| --- | --- |
| `NamedOwner` | `ToString()`이 안정적인 owner object |
| `Reset()` | `BlackboxHandle.RestartDefaultSession()`, logger, 설정값 초기화 |
| `Owner(name)` | 새 `NamedOwner` 생성 |
| `Blackbox(name)` | owner에 대응되는 internal `Blackbox` 생성 |
| `BlackboxFor(owner)` | 이미 만든 owner에 대응되는 internal `Blackbox` 생성 |
| `Logs(blackbox)` | `blackbox.GetLogs()`를 list로 수집 |
| `Lines(blackbox)` | `blackbox.GetLogs()` 결과를 문자열 list로 렌더링 |
| `CreateTempDirectory()` | export 테스트용 임시 폴더 생성 |
| `GetFiles(directory, pattern)` | export 결과 파일을 검색 |
| `CleanupTempDirectories()` | 테스트에서 만든 임시 폴더 삭제 |
| `ForceFullCollection()` | weak reference 정책 검증을 위한 강제 GC |
| `NormalLogs` / `WarningLogs` | logger 호출 결과 저장 |

이 파일은 테스트 보조 도구이므로 `[Test]` 메서드를 포함하지 않는다.

## 3. 단위 테스트 계획

### 3-1. LogDataTests.cs

대응 표: `1-1. LogData`

| 테스트 | 검증 내용 |
| --- | --- |
| `ConstructorStoresValueMetadata` | Value 상태 생성 시 owner, message, method, scope, sequence, thread metadata를 저장한다. |
| `ConstructorStoresTagMetadata` | Tagged 상태 생성 시 `Tags`, `TaggedBy`, `TagTargetTypes`를 저장한다. |
| `ConstructorStoresInteractionMetadata` | Interaction 상태 생성 시 `InteractionId`, `ExertedBy`, `ExertingTo`를 저장한다. |
| `MutableExportFieldsCanBeUpdated` | export/merge 단계에서 `ScopeDepth`, `IsValid`, `Message`, interaction 정보가 갱신된다. |
| `ToStringUsesFormatter` | `ToString()`이 `LogFormatter.RenderTextLine(...)` 경로의 결과를 낸다. |

### 3-2. LogFormatterTests.cs

대응 표: `1-2. LogFormatter`

| 테스트 | 검증 내용 |
| --- | --- |
| `RenderMessageReplacesTagPlaceholders` | `%0`, `%1` placeholder가 target 참조로 치환된다. |
| `RenderMessageAppendsUnusedTags` | message에서 사용하지 않은 tag가 뒤에 추가된다. |
| `RenderTaggedMessageHonorsTargetTypes` | `TargetTypes` 조합에 따라 target 로그 메시지 상세도가 달라진다. |
| `RenderTextLineIncludesSequenceThreadScopeAndInteraction` | 한 줄 text에 sequence, thread, scope, interaction 정보가 포함된다. |
| `RenderMethodLabelDependsOnScopeType` | open/close/step/normal label이 scope type에 맞게 렌더링된다. |
| `ArrowAndTagRefHandleInteractionIds` | interaction id 유무에 따라 arrow와 tag ref 표현이 달라진다. |

### 3-3. TagHandleTests.cs

대응 표: `2-1. TagHandle`

| 테스트 | 검증 내용 |
| --- | --- |
| `DefaultWithAndStringConversionDoNothing` | default handle에서 `With(...)`와 문자열 변환은 기록을 만들지 않고 빈 문자열을 반환한다. |
| `FallbackMessageConvertsToOriginalMessage` | fallback handle의 문자열 변환은 원래 message를 반환한다. |
| `WithValidTargetAddsSourceAndTargetTags` | source log와 target log에 tag 정보가 반영된다. |
| `WithNullTargetTagsNull` | `With(null)`은 null target으로 렌더링된다. |
| `WithNullArrayTagsNull` | `With((object[])null)`도 null target으로 정규화된다. |
| `WithTargetTypesLastOverridesTargetLogPolicy` | 마지막 `TargetTypes` 인자가 target 로그 표시 정책으로 사용된다. |

### 3-4. ScopeHandleTests.cs

대응 표: `2-2. ScopeHandle`

| 테스트 | 검증 내용 |
| --- | --- |
| `DefaultHandleOperationsDoNothing` | default handle의 `With(...)`와 dispose는 로그를 만들지 않고 disposed 상태를 유지한다. |
| `AliveDisposeClosesScope` | alive handle dispose가 close log를 추가하고 disposed 상태가 된다. |
| `DisposedDisposeDoesNotAddLogAgain` | 중복 dispose가 close log를 추가하지 않는다. |
| `WithAddsTagsToOpenLog` | `With(...)`가 scope open log에 target을 붙이고 같은 handle을 유지한다. |
| `DifferentThreadDisposeWarns` | 생성 thread와 다른 thread에서 dispose하면 warning을 남긴다. |
| `PrintedDisposeDoesNotCloseScope` | export 이후 dispose는 close log를 추가하지 않는다. |

### 3-5. ExertHandleTests.cs

대응 표: `2-3. ExertHandle`

| 테스트 | 검증 내용 |
| --- | --- |
| `DefaultDisposeDoesNothing` | default exert handle dispose는 아무 동작도 하지 않는다. |
| `DisposeMergesAdjacentTargetScope` | interaction 바로 다음 open scope가 있으면 병합한다. |
| `DisposeWithoutMergeTargetOnlyDisposes` | 병합 대상이 없으면 로그를 변경하지 않고 disposed 처리만 한다. |
| `DisposedDisposeDoesNotMergeAgain` | 중복 dispose가 merge를 반복하지 않는다. |
| `DifferentThreadDisposeWarns` | 다른 thread dispose는 warning을 남긴다. |
| `PrintedDisposeDoesNotMerge` | export 이후 dispose는 병합하지 않는다. |

### 3-6. SessionCounterTests.cs

대응 표: `3-1. 세션 카운터 (BlackboxSession)`

| 테스트 | 검증 내용 |
| --- | --- |
| `IdsIncreaseIndependently` | blackbox id, interaction id, sequence가 독립적으로 증가한다. |
| `ResetRestartsCounters` | 기본 세션 재시작 후 각 counter가 초기값부터 다시 시작한다. |

### 3-7. BlackboxSessionRegistryTests.cs

대응 표: `3-2. BlackboxSessionRegistry`

| 테스트 | 검증 내용 |
| --- | --- |
| `GetBlackboxRejectsNull` | null subject는 `ArgumentNullException`을 던진다. |
| `GetBlackboxCreatesAndReusesOwner` | 같은 owner는 같은 `Blackbox`를 재사용한다. |
| `ContainsAndCountTrackRegisteredOwners` | 등록 전/후 `Contains(...)`와 등록된 owner 수가 함께 달라진다. |
| `RestartDefaultSessionClearsRegistryRuntimeAndHandles` | 기본 세션을 교체해 저장소·카운터를 새로 시작하고 이전 기본 세션 핸들을 종료한다. 전역 핸들 풀은 유지한다. |
| `StrongReferencePolicyKeepsOwnerReference` | strong reference 설정이 owner를 강하게 보관한다. |
| `WeakReferencePolicyStoresWeakOwnerReference` | weak reference 설정에서는 owner가 사라질 수 있고 fallback owner string을 유지한다. |

### 3-8. InfrastructureTests.cs

대응 표: `3-3. Infrastructure`

| 테스트 | 검증 내용 |
| --- | --- |
| `ConfigureStoresSettings` | `Configure(...)`가 logger와 export 설정을 저장한다. |
| `ResolveUsesConfiguredDefaultsAndExplicitValues` | `BasedOnSettings` 값은 현재 설정값으로 해석되고, 명시 값은 설정값으로 덮이지 않는다. |
| `LogDispatchesToMatchingLogger` | normal/warning logger가 분리 호출된다. |
| `TryMarkPrintedSucceedsOnceAndResetAllowsPrintingAgain` | 크래시 동결은 세션당 한 번만 성공하고 기본 세션 재시작 후 새 세션에서 다시 동결할 수 있다. |

### 3-9. LogContextTests.cs

대응 표: `4-1. LogContext`

| 테스트 | 검증 내용 |
| --- | --- |
| `EnqueueLogStoresLogData` | 일반 로그가 `LogData`로 저장된다. |
| `RingBufferKeepsRecentLogs` | buffer 크기를 넘으면 최근 로그만 유지한다. |
| `OpenAndCloseScopeStoresScopePair` | open/close 로그가 같은 scope id로 저장된다. |
| `CloseMissingScopeWarns` | 열리지 않은 scope close는 warning을 남긴다. |
| `CloseOuterFirstAutoClosesNewerScopes` | 같은 thread에서 outer를 먼저 닫으면 newer scope를 자동 close한다. |
| `CloseFromDifferentThreadWarnsWithoutAutoClosingNewerScopes` | cross-thread close는 warning만 남기고 newer 자동 close를 하지 않는다. |
| `ResolveWithAddsSourceAndTargetTags` | source와 target 로그에 tag 정보가 반영된다. |
| `TryMergeScopeMergesAdjacentInteractionAndOpenScope` | adjacent interaction/open scope를 병합한다. |
| `TryMergeScopeRejectsNonAdjacentOrMissingTarget` | 병합 대상이 없으면 기존 로그를 유지한다. |
| `GetLogsStopsAtMaxSequence` | `maxSequence` 이후 로그를 반환하지 않는다. |

### 3-10. BlackboxTests.cs

대응 표: `4-2. Blackbox`

| 테스트 | 검증 내용 |
| --- | --- |
| `WriteStoresSelfLog` | `Write(...)`가 owner 로그를 저장한다. |
| `ScopeReturnsScopeHandleAndOpenLog` | `Scope(...)`가 open log와 handle을 만든다. |
| `ExertMessageRejectsNullTarget` | null target은 `ArgumentNullException`을 던진다. |
| `ExertMessageStoresSelfInteraction` | 자기 자신 대상 interaction을 한 로그에 저장한다. |
| `ExertMessageStoresTwoSidedPeerInteraction` | peer interaction을 양쪽 owner에 저장한다. |
| `ExertReturnsHandleForPeerInteraction` | `Exert(...)`가 interaction 저장 후 handle을 반환한다. |
| `PrintedStateStopsNewLogs` | printed 상태에서는 새 로그가 추가되지 않는다. |
| `GetLogsSortsAcrossThreadContextsBySequence` | 여러 context 로그를 sequence 기준으로 정렬한다. |
| `GetLogsByContextReturnsSeparateThreadBuckets` | thread별 context 로그 묶음을 분리해서 반환한다. |
| `OwnerStringFallsBackWhenOwnerReferenceIsLost` | weak owner가 사라진 경우 fallback owner string을 사용한다. |

### 3-11. BlackboxHandleTests.cs

대응 표: `5-1. BlackboxHandle`, `1-3. BlackboxHandle.WriteHandler`

| 테스트 | 검증 내용 |
| --- | --- |
| `OfCreatesValidHandleAndRejectsNull` | 유효 subject는 handle을 만들고 null은 예외를 던진다. |
| `InvalidHandleReturnsFallbacks` | default/invalid handle은 로그를 만들지 않고 메시지 또는 disposed handle을 반환한다. |
| `ConstructWritesCtorScopeAndCachesHandle` | 생성 scope와 cached handle을 만든다. |
| `WhenReturnsValidOrSkippedHandle` | 조건에 따라 valid/default handle을 반환한다. |
| `DisposeWritesDisposedLog` | dispose API가 disposed message를 남긴다. |
| `WriteReturnsMessageAndStoresLog` | write 호출이 원래 message를 보존하고 로그를 저장한다. |
| `ScopeReturnsAliveScopeHandle` | scope API가 alive handle을 반환한다. |
| `ExertMessageRejectsNullAndStoresPeerInteraction` | null/peer 조건을 각각 검증한다. |
| `ExertRejectsNullAndReturnsHandleForPeer` | null/peer 조건을 각각 검증한다. |
| `WriteErrorRecordsErrorAndTargets` | 오류 로그와 target tag를 저장한다. |
| `WriteErrorCanTriggerCrashExport` | 설정에 따라 `WriteError`가 crash export로 이어진다. |
| `CrashExportWritesStackTraceAndExportsOnce` | stack trace 로그와 crash 파일을 만든다. |
| `ExportWarnsForInvalidOrDuplicateExport` | invalid handle과 duplicate export warning을 검증한다. |
| `RestartDefaultSessionRestoresDefaultRuntimeState` | 기본 세션 재시작 후 새 기록 수명을 시작할 수 있다. |
| `WriteHandlerFormatsInterpolatedValuesForValidHandle` | 유효 handle에서 interpolated string literal, formatted value, null value가 message로 조립된다. |
| `WriteHandlerSkipsFormattingForInvalidHandle` | invalid handle에서 handler가 skipped 상태가 되어 문자열 조립과 로그 저장을 생략한다. |
| `CSharp9InterpolationUsesStringFallbackWhenRecordingIsDisabled` | C# 9 호출자는 기록이 꺼져 있어도 보간 식을 먼저 평가하고 원래 message를 반환하며 로그를 저장하지 않는다. |
| `UseBlackboxFalseDisablesNewAndExistingHandles` | `UseBlackbox=false` 상태에서 public API가 invalid/default handle과 원래 message를 반환하고 실제 기록을 만들지 않는다. |

### 3-12. ExportToolsTests.cs

대응 표: `6-1. export tools`

| 테스트 | 검증 내용 |
| --- | --- |
| `BuildExportGraphBuildsRootOnly` | 연결 없는 root node만 만든다. |
| `BuildExportGraphIncludesFocusedIncomingPeers` | focused export가 필요한 peer만 포함한다. |
| `BuildExportGraphIncludesFullOutgoingPeers` | full export가 outgoing peer까지 포함한다. |
| `BuildExportGraphRespectsDepthLimit` | recursion depth 제한을 지킨다. |
| `FlattenStepsConvertsEmptyScopePair` | 빈 open/close scope를 step으로 접는다. |
| `FlattenStepsPreservesNonEmptyScope` | 내부 로그가 있는 scope는 유지한다. |
| `ResolveScopeDepthsAssignsNestedDepths` | 중첩 scope depth를 계산한다. |
| `TrimSmartSanitizesFileNameParts` | 비어 있거나 파일 이름에 부적합한 입력은 `null`로 정규화하고 긴 입력은 앞/뒤를 보존해 줄인다. |

### 3-13. ExporterTests.cs

대응 표: `6-2. exporters`

| 테스트 | 검증 내용 |
| --- | --- |
| `TxtExporterRejectsMissingLogDirectory` | txt export에서 빈 경로는 예외를 던진다. |
| `TxtExporterCreatesNormalAndCrashFiles` | normal/crash txt 파일을 만든다. |
| `HtmlExporterRejectsMissingLogDirectory` | html export에서 빈 경로는 예외를 던진다. |
| `HtmlExporterCreatesNormalAndCrashFiles` | normal/crash html 파일을 만든다. |
| `HtmlExporterWritesInteractionLinks` | html export가 interaction anchor/link를 포함한다. |
| `OpenLogFailureIsReportedAsWarning` | 자동 open 실패는 warning으로 보고된다. |

## 4. 통합 테스트 계획

### 4-1. IntegrationTests.cs

대응 표: `7. 통합 흐름`

| 테스트 | 검증 내용 |
| --- | --- |
| `SingleOwnerHistoryExportsReadableFile` | 한 owner의 생성, scope, write, export 흐름을 파일에서 확인한다. |
| `TwoOwnerInteractionExportsConnectedLogs` | source와 target 상호작용이 export에서 연결된다. |
| `TagTargetFlowWritesSourceAndTargetReferences` | `.With(...)`가 source/target 양쪽 표시를 만든다. |
| `ErrorFlowExportsErrorAndCrashContext` | error/crash 흐름이 오류 로그와 파일 출력으로 이어진다. |
| `RestartDefaultAndRerunStartsClean` | export 후 reset하고 새 실행을 독립적으로 기록한다. |
| `RingBufferLimitKeepsRecentLogsInPublicFlow` | public API 경로에서 buffer 제한이 최근 로그 유지로 나타난다. |

### 4-2. SampleCliTests.cs

대응 표: `8. Sample CLI`

테스트 프로젝트가 준비한 SampleApp 배포 파일을 테스트마다 독립된 임시 디렉터리로 복사하고, 실제 샘플 DLL을 .NET 호스트의 자식 프로세스로 실행한다. 표준 입력을 닫지 않은 상태에서 종료를 기다려 일회 실행이 EOF나 대화형 프롬프트에 의존하지 않는지 확인한다. 대화형 검증에서는 입력 줄을 전달한 뒤에도 같은 조건을 유지한다. 종료 코드, stdout, stderr와 임시 디렉터리의 파일을 관찰하며 테스트 종료 후 해당 디렉터리를 정리한다.

아래 여섯 테스트 메서드는 매개변수 조합을 포함하여 17개 사례를 구성한다.

| 테스트 | 사례 수 | 대응 축과 검증 내용 |
| --- | --- | --- |
| `InformationCommandsExitWithoutReadingInputOrCreatingOutput` | 2 | `One-shot x Help/Exit`: help와 exit가 입력을 기다리지 않고 성공 종료하며 출력 디렉터리를 생성하지 않는다. |
| `ScenariosAndNumericAliasesExportAndExitWithInputStillOpen` | 8 | `One-shot x Scenario`: 네 시나리오와 숫자 별칭이 한 번 실행되고, 완료 메시지·시나리오별 HTML 파일·의도된 예외 결과를 남긴다. |
| `InvalidCommandsReturnUsageErrorWithoutRunningAScenario` | 4 | `One-shot x InvalidInput`: unknown 명령과 write/help/exit의 추가 인자를 종료 코드 2로 거부하고 stderr에 사용법을 출력한다. |
| `InteractiveModeContinuesAfterEmptyInputAndInvalidCommands` | 1 | `Interactive x Empty/Invalid/Help/Exit`: 빈 입력과 잘못된 명령 뒤에도 help·exit와 프롬프트 반복이 유지된다. |
| `UnexpectedOutputFailureReturnsFailureAndDoesNotReportSuccess` | 1 | `One-shot x OutputFailure`: SampleOutput 위치를 파일로 막으면 종료 코드 1과 실패 메시지를 반환하고 성공을 보고하지 않는다. |
| `InteractiveModeContinuesAfterUnexpectedOutputFailure` | 1 | `Interactive x OutputFailure`: 같은 출력 오류 뒤에도 help와 exit를 처리하며 실패한 실행을 완료로 표시하지 않는다. |
