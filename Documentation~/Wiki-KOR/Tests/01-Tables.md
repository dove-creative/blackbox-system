# Blackbox 테스트 상태-동작 표

이 문서는 Blackbox 프레임워크의 새 테스트를 작성하기 전에, 객체별 상태와 동작을 정리하는 표이다.

Blackbox는 기록 대상 객체, 로그 문맥, disposable handle, export 도구가 함께 작동하는 프레임워크이다. 따라서 테스트 표도 하나의 큰 기능 묶음이 아니라, 낮은 단위의 값/handle 객체에서 시작해 외부 진입점인 `BlackboxHandle`과 export 흐름으로 올라가는 순서로 작성한다.

---

# 표기

- **상태명**: 동작 후 상태가 바뀐다.
- `[동작]`: 해당 상태에서 고유 동작을 수행한다.
- `<동작>`: 다른 동작을 내부적으로 이어서 수행한다.
- `<`: 왼쪽 동작과 같은 기능 그룹에 속한 하위 항목이다.
- `^`: 위쪽 칸의 동작이나 결과를 그대로 따른다.
- _예외_: 해당 예외를 던진다.
- `-`: 상태 변화 없이 정상적으로 무시하거나 기존 값을 유지한다.
- `X`: 해당 상태나 조건에서 의미가 없는 조합이다.
- **SymbolOff**: `BLACKBOX` 심볼이 없는 빌드 축이다. 이 축은 `com.BlackThunder.BlackboxSystem.Tests.asmdef`가 아니라 별도 no-Blackbox 검증 경로에서 확인한다.

---

# 1. 값과 formatting 단위

## 1-1. `LogData`

`LogData`는 로그 한 줄의 구조화된 값이다. 테스트에서는 생성자와 출력 변환 경로가 저장한 값을 유지하는지 확인한다.

| LogData     | -   | Create          | ToString                | MutateForExport                   |
| ----------- | --- | --------------- | ----------------------- | --------------------------------- |
|             | -   |                 |                         |                                   |
|             | -   |                 |                         |                                   |
| Value       | -   | **Value**       | `[RenderTextLine]`      | `[ScopeDepth/IsValid/Message 갱신]` |
| Tagged      | -   | **Tagged**      | `[RenderTaggedMessage]` | `[Tags 갱신]`                       |
| Interaction | -   | **Interaction** | `[RenderInteraction]`   | `[Interaction 병합 정보 갱신]`          |

## 1-2. `LogFormatter`

`LogFormatter`는 상태를 가지기보다 `LogData`를 출력 문자열로 변환하는 정적 도구이다.

| LogFormatter | -   | RenderMessage          | RenderTextLine                           | RenderMethodLabel     | Arrow     | TagRef        |
| ------------ | --- | ---------------------- | ---------------------------------------- | --------------------- | --------- | ------------- |
|              | -   |                        |                                          |                       |           |               |
|              | -   |                        |                                          |                       |           |               |
| Ready        | -   | `[tag placeholder 해석]` | `[thread/sequence/scope/interaction 표시]` | `[scope type별 label]` | `[방향 표시]` | `[target 표시]` |

## 1-3. `BlackboxHandle.WriteHandler`

`WriteHandler`는 interpolated string 호출에서 유효한 handle일 때만 문자열을 조립하는 public API 보조 값이다. 테스트에서는 문자열 조립 자체보다, 유효하지 않은 handle에서 formatted value 평가와 로그 조립이 빠지는지 확인한다.

| WriteHandler | -   | Construct       | AppendLiteral | AppendFormatted        | GetTextAndClear       |
| ------------ | --- | --------------- | ------------- | ---------------------- | --------------------- |
|              | -   | ValidHandle     | Literal       | Value                  |                       |
|              | -   | InvalidHandle   |               | NullValue              |                       |
| Active       | -   | **Active**      | `[literal 저장]` | `[value 문자열화]`      | `[message 반환 + clear]` |
| Skipped      | -   | **Skipped**     | -             | -                      | `[빈 문자열 반환]`       |
| Active       | -   | ^               | ^             | `[null 문자열화]`       | ^                     |

---

# 2. handle 단위

## 2-1. `TagHandle`

`TagHandle`은 `Write(...)` 또는 `Scope(...)` 이후 관련 target을 붙이는 일회성 연결점이다.

