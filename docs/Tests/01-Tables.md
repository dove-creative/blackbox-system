# Blackbox 테스트 상태-동작 표

> 이 문서는 C# 구현의 검증 기준과 기록이다. TypeScript 사용법과 검증은 [TypeScript 안내](../../typescript/README.md)를 따른다.

> 세션 수명 기준: RestartDefaultSession은 BlackboxHandle의 공개 API이며 공유 기본 세션만 교체한다. Runtime/Registry/HandleManager의 Reset API는 제공하지 않는다. 기본 설정은 유지하고 키·기록 상태를 새로 시작하며, 명시적 세션은 Dispose 후 Begin으로 교체한다. 일반 Export는 반복 가능하고 CrashExport만 소속 세션을 동결한다. 아래 Printed 상태는 크래시 동결 상태를 뜻하며 일반 출력 완료로 진입하지 않는다. 자세한 계약은 [세션 문서](../Wiki/05-Sessions.md)를 따른다.

이 문서는 Blackbox 프레임워크의 새 테스트를 작성하기 전에, 객체별 상태와 동작을 정리하는 표이다.

Blackbox는 기록 대상 객체, 로그 문맥, disposable handle, export 도구가 함께 작동하는 프레임워크이다. 따라서 테스트 표도 하나의 큰 기능 묶음이 아니라, 낮은 단위의 값/handle 객체에서 시작해 외부 진입점인 `BlackboxHandle`과 export 흐름으로 올라가는 순서로 작성한다.

## 표기

- **상태명**: 동작 후 상태가 바뀐다.
- `[동작]`: 해당 상태에서 고유 동작을 수행한다.
- `<동작>`: 다른 동작을 내부적으로 이어서 수행한다.
- `<`: 왼쪽 동작과 같은 기능 그룹에 속한 하위 항목이다.
- `^`: 위쪽 칸의 동작이나 결과를 그대로 따른다.
- _예외_: 해당 예외를 던진다.
- `-`: 상태 변화 없이 정상적으로 무시하거나 기존 값을 유지한다.
- `X`: 해당 상태나 조건에서 의미가 없는 조합이다.
- **UseBlackboxOff**: `UseBlackbox`가 꺼진 런타임 축이다. 이 축에서는 public API가 원래 메시지나 default handle을 반환하고 실제 기록을 만들지 않는지 확인한다.

## 1. 값과 formatting 단위

### 1-1. LogData

`LogData`는 로그 한 줄의 구조화된 값이다. 테스트에서는 생성자와 출력 변환 경로가 저장한 값을 유지하는지 확인한다.

| LogData     | -   | Create          | ToString                | MutateForExport                   |
| ----------- | --- | --------------- | ----------------------- | --------------------------------- |
|             | -   |                 |                         |                                   |
|             | -   |                 |                         |                                   |
| Value       | -   | **Value**       | `[RenderTextLine]`      | `[ScopeDepth/IsValid/Message 갱신]` |
| Tagged      | -   | **Tagged**      | `[RenderTaggedMessage]` | `[Tags 갱신]`                       |
| Interaction | -   | **Interaction** | `[RenderInteraction]`   | `[Interaction 병합 정보 갱신]`          |

### 1-2. LogFormatter

`LogFormatter`는 상태를 가지기보다 `LogData`를 출력 문자열로 변환하는 정적 도구이다.

| LogFormatter | -   | RenderMessage          | RenderTextLine                           | RenderMethodLabel     | Arrow     | TagRef        |
| ------------ | --- | ---------------------- | ---------------------------------------- | --------------------- | --------- | ------------- |
|              | -   |                        |                                          |                       |           |               |
|              | -   |                        |                                          |                       |           |               |
| Ready        | -   | `[tag placeholder 해석]` | `[thread/sequence/scope/interaction 표시]` | `[scope type별 label]` | `[방향 표시]` | `[target 표시]` |

### 1-3. BlackboxHandle.WriteHandler

`WriteHandler`는 유효한 handle일 때만 문자열을 조립하는 public API 보조 값이다. C# 9 테스트에서는 handler를 직접 구성하여 활성 상태와 문자열 조립 여부를 확인한다. C# 9의 interpolated string 호출은 일반 string 오버로드를 사용하므로 식 평가가 생략되지 않는다. 컴파일러가 handler를 자동으로 구성하고 formatted value 평가를 생략하는 경로는 C# 10 이상 호출자에서 지원한다.