| TagHandle       | -   | With             | <          | <                     | <                  | ToString             |
| --------------- | --- | ---------------- | ---------- | --------------------- | ------------------ | -------------------- |
|                 | -   | ValidTarget      | NullTarget | NullArray             | TargetTypesLast    |                      |
|                 | -   |                  |            |                       |                    |                      |
| Default         | -   | -                | -          | -                     | -                  | `[fallback 빈 문자열]` |
| FallbackMessage | -   | -                | -          | -                     | -                  | `[fallback message]` |
| SourceLog       | -   | `[tag 확정]`      | `[null tag]` | `[null tag]`          | `[표시 정책 적용]` | `[resolved message]` |

## 2-2. `ScopeHandle`

`ScopeHandle`은 열린 scope를 닫는 disposable handle이다.

| ScopeHandle | -   | With                    | Dispose       | <                    | <             |
| ----------- | --- | ----------------------- | ------------- | -------------------- | ------------- |
|             | -   | ValidTarget             | SameThread    | DifferentThread      | Printed       |
|             | -   |                         |               |                      |               |
| Default     | -   | -                       | -             | -                    | -             |
| Alive       | -   | `[open log에 tag 부착]` | **Disposed**  | **Disposed** + warning | **Disposed** |
| Disposed    | -   | -                       | -             | -                    | -             |

## 2-3. `ExertHandle`

`ExertHandle`은 상호작용 로그 이후 받는 쪽 scope 병합을 시도하는 disposable handle이다.

| ExertHandle | -   | Dispose                 | <             | <                    | <             |
| ------------ | --- | ----------------------- | ------------- | -------------------- | ------------- |
|              | -   | MergeTargetExists       | MergeTargetMissing | DifferentThread | Printed       |
|              | -   |                         |               |                      |               |
| Default      | -   | -                       | -             | -                    | -             |
| Alive        | -   | **Disposed** + `[merge]` | **Disposed**  | **Disposed** + warning | **Disposed** |
| Disposed     | -   | -                       | -             | -                    | -             |

---

# 3. runtime 저장 단위

## 3-1. `BlackboxRuntime`

`BlackboxRuntime`은 전역 id, interaction id, sequence를 발급한다.

| BlackboxRuntime | -   | GetNextBlackboxId | GetNextInteractionId     | GetNextSequence | Reset     |
| --------------- | --- | ----------------- | ------------------------ | --------------- | --------- |
|                 | -   |                   |                          |                 |           |
|                 | -   |                   |                          |                 |           |
| Ready           | -   | `[id 증가]`       | `[interaction id 증가]`   | `[sequence 증가]` | **Ready** |

## 3-2. `BlackboxRegistry`

`BlackboxRegistry`는 대상 객체와 `Blackbox`를 연결한다.

| BlackboxRegistry | -   | GetBlackbox            | <                      | <                 | <               | Contains | <                      | Count     | ForceReset              |
| ---------------- | --- | ---------------------- | ---------------------- | ----------------- | --------------- | -------- | ---------------------- | --------- | ----------------------- |
|                  | -   | ValidSubject           | NullSubject            | StrongReferenceOn | StrongReferenceOff | ValidSubject | NullSubject        |           |                         |
|                  | -   |                        |                        |                   |                 |          |                        |           |                         |
| Empty            | -   | **Registered**         | _ArgumentNullException_ | **Registered**    | **Registered**  | `[false]` | _ArgumentNullException_ | `[0]`     | -                       |
| Registered       | -   | `[same owner 재사용]`   | _ArgumentNullException_ | `[strong owner]`  | `[weak owner]`  | `[true]` | _ArgumentNullException_ | `[count]` | **Empty**               |
| Any              | -   | ^                      | ^                      | ^                 | ^               | ^        | ^                      | X         | `<runtime/handle reset>` |

## 3-3. `Infrastructure`

`Infrastructure`는 Blackbox의 전역 설정과 export 완료 상태를 보관한다.

설정값에는 `LogDirectory`, logger, `MaxLogCount`, `StrongReference`, `DefaultRecursionDepth`, `ExportFormat`, `FullExportOption`, `OpenLogOption`, `ExceptionHandlingOption`, `TagTargetTypes`가 포함된다. `BasedOnSettings` 값은 기존 설정을 덮지 않고, resolve 시점에 현재 설정값으로 해석되어야 한다.

| Infrastructure | -   | Configure/Set       | Resolve                     | Log           | TryMarkPrinted | ForceResetRuntimeState |
| -------------- | --- | ------------------- | --------------------------- | ------------- | -------------- | ---------------------- |
|                | -   |                     |                             |               |                |                        |
|                | -   |                     |                             |               |                |                        |
| Ready          | -   | `[settings 갱신]`    | `[explicit/default 해석]`    | `[logger 호출]` | **Printed**    | -                      |
| Printed        | -   | `[settings 갱신]`    | `[explicit/default 해석]`    | `[logger 호출]` | -              | **Ready**              |