| WriteHandler | -   | Construct       | AppendLiteral | AppendFormatted        | GetTextAndClear       |
| ------------ | --- | --------------- | ------------- | ---------------------- | --------------------- |
|              | -   | ValidHandle     | Literal       | Value                  |                       |
|              | -   | InvalidHandle   |               | NullValue              |                       |
| Active       | -   | **Active**      | `[literal 저장]` | `[value 문자열화]`      | `[message 반환 + clear]` |
| Skipped      | -   | **Skipped**     | -             | -                      | `[빈 문자열 반환]`       |
| Active       | -   | ^               | ^             | `[null 문자열화]`       | ^                     |

## 2. handle 단위

### 2-1. TagHandle

`TagHandle`은 `Write(...)` 또는 `Scope(...)` 이후 관련 target을 붙이는 일회성 연결점이다.

| TagHandle       | -   | With             | <          | <                     | <                  | ToString             |
| --------------- | --- | ---------------- | ---------- | --------------------- | ------------------ | -------------------- |
|                 | -   | ValidTarget      | NullTarget | NullArray             | TargetTypesLast    |                      |
|                 | -   |                  |            |                       |                    |                      |
| Default         | -   | -                | -          | -                     | -                  | `[fallback 빈 문자열]` |
| FallbackMessage | -   | -                | -          | -                     | -                  | `[fallback message]` |
| SourceLog       | -   | `[tag 확정]`      | `[null tag]` | `[null tag]`          | `[표시 정책 적용]` | `[resolved message]` |

### 2-2. ScopeHandle

`ScopeHandle`은 열린 scope를 닫는 disposable handle이다.

| ScopeHandle | -   | With                    | Dispose       | <                    | <             |
| ----------- | --- | ----------------------- | ------------- | -------------------- | ------------- |
|             | -   | ValidTarget             | SameThread    | DifferentThread      | Printed       |
|             | -   |                         |               |                      |               |
| Default     | -   | -                       | -             | -                    | -             |
| Alive       | -   | `[open log에 tag 부착]` | **Disposed**  | **Disposed** + warning | **Disposed** |
| Disposed    | -   | -                       | -             | -                    | -             |

### 2-3. ExertHandle

`ExertHandle`은 상호작용 로그 이후 받는 쪽 scope 병합을 시도하는 disposable handle이다.

| ExertHandle | -   | Dispose                 | <             | <                    | <             |
| ------------ | --- | ----------------------- | ------------- | -------------------- | ------------- |
|              | -   | MergeTargetExists       | MergeTargetMissing | DifferentThread | Printed       |
|              | -   |                         |               |                      |               |
| Default      | -   | -                       | -             | -                    | -             |
| Alive        | -   | **Disposed** + `[merge]` | **Disposed**  | **Disposed** + warning | **Disposed** |
| Disposed     | -   | -                       | -             | -                    | -             |

## 3. runtime 저장 단위

### 3-1. 세션 카운터 (BlackboxSession)

`BlackboxSession`은 세션마다 id, interaction id, sequence를 발급한다. 표는 현재 세션(`BlackboxSessionRegistry.Current`)의 카운터를 대상으로 한다.

| SessionCounters | -   | NextBoxId         | NextInteractionId        | NextSequence    | BlackboxHandle.RestartDefaultSession |
| --------------- | --- | ----------------- | ------------------------ | --------------- | --------- |
|                 | -   |                   |                          |                 |           |
|                 | -   |                   |                          |                 |           |
| Ready           | -   | `[id 증가]`       | `[interaction id 증가]`   | `[sequence 증가]` | **Ready** |

### 3-2. BlackboxSessionRegistry

`BlackboxSessionRegistry`는 현재 세션을 찾고, 그 세션에서 대상 객체와 `Blackbox`를 연결한다.