---

# 4. 로그 문맥 단위

## 4-1. `LogContext`

`LogContext`는 한 `Blackbox` 안에서 thread별 로그 버퍼와 scope 생명주기를 관리한다.

| LogContext    | -   | EnqueueLog            | OpenScope              | CloseScope     | <                     | <                         | ResolveWith          | TryMergeScope           | <             | GetLogs        |
| ------------- | --- | --------------------- | ---------------------- | -------------- | --------------------- | ------------------------- | -------------------- | ----------------------- | ------------- | -------------- |
|               | -   |                       |                        | CloseNewest    | CloseOuterFirst       | CloseFromDifferentThread  | ValidTarget          | MergeAdjacent           | MergeNonAdjacent |                |
|               | -   |                       |                        |                |                       |                           |                      |                         |               |                |
| Ready         | -   | `[log 저장]`          | **ScopeOpen**          | _Warning_      | _Warning_             | _Warning_                 | -                    | -                       | -             | `[valid logs]` |
| ScopeOpen     | -   | `[scoped log 저장]`   | **ScopeOpen**          | **Ready**      | **Ready** + auto close | `[requested close]` + warning | `[tag resolve]` | `[merge]`               | -             | `[valid logs]` |
| RingBufferFull | -  | `[old log overwrite]` | `[old log overwrite 가능]` | `[available range]` | `[available range]` | `[available range]`       | `[available log only]` | `[available range only]` | -             | `[recent logs]` |

## 4-2. `Blackbox`

`Blackbox`는 owner 하나에 대한 실제 기록 규칙을 적용한다.

| Blackbox           | -   | Write             | Scope          | ExertMessage           | <                    | <                         | Exert                 | <                    | <                         | GetLogs              | GetLogsByContext     |
| ------------------ | --- | ----------------- | ------------------- | ---------------------- | -------------------- | ------------------------- | --------------------- | -------------------- | ------------------------- | -------------------- | -------------------- |
|                    | -   |                   |                     | OtherNull              | OtherSelf            | OtherPeer                 | OtherNull             | OtherSelf            | OtherPeer                 | MultiContext         |                      |
|                    | -   |                   |                     |                        |                      |                           |                       |                      |                           |                      |                      |
| Ready              | -   | `[self log 저장]` | `[open scope 저장]` | _ArgumentNullException_ | `[self interaction]` | `[two-sided interaction]` | _ArgumentNullException_ | `[self exert handle]` | `[interaction + handle]` | `[sequence 정렬]`   | `[context별 logs]`   |
| Printed            | -   | -                 | -                   | -                      | -                    | -                         | -                     | -                    | -                         | `[snapshot]`         | `[snapshot]`         |
| OwnerReferenceLost | -   | `[fallback owner string]` | `[fallback owner string]` | `[fallback owner string]` | ^ | ^ | `[fallback owner string]` | ^ | ^ | `[fallback owner string]` | `[fallback owner string]` |

---

# 5. 외부 진입점 단위

## 5-1. `BlackboxHandle`

`BlackboxHandle`은 외부 코드가 사용하는 public API 표면이다.

이 표는 `BLACKBOX` 심볼이 켜진 일반 검증 축과, 심볼이 꺼진 fallback 검증 축을 함께 가진다. `SymbolOff` 행은 no-Blackbox 빌드에서 public API가 원래 메시지나 default handle을 반환하고 실제 기록을 만들지 않는지 확인한다.

| BlackboxHandle    | -   | Of                  | <                      | Construct                 | When              | <       | Dispose          | Write          | Scope       | ExertMessage           | <                     | Exert          | <                     |
| ----------------- | --- | ------------------- | ---------------------- | ------------------------- | ----------------- | ------- | ---------------- | -------------- | ---------------- | ---------------------- | --------------------- | --------------- | --------------------- |
|                   | -   | ValidSubject        | NullSubject            |                           | True              | False   |                  |                |                  | OtherNull              | OtherPeer             | OtherNull       | OtherPeer             |
|                   | -   |                     |                        |                           |                   |         |                  |                |                  |                        |                       |                 |                       |
| SymbolOff         | -   | **Default/Invalid** | X                      | -                         | -                 | -       | -                | `[message 반환]` | -              | `[message 반환]`        | `[message 반환]`       | -               | -                     |
| Default/Invalid   | -   | X                   | X                      | -                         | -                 | -       | -                | `[message 반환]` | -              | `[message 반환]`        | `[message 반환]`       | -               | -                     |
| Valid             | -   | `[same owner handle]` | _ArgumentNullException_ | `[ctor scope + cached handle]` | `[valid 유지]` | **ConditionalSkipped** | `[disposed log]` | `[self log]` | `[scope handle]` | _ArgumentNullException_ | `[two-sided interaction]` | _ArgumentNullException_ | `[exert handle]` |
| ConditionalSkipped | -  | X                   | X                      | -                         | X                 | -       | -                | `[message 반환]` | -              | `[message 반환]`        | `[message 반환]`       | -               | -                     |
| Printed           | -   | `[handle 생성 가능]` | _ArgumentNullException_ | -                         | `[condition 반영]` | ^       | -                | `[message 반환]` | -              | `[message 반환]`        | `[message 반환]`       | -               | -                     |