| BlackboxSessionRegistry | -   | GetBlackbox            | <                      | <                 | <               | Contains | <                      | Count     | BlackboxHandle.RestartDefaultSession              |
| ---------------- | --- | ---------------------- | ---------------------- | ----------------- | --------------- | -------- | ---------------------- | --------- | ----------------------- |
|                  | -   | ValidSubject           | NullSubject            | StrongReferenceOn | StrongReferenceOff | ValidSubject | NullSubject        |           |                         |
|                  | -   |                        |                        |                   |                 |          |                        |           |                         |
| Empty            | -   | **Registered**         | _ArgumentNullException_ | **Registered**    | **Registered**  | `[false]` | _ArgumentNullException_ | `[0]`     | -                       |
| Registered       | -   | `[same owner 재사용]`   | _ArgumentNullException_ | `[strong owner]`  | `[weak owner]`  | `[true]` | _ArgumentNullException_ | `[count]` | **Empty**               |
| Any              | -   | ^                      | ^                      | ^                 | ^               | ^        | ^                      | X         | `[새 기본 세션 / 이전 세션 핸들 종료]` |

### 3-3. Infrastructure

`Infrastructure`는 현재 세션의 설정에 접근한다. 크래시 동결은 BlackboxSession가 소유한다.

설정값에는 `LogDirectory`, logger, `MaxLogCount`, `StrongReference`, `DefaultRecursionDepth`, `ExportFormat`, `FullExportOption`, `OpenLogOption`, `ExceptionHandlingOption`, `TagTargetTypes`가 포함된다. `BasedOnSettings` 값은 기존 설정을 덮지 않고, resolve 시점에 현재 설정값으로 해석되어야 한다.

| Infrastructure | -   | Configure/Set       | Resolve                     | Log           | BlackboxSession.TryFreeze | BlackboxHandle.RestartDefaultSession |
| -------------- | --- | ------------------- | --------------------------- | ------------- | -------------- | ---------------------- |
|                | -   |                     |                             |               |                |                        |
|                | -   |                     |                             |               |                |                        |
| Ready          | -   | `[settings 갱신]`    | `[explicit/default 해석]`    | `[logger 호출]` | **Printed**    | -                      |
| Printed        | -   | `[settings 갱신]`    | `[explicit/default 해석]`    | `[logger 호출]` | -              | **Ready**              |

## 4. 로그 문맥 단위

### 4-1. LogContext

`LogContext`는 한 `Blackbox` 안에서 thread별 로그 버퍼와 scope 생명주기를 관리한다.

| LogContext    | -   | EnqueueLog            | OpenScope              | CloseScope     | <                     | <                         | ResolveWith          | TryMergeScope           | <             | GetLogs        |
| ------------- | --- | --------------------- | ---------------------- | -------------- | --------------------- | ------------------------- | -------------------- | ----------------------- | ------------- | -------------- |
|               | -   |                       |                        | CloseNewest    | CloseOuterFirst       | CloseFromDifferentThread  | ValidTarget          | MergeAdjacent           | MergeNonAdjacent |                |
|               | -   |                       |                        |                |                       |                           |                      |                         |               |                |
| Ready         | -   | `[log 저장]`          | **ScopeOpen**          | _Warning_      | _Warning_             | _Warning_                 | -                    | -                       | -             | `[valid logs]` |
| ScopeOpen     | -   | `[scoped log 저장]`   | **ScopeOpen**          | **Ready**      | **Ready** + auto close | `[requested close]` + warning | `[tag resolve]` | `[merge]`               | -             | `[valid logs]` |
| RingBufferFull | -  | `[old log overwrite]` | `[old log overwrite 가능]` | `[available range]` | `[available range]` | `[available range]`       | `[available log only]` | `[available range only]` | -             | `[recent logs]` |

### 4-2. Blackbox

`Blackbox`는 owner 하나에 대한 실제 기록 규칙을 적용한다.

| Blackbox           | -   | Write             | Scope          | ExertMessage           | <                    | <                         | Exert                 | <                    | <                         | GetLogs              | GetLogsByContext     |
| ------------------ | --- | ----------------- | ------------------- | ---------------------- | -------------------- | ------------------------- | --------------------- | -------------------- | ------------------------- | -------------------- | -------------------- |
|                    | -   |                   |                     | OtherNull              | OtherSelf            | OtherPeer                 | OtherNull             | OtherSelf            | OtherPeer                 | MultiContext         |                      |
|                    | -   |                   |                     |                        |                      |                           |                       |                      |                           |                      |                      |
| Ready              | -   | `[self log 저장]` | `[open scope 저장]` | _ArgumentNullException_ | `[self interaction]` | `[two-sided interaction]` | _ArgumentNullException_ | `[self exert handle]` | `[interaction + handle]` | `[sequence 정렬]`   | `[context별 logs]`   |
| Printed            | -   | -                 | -                   | -                      | -                    | -                         | -                     | -                    | -                         | `[snapshot]`         | `[snapshot]`         |
| OwnerReferenceLost | -   | `[fallback owner string]` | `[fallback owner string]` | `[fallback owner string]` | ^ | ^ | `[fallback owner string]` | ^ | ^ | `[fallback owner string]` | `[fallback owner string]` |

## 5. 외부 진입점 단위

### 5-1. BlackboxHandle

`BlackboxHandle`은 외부 코드가 사용하는 public API 표면이다.

이 표는 기본 기록 축과 `UseBlackbox`가 꺼진 fallback 검증 축을 함께 가진다. `UseBlackboxOff` 행은 런타임 설정이 꺼졌을 때 public API가 원래 메시지나 default handle을 반환하고 실제 기록을 만들지 않는지 확인한다.

`When(Func<bool>)`는 기록 활성 여부와 관계없이 조건 함수를 한 번 평가하고 null 조건 함수를 거부한다. 아래 `When` 칸의 `-`는 기록이나 유효 핸들을 만들지 않는다는 뜻이며, 조건 함수의 평가를 생략한다는 뜻은 아니다. 비활성 기록에서 `Of(null)`은 subject를 검사하거나 등록하지 않고 default handle을 반환한다.

| BlackboxHandle    | -   | Of                  | <                      | Construct                 | When              | <       | Dispose          | Write          | Scope       | ExertMessage           | <                     | Exert          | <                     |
| ----------------- | --- | ------------------- | ---------------------- | ------------------------- | ----------------- | ------- | ---------------- | -------------- | ---------------- | ---------------------- | --------------------- | --------------- | --------------------- |
|                   | -   | ValidSubject        | NullSubject            |                           | True              | False   |                  |                |                  | OtherNull              | OtherPeer             | OtherNull       | OtherPeer             |
|                   | -   |                     |                        |                           |                   |         |                  |                |                  |                        |                       |                 |                       |
| UseBlackboxOff    | -   | **Default/Invalid** | **Default/Invalid**    | -                         | -                 | -       | -                | `[message 반환]` | -              | `[message 반환]`        | `[message 반환]`       | -               | -                     |
| Default/Invalid   | -   | X                   | X                      | -                         | -                 | -       | -                | `[message 반환]` | -              | `[message 반환]`        | `[message 반환]`       | -               | -                     |
| Valid             | -   | `[same owner handle]` | _ArgumentNullException_ | `[ctor scope + cached handle]` | `[valid 유지]` | **ConditionalSkipped** | `[disposed log]` | `[self log]` | `[scope handle]` | _ArgumentNullException_ | `[two-sided interaction]` | _ArgumentNullException_ | `[exert handle]` |
| ConditionalSkipped | -  | X                   | X                      | -                         | X                 | -       | -                | `[message 반환]` | -              | `[message 반환]`        | `[message 반환]`       | -               | -                     |
| Printed           | -   | `[handle 생성 가능]` | _ArgumentNullException_ | -                         | `[condition 반영]` | ^       | -                | `[message 반환]` | -              | `[message 반환]`        | `[message 반환]`       | -               | -                     |

| BlackboxHandle    | -   | WriteError        | <                  | <                            | CrashExport                       | Export       | <               | RestartDefaultSession              |
| ----------------- | --- | ----------------- | ------------------ | ---------------------------- | --------------------------------- | ------------ | --------------- | ----------------------- |
|                   | -   | ErrorTargetsEmpty | ErrorTargetsPresent | ExceptionHandlingCrashExport |                                   | FirstExport   | DuplicateExport |                         |
|                   | -   |                   |                    |                              |                                   |              |                 |                         |
| Default/Invalid   | -   | `[message 반환]`   | `[message 반환]`    | `[message 반환]`              | `[message 반환 + warning 가능]`     | `[warning]`  | `[warning]`     | `<BlackboxHandle.RestartDefaultSession>` |
| Valid             | -   | `[error log]`     | `[error log + tag]` | `<CrashExport>`              | `[error log + stack trace + export]` | [export / 기록 유지] | [반복 export] | `<BlackboxHandle.RestartDefaultSession>` |
| Printed           | -   | `[message 반환]`   | `[message 반환]`    | `[message 반환]`              | `[duplicate export 제한]`           | [보관 로그 export] | [반복 export] | [새 기본 세션 / 명시적 세션 유지] |