| BlackboxHandle    | -   | WriteError        | <                  | <                            | CrashExport                       | Export       | <               | ForceReset              |
| ----------------- | --- | ----------------- | ------------------ | ---------------------------- | --------------------------------- | ------------ | --------------- | ----------------------- |
|                   | -   | ErrorTargetsEmpty | ErrorTargetsPresent | ExceptionHandlingCrashExport |                                   | FirstExport   | DuplicateExport |                         |
|                   | -   |                   |                    |                              |                                   |              |                 |                         |
| Default/Invalid   | -   | `[message 반환]`   | `[message 반환]`    | `[message 반환]`              | `[message 반환 + warning 가능]`     | `[warning]`  | `[warning]`     | `<Registry.ForceReset>` |
| Valid             | -   | `[error log]`     | `[error log + tag]` | `<CrashExport>`              | `[error log + stack trace + export]` | **Printed** | -               | `<Registry.ForceReset>` |
| Printed           | -   | `[message 반환]`   | `[message 반환]`    | `[message 반환]`              | `[duplicate export 제한]`           | -            | -               | **Default reset state** |

---

# 6. export 단위

## 6-1. export tools

export tools는 원본 로그를 출력용 그래프로 정리한다.

| ExportTools  | -   | BuildExportGraph              | FlattenSteps                | ResolveScopeDepths | TrimSmart                    |
| ------------ | --- | ----------------------------- | --------------------------- | ------------------ | ---------------------------- |
|              | -   |                               |                             |                    |                              |
|              | -   |                               |                             |                    |                              |
| RootOnly     | -   | `[root node]`                 | `[empty scope step 변환]`    | `[scope depth 계산]` | X                            |
| Focused      | -   | `[incoming peer 중심 포함]`    | `[context별 변환]`           | `[context별 계산]`  | X                            |
| Full         | -   | `[incoming/outgoing peer 포함]` | `[context별 변환]`          | `[context별 계산]`  | X                            |
| DepthLimited | -   | `[recursionDepth까지만 포함]`  | `[context별 변환]`           | `[context별 계산]`  | X                            |
| FileName     | -   | X                             | X                           | X                  | `[invalid char 제거/길이 제한/null fallback]` |

## 6-2. exporters

`TxtExporter`와 `HtmlExporter`는 같은 export graph를 서로 다른 파일 형식으로 출력한다.

| Exporter            | -   | Export          | <               | <      |
| ------------------- | --- | --------------- | --------------- | ------ |
|                     | -   | Normal          | Crash           | OpenLog |
|                     | -   |                 |                 |        |
| LogDirectoryMissing | -   | _InvalidOperationException_ | ^       | X      |
| TxtReady            | -   | `[txt file 생성]` | `[CRASH prefix file 생성]` | `[파일 자동 열기 시도]` |
| HtmlReady           | -   | `[html file 생성]` | `[CRASH prefix file 생성]` | `[파일 자동 열기 시도]` |

---

# 7. 통합 흐름

통합 테스트는 개별 객체 표에서 검증한 동작을 바탕으로 대표적인 사용 흐름을 확인한다.

| Integration        | -   | Run                                      |
| ------------------ | --- | ---------------------------------------- |
|                    | -   |                                          |
|                    | -   |                                          |
| SingleOwnerHistory | -   | `[Construct -> Scope -> Write -> Export]` |
| TwoOwnerInteraction | -  | `[Exert -> target Scope -> Export 연결 확인]` |
| TagTargetFlow      | -   | `[Write.With / Scope.With -> source/target 표시 확인]` |
| ErrorFlow          | -   | `[WriteError / CrashExport -> error log + export 확인]` |
| ResetAndRerun      | -   | `[Export -> ForceReset -> 새 실행]`       |
| RingBufferLimit    | -   | `[MaxLogCount 초과 시 최근 로그 유지]`     |