## 6. export 단위

### 6-1. export tools

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

### 6-2. exporters

`TxtExporter`와 `HtmlExporter`는 같은 export graph를 서로 다른 파일 형식으로 출력한다.

| Exporter            | -   | Export          | <               | <      |
| ------------------- | --- | --------------- | --------------- | ------ |
|                     | -   | Normal          | Crash           | OpenLog |
|                     | -   |                 |                 |        |
| LogDirectoryMissing | -   | _InvalidOperationException_ | ^       | X      |
| TxtReady            | -   | `[txt file 생성]` | `[CRASH prefix file 생성]` | `[파일 자동 열기 시도]` |
| HtmlReady           | -   | `[html file 생성]` | `[CRASH prefix file 생성]` | `[파일 자동 열기 시도]` |

## 7. 통합 흐름

통합 테스트는 개별 객체 표에서 검증한 동작을 바탕으로 대표적인 사용 흐름을 확인한다.

| Integration        | -   | Run                                      |
| ------------------ | --- | ---------------------------------------- |
|                    | -   |                                          |
|                    | -   |                                          |
| SingleOwnerHistory | -   | `[Construct -> Scope -> Write -> Export]` |
| TwoOwnerInteraction | -  | `[Exert -> target Scope -> Export 연결 확인]` |
| TagTargetFlow      | -   | `[Write.With / Scope.With -> source/target 표시 확인]` |
| ErrorFlow          | -   | `[WriteError / CrashExport -> error log + export 확인]` |
| RestartDefaultAndRerun      | -   | `[Export -> RestartDefaultSession -> 새 실행]`       |
| RingBufferLimit    | -   | `[MaxLogCount 초과 시 최근 로그 유지]`     |

## 8. Sample CLI

샘플 CLI는 라이브러리 내부 상태와 별도로 실행 방식, 종료 코드, 콘솔 스트림과 출력 파일을 검증하는 축이다. 테스트 주석의 `Sample CLI / 실행 방식 x 입력` 표기는 아래 행에 대응한다.

| 실행 방식 | 입력 | 검증할 결과 |
| --- | --- | --- |
| One-shot | Help/Exit | 표준 입력이 열려 있어도 종료 코드 0으로 끝나며 프롬프트·오류·출력 디렉터리를 만들지 않는다. help는 명령 목록을 출력한다. |
| One-shot | Scenario | write/exert/tag/exception과 숫자 별칭 1/2/3/4가 종료 코드 0으로 끝난다. 프롬프트 없이 완료 메시지와 경로를 출력하고 시나리오 디렉터리에 HTML 파일 하나를 만든다. exception은 예상한 오류 메시지와 CRASH 파일을 남긴다. |
| One-shot | InvalidInput | 알 수 없는 명령이나 불필요한 추가 인자는 종료 코드 2와 stderr의 사용법 안내로 처리한다. stdout은 비어 있으며 출력 디렉터리를 만들지 않는다. |
| One-shot | OutputFailure | 출력 디렉터리를 만들 수 없으면 종료 코드 1과 stderr의 실행 실패 메시지를 반환한다. 완료 메시지나 프롬프트는 출력하지 않는다. |
| Interactive | Empty/Invalid/Help/Exit | 빈 입력과 잘못된 명령 뒤에도 프롬프트를 유지한다. help를 처리하고 exit에서 종료 코드 0으로 끝난다. 시나리오 출력 디렉터리는 만들지 않는다. |
| Interactive | OutputFailure | 출력 오류를 stderr로 알린 뒤에도 help와 exit를 처리한다. 실패한 실행을 완료로 표시하지 않으며 exit의 종료 코드는 0이다. |
