# 목차

1. Blackbox 구조
2. 상세 구현 문서
3. 핵심 구성 요소
4. 기록 파이프라인
5. 스레드별 기록 안전성
6. 구조적 의도
7. 구현 시 고려사항

---

# 1. Blackbox 구조

Blackbox는 한 객체의 실행 맥락을 기록하는 내부 엔진을 중심으로, 객체 간 상호작용과 스코프 흐름을 같은 로그 체계 안에 담는 프레임워크이다.

이 프레임워크의 구현은 크게 열 요소로 나뉜다.

- `Blackbox`: 실제 기록 규칙을 적용하고 객체별 로그 흐름을 관리하는 중심 객체
- `BlackboxRuntime`: 전역 id, 상호작용 id, sequence를 발급하는 실행 상태
- `LogContext`: 스레드별 로그 버퍼, 열린 스코프 목록, 상호작용 병합 탐색을 맡는 실행 문맥
- `LogData`: 한 줄 로그가 가지는 정보 단위
- `LogFormatter`: `LogData`를 텍스트/HTML 출력에서 읽을 수 있는 문자열로 변환하는 포맷팅 도구
- `BlackboxRegistry`: 객체와 `Blackbox`를 연결하는 전역 조회 구조
- `BlackboxHandle`: 외부에서 기록 기능을 호출하는 진입점
- `ScopeHandle`: 열린 스코프를 닫는 `IDisposable` 핸들
- `ExertHandle`: 객체 간 상호작용 후 받는 쪽 스코프 병합을 시도하는 `IDisposable` 핸들
- `HandleManager<T>`: 값 타입 핸들의 중복 `Dispose()`를 막는 id 생명주기 관리자

이 구조의 핵심은 기록의 중심, 실행 문맥, 출력 포맷팅, 외부 사용 인터페이스, `Dispose()` 후처리 단위를 분리하는 데 있다. 실제 기록 규칙은 `Blackbox`에 모이고, 스레드마다 달라지는 로그 버퍼와 스코프 상태는 `LogContext`에 분리되며, 출력 문자열 조립은 `LogFormatter`가 맡는다. 외부 코드는 `BlackboxHandle`을 통해 이 구조를 간접적으로 사용한다. `ScopeHandle`과 `ExertHandle`은 이전처럼 하나의 action 기반 핸들이 아니라, 각자 닫아야 할 후처리 의미를 별도 타입으로 가진다.

---

# 2. 상세 구현 문서

이 문서는 Blackbox의 전체 구조와 대표 흐름을 설명한다. 여러 객체를 함께 읽어야 하는 세부 구현은 아래 문서에서 따로 다룬다.

- `02.1-Tag-Flow.md`: `.With(...)`로 연결되는 원본 로그, 대상 로그, 표시 정책, 치환 자리 처리 흐름
- `02.2-Export-Pipeline.md`: 출력 그래프 수집, 스코프 표시 변환, 텍스트/HTML 출력 흐름
- `02.3-Handle-Lifecycle.md`: `ScopeHandle`, `ExertHandle`, `HandleManager<T>`의 생명주기와 중복 `Dispose()` 방지

---

# 3. 핵심 구성 요소

## 3-1. Blackbox

`Blackbox`는 한 객체에 대응되는 기록 엔진이다. 기록의 주체, 스코프 규칙, 상호작용 규칙, 전체 출력 순서를 조율한다.

이 구현에서 중요한 점은 `Blackbox`가 단순한 문자열 수집기가 아니라는 것이다. `Blackbox`는 다음과 같은 책임을 함께 가진다.

- 기록의 주체 유지
- 기록 요청의 현재 스레드 `LogContext` 전달
- 스코프 id 발급과 open 로그 기록
- 객체 간 호출 관계의 양쪽 로그 반영
- 태그 대상 로그를 상대 객체 쪽에 보조 기록으로 반영
- 여러 스레드 로그의 전역 sequence 기준 재정렬

보유 정보:

- `Owner` / `OwnerString` / `Id`: 기록 대상 객체, 출력용 이름, Blackbox 내부 식별자
- `_strongOwner` / `_weakOwner` / `_ownerDescription`: owner 보관 방식과 참조 손실 시 사용할 설명 문자열
- `_logContext`: 현재 스레드에 대응되는 `LogContext`
- `_scopeId`: 이 owner 안에서 스코프 open/close 쌍을 연결하기 위한 번호

제공 서비스와 이유:

- `Write(...)`: 한 객체 내부의 진행 상황 기록 후, 필요한 경우 태그 대상을 붙일 수 있는 값 반환
- `Scope(...)`: 스코프 id를 발급하고 open 로그를 남긴 뒤 `ScopeHandle` 반환
- `Tag(...)`: 원본 로그가 `.With(...)`로 연결한 대상 쪽에 태그 로그 저장
- `ExertMessage(...)`: 객체 간 상호작용을 양쪽에 한 줄 기록으로 저장
- `Exert(...)`: 객체 간 상호작용을 기록하고, `Dispose()` 시 병합을 시도할 `ExertHandle` 반환
- `GetLogsByContext(...)` / `GetLogs(...)`: 여러 스레드 context 로그를 모으고 전역 sequence 기준으로 정렬

즉, `Blackbox`는 기록 저장소 자체라기보다 객체별 기록 규칙과 전체 출력 순서를 조율하는 중심 엔진이다.

## 3-2. BlackboxRuntime

`BlackboxRuntime`은 `Blackbox` 인스턴스들이 공유하는 전역 번호 발급 지점이다.

객체 id, 상호작용 id, 로그 sequence는 특정 owner 하나의 지역 상태가 아니다. 여러 `Blackbox`가 같은 실행 안에서 함께 기록되므로, 이 값들은 `BlackboxRuntime`이 원자적으로 증가시킨다. 이렇게 해야 나중에 여러 owner와 여러 스레드 문맥에서 수집한 로그를 하나의 순서로 다시 정렬할 수 있다.

보유 정보:

- `CurrentSequence`: 지금까지 발급된 마지막 sequence
- 내부 blackbox id / interaction id / sequence counter: 객체, 상호작용, 로그 순서를 나누어 관리하는 전역 counter

제공 서비스와 이유:

- `GetNextBlackboxId()`: owner별 `Blackbox` 식별자 발급
- `GetNextInteractionId()`: 객체 간 상호작용을 양쪽 로그에서 연결할 번호 발급
- `GetNextSequence()`: 전체 로그 발생 순서 정렬을 위한 번호 발급
- `Reset()`: `ForceReset()` 시 실행 상태 초기화

## 3-3. LogContext

`LogContext`는 한 `Blackbox` 안에서 스레드별 로그 버퍼와 스코프 상태를 나누어 보관하는 내부 구조이다.

각 `LogContext`는 ring buffer 형태의 로그 버퍼, 스레드 metadata, 현재 열린 스코프 목록을 가진다. `Blackbox`는 `ThreadLocal<LogContext>`를 통해 현재 스레드에 대응되는 문맥을 얻고, `LogData` 생성과 저장은 그 문맥 안에서 수행한다.

현재 구현에서 스코프는 두 층으로 처리된다. 기록 시점에는 `LogContext`가 `_openScopes` 목록을 사용해 열린 스코프의 생명주기를 관리하고, close 순서가 어긋났을 때 같은 스레드의 더 나중 스코프를 자동으로 닫는다. 출력 시점에는 `ResolveScopeDepths`가 open/close 로그를 다시 읽어 표시 깊이를 계산한다. 따라서 실행 중 스코프 상태와 출력 표시 깊이는 서로 다른 책임이다.

상호작용 병합도 이 문맥 안의 ring buffer를 대상으로 수행된다. `TryMergeScope(...)`는 같은 owner와 `InteractionId`를 가진 상호작용 로그 바로 다음에 open 스코프가 있는지 찾고, 조건이 맞으면 상호작용 로그를 invalid로 표시하고 open 스코프에 그 정보를 합친다.

태그 연결은 원본 로그가 저장된 문맥에서 시작된다. `Write(...)`나 `Scope(...)`로 만든 원본 로그에 `.With(...)`가 이어지면, `LogContext`는 원본 로그의 대상 목록을 확정하고 필요한 대상 `Blackbox`에 별도의 태그 로그를 남긴다. 원본 로그 메시지의 치환 자리 처리와 대상 참조 문자열 조립은 출력 시점에 `LogFormatter`가 처리한다. 이때 대상에 남는 로그의 상세도는 `TargetTypes` 정책으로 조정된다.

보유 정보:

- `_logBuffer` / `_currentLogIndex`: 고정 크기 ring buffer와 최근 기록 위치
- `_threadId` / `_threadName`: 이 문맥을 만든 스레드의 출력용 metadata
- `_openScopes`: 아직 닫히지 않은 스코프의 id, method name, alive 상태

제공 서비스와 이유:

- `OpenScope(...)`: 열린 스코프 목록에 스코프를 등록하고 open 로그 저장
- `CloseScope(...)`: 스코프 close 로그 저장, 같은 스레드의 out-of-order close 보정, cross-thread close warning
- `EnqueueLog(...)`: 전달받은 스코프 id, 스코프 종류, 상호작용 정보를 `LogData`로 고정하고 ring buffer에 저장
- `ResolveWith(...)`: 원본 로그의 태그 대상 목록을 확정하고 대상 로그를 생성
- `RenderMessage(...)`: 태그 치환 자리가 반영된 메시지를 `LogFormatter`를 통해 반환
- `TryMergeScope(...)`: 직전 상호작용과 이어지는 open 스코프를 하나의 출력 흐름으로 합칠 수 있는지 판단
- `GetLogs(...)`: ring buffer 안의 유효한 로그 전달

이 분리가 필요한 이유는 같은 owner 객체가 여러 스레드에서 기록될 수 있기 때문이다. 서로 다른 스레드가 같은 owner 객체를 기록하더라도 각 스레드의 ring buffer와 thread metadata는 독립적으로 유지되고, 출력할 때 전역 sequence를 기준으로 다시 합쳐진다.

## 3-4. LogData

`LogData`는 `Blackbox`가 저장하는 최소 기록 단위이다. 한 항목에는 유효성, 메시지, 시각, 메서드 이름, 스코프 id, 스코프 종류, 상호작용 상대, 상호작용 번호가 함께 들어간다.

현재 구현에서는 여기에 전역 `Sequence`, `ThreadId`, `ThreadName`도 함께 저장한다. 이 정보들은 단순 출력용 부가 정보가 아니라, 여러 스레드에서 수집된 로그를 나중에 다시 순서대로 읽고, 출력 시점에 스코프 깊이를 계산하기 위한 기준이다.

또한 `ScopeType`을 통해 일반 로그와 스코프 로그를 같은 자료형 안에서 다루므로, 기록 구조를 별도의 타입 여러 개로 쪼개지 않고도 흐름을 유지할 수 있다. 태그 기능은 같은 `LogData` 안에 `Tags`, `TaggedBy`, `TagTargetTypes`를 추가로 저장하여, 원본 로그와 대상 로그가 같은 기록 단위 안에서 출력용 연결 정보를 공유하게 한다.

보유 정보:

- `Owner` / `IsValid` / `Message` / `Time` / `MethodName`: 한 줄 로그가 유효한지, 어느 객체에서 언제 어떤 의미로 발생했는지 나타내는 기본 정보
- `ScopeId` / `ScopeType` / `ScopeDepth`: open/close 로그 연결과 출력 표시 깊이를 위한 값
- `ExertedBy` / `ExertingTo` / `InteractionId`: 객체 간 상호작용 방향과 연결 번호
- `Tags` / `TaggedBy` / `TagTargetTypes`: 원본 로그의 관련 대상 목록과 대상 로그의 표시 정책
- `Sequence` / `ThreadId` / `ThreadName`: 여러 문맥에서 모은 로그를 순서와 스레드 기준으로 다시 읽기 위한 metadata

제공 서비스와 이유:

- 생성자: 로그 생성 시점의 기록 값을 하나의 데이터로 고정
- `internal set` 필드: 병합, 태그 해석, 출력 변환 시점에 `Message`, `InteractionId`, 상호작용 방향, 태그 정보, 유효성, 스코프 표시 정보를 조정
- `ToString(scopeDepth)`: 출력 단계에서 계산한 깊이를 받아 `LogFormatter`를 통해 텍스트 로그 한 줄 포맷 제공

## 3-5. LogFormatter

`LogFormatter`는 저장된 `LogData`를 사람이 읽는 문자열로 바꾸는 포맷팅 도구이다.

기록 시점에는 `LogData`가 가능한 한 구조화된 값으로 정보를 보관한다. 실제 텍스트 출력, HTML 출력, 태그 치환 자리 해석, 스코프 메서드 표시 조립은 출력 시점에 가까운 `LogFormatter`에서 처리한다. 이 분리 덕분에 `LogData`는 기록 값을 보관하는 역할에 집중하고, 출력 형식이 바뀌어도 기록 저장 경로 전체가 흔들리지 않는다.

제공 서비스와 이유:

- `RenderMessage(...)`: 태그 치환 자리와 대상 표시 정책을 반영한 메시지 생성
- `RenderTextLine(...)`: txt export와 `LogData.ToString(...)`에서 사용할 한 줄 문자열 생성
- `RenderMethodLabel(...)`: 스코프 종류에 따라 메서드 표시를 open/close/step 형태로 생성
- `Arrow(...)` / `TagRef(...)`: 상호작용 id와 대상 참조를 일관된 문자열로 표시

## 3-6. BlackboxRegistry

`BlackboxRegistry`는 객체와 `Blackbox`를 연결하는 전역 조회 지점이다.

동일한 객체에 대해 여러 번 기록을 남겨도 항상 같은 `Blackbox`를 찾아야 로그가 하나의 맥락으로 이어진다. registry는 이 연결을 보장하는 구조이다. 이 구조 덕분에 외부에서는 별도 등록 과정 없이 `BlackboxHandle.Of(subject)`를 호출하는 것만으로 기록을 시작할 수 있다. 내부에서는 같은 객체 identity를 기준으로 대응되는 `Blackbox`를 재사용한다.

보유 정보:

- `_subjects`: 대상 객체 identity와 `Blackbox` 사이의 연결을 저장하는 `ConditionalWeakTable`
- `_lock`: debug/test 성격의 count와 reset 작업을 안전한 시점에서 보호하기 위한 동기화 객체

제공 서비스와 이유:

- `GetBlackbox(...)`: 대상 객체에 대응되는 `Blackbox` 조회 또는 생성
- `Contains(...)` / `Count()`: registry 상태의 테스트와 디버그 관찰
- `ForceReset()`: 테스트나 반복 실험에서 registry, runtime, 핸들 id 상태 초기화

## 3-7. BlackboxHandle

`BlackboxHandle`은 외부 코드가 실제로 마주하는 진입점이다. 그러나 구현자 관점에서는 새로운 기록 구조라기보다, 앞선 내부 구조를 감싸는 얇은 forwarding 계층으로 이해하는 편이 적절하다.

이 타입의 역할은 다음과 같다.

- 대상 객체에 대응되는 `Blackbox`를 필요 시 조회한다.
- 일반 기록, 스코프 기록, exert 기록 호출을 내부 `Blackbox`로 전달한다.
- 일반 기록과 스코프 open 로그에 관련 대상을 나중에 붙일 수 있는 연결점을 제공한다.
- 조건부 기록, 생성 기록, 오류 기록, 출력 같은 외부 사용 편의 기능을 제공한다.
- 외부 사용 코드를 단순한 형태로 유지한다.

보유 정보:

- `_subject`: 아직 `Blackbox`를 직접 잡지 않은 핸들이 registry에서 대상 `Blackbox`를 찾기 위한 원본 객체
- `_blackbox`: `Construct(..., out handle)` 이후처럼 이미 확인된 `Blackbox`를 반복 조회 없이 사용하기 위한 캐시
- `Owner` / `OwnerString` / `Id` / `IsValid`: 내부 `Blackbox` 상태를 외부 사용자가 확인할 수 있도록 제공하는 forwarding 속성
- `LogDirectory` / logger / 출력 option / `MaxLogCount` / `StrongReference` / `TagTargetTypes`: `Infrastructure` 설정을 외부 API에서 조정하기 위한 정적 속성

제공 서비스와 이유:

- `Configure(...)` / `ForceReset()` / `Of(...)`: 기록 환경 준비와 대상 객체 핸들 획득
- `Construct(...)` / `Dispose(...)` / `When(...)`: 생성·해제 기록과 조건부 기록 진입 패턴 표현
- `Write(...)` / `Scope(...)`: 자기 객체의 일반 기록과 스코프 기록
- `ExertMessage(...)` / `Exert(...)`: caller 기준의 객체 간 상호작용 방향 명시
- `Scope(...).With(...)`: 스코프 open 로그에 관련 대상 연결
- `WriteError(...)` / `CrashExport(...)` / `Export(...)`: 오류 기록과 관련 대상 연결, 최종 출력 처리

따라서 `BlackboxHandle`의 가치는 기능을 많이 가지는 데 있지 않고, 외부 사용 방식과 내부 기록 구조를 분리하는 데 있다.

## 3-8. ScopeHandle

`ScopeHandle`은 열린 스코프를 닫는 `IDisposable` 핸들이다.

`BlackboxHandle.Scope(...)`는 open 로그를 남긴 뒤 `ScopeHandle`을 반환한다. 핸들은 스코프를 연 `LogContext`와 스코프 id를 보관하고, `Dispose()` 시점에 `LogContext.CloseScope(...)`로 닫기 요청을 전달한다. 또한 스코프 open 로그도 일반 로그처럼 관련 대상을 가질 수 있으므로, `ScopeHandle.With(...)`는 open 로그에 대상 정보를 붙인 뒤 같은 스코프 핸들을 다시 돌려준다.

보유 정보:

- `_id`: `HandleManager<ScopeHandle>`에서 발급한 복사 안전 핸들 id
- `_createdThreadId`: 핸들이 만들어진 스레드 id
- `_context`: close 요청을 전달할 `LogContext`
- `_scopeId`: open/close 로그를 연결할 스코프 id
- `_tagHandle`: 스코프 open 로그에 `.With(...)` 대상을 붙이기 위한 보조 값

제공 서비스와 이유:

- `Dispose()`: `using` 블록 종료 시점에 대응 스코프 close 요청
- `With(...)`: 스코프 open 로그에 관련 대상을 붙이고 기존 스코프 생명주기 유지
- `IsAlive` / `IsDisposed` / `ScopeId`: 핸들 생명주기와 스코프 id 관찰
- thread mismatch warning: 생성된 스레드와 다른 스레드에서 dispose되면 로그 구조가 불안정할 수 있음을 경고
- 기본값/해제된 핸들 무시: 조건부 기록이나 중복 dispose 상황에서 호출부 단순화

## 3-9. ExertHandle

`ExertHandle`은 객체 간 상호작용 뒤에 받는 쪽 스코프 병합을 시도하는 `IDisposable` 핸들이다.

`Blackbox.ExertMessage(...)`는 보내는 쪽과 받는 쪽에 같은 `InteractionId`를 가진 로그만 남긴다. `Blackbox.Exert(...)`는 같은 상호작용 로그를 먼저 남긴 뒤 `ExertHandle`을 반환하고, `Dispose()` 시점에 받는 쪽 `LogContext`에서 같은 상호작용 직후 열린 스코프를 찾아 병합을 시도한다.

반환된 `ExertHandle`을 닫지 않으면 결과 로그는 한 줄 상호작용 기록으로 남는다. 다만 이 경우 `ExertHandle` 생명주기가 완료되지 않으므로, 단순 연결점만 남기는 사용 흐름에서는 `ExertMessage(...)`를 사용한다.

보유 정보:

- `_id`: `HandleManager<ExertHandle>`에서 발급한 복사 안전 핸들 id
- `_createdThreadId`: 핸들이 만들어진 스레드 id
- `_targetContext`: 받는 쪽 상호작용 로그가 저장된 `LogContext`
- `_interactionId`: 양쪽 로그를 연결하는 상호작용 번호

제공 서비스와 이유:

- `Dispose()`: 받는 쪽의 직후 open 스코프에 상호작용 정보를 병합
- `IsAlive` / `IsDisposed`: 핸들 생명주기 관찰
- 대상 문맥 병합: 생성 시점에 고정한 대상 문맥 안에서만 병합을 시도

## 3-10. HandleManager

`HandleManager<T>`는 `ScopeHandle`과 `ExertHandle`의 id 생명주기를 관리한다.

핸들이 `public readonly struct`이면 값을 복사할 수 있다. 이때 복사본마다 dispose 후처리가 반복되면 close 로그나 merge 처리가 중복될 수 있다. `HandleManager<T>`는 발급한 id를 중앙에서 alive/disposed 상태로 관리하여, 같은 핸들 값이 복사되어도 첫 dispose만 성공하고 이후 dispose는 무시되도록 한다.

제공 서비스와 이유:

- `GetHandleId()`: 새 핸들 id 발급
- `TryDisposeHandleId(...)`: 살아 있는 id만 한 번 disposed 상태로 전환
- `IsAlive(...)`: 핸들 관찰 속성의 기준 상태 제공
- `WarnIfThreadMismatch(...)`: 생성 스레드와 dispose 스레드가 다를 때 warning 출력
- `Reset()`: `BlackboxRegistry.ForceReset()` 시 테스트나 반복 실험용 안전 지점에서 핸들 id 상태 초기화

---

# 4. 기록 파이프라인

기록 파이프라인은 앞에서 설명한 객체들이 실제 호출 순서 안에서 어떻게 연결되는지를 보여준다. 기본 기록은 한 객체 내부 기록을 남기는 흐름이고, 스코프와 exert는 그 위에 실행 범위와 객체 간 연결을 덧붙이는 확장 흐름이다.

## 4-1. 일반 기록 흐름

가장 기본적인 흐름은 한 객체가 자기 자신의 상태나 진행 상황을 남기는 경우이다.

1. 외부 코드는 `BlackboxHandle.Of(subject)`로 기록 진입점을 얻는다.
2. `Write` 계열 호출이 들어오면, 핸들은 대상 객체에 대응되는 `Blackbox`를 조회한다.
3. `Blackbox`는 현재 스레드의 `LogContext`를 얻는다.
4. `Blackbox`는 메시지, 메서드 이름, sequence 같은 기록 재료를 현재 문맥에 전달한다.
5. `LogContext`는 전달받은 기록 값을 `LogData`로 만들고, 자신의 ring buffer에 저장한다.

이 흐름에서 중요한 점은 외부 코드가 로그 저장소를 직접 다루지 않는다는 것이다. 외부는 기록 의도만 전달하고, 실제 저장 형식과 문맥 구성은 내부 구조가 맡는다.

기록 직후 `.With(...)`가 붙으면 같은 원본 로그에 대상 목록이 확정된다. 이 경우 `LogContext.ResolveWith(...)`가 원본 로그의 `Tags`를 채우고, 대상 객체가 원본과 다른 `Blackbox`라면 대상 쪽에도 태그 로그를 추가한다. 이 태그 로그는 `Exert(...)`처럼 호출 구간을 여는 기록이 아니라, 원본 로그와 관련 대상을 함께 읽기 위한 보조 연결이다.

## 4-2. 스코프 기록 흐름

스코프 기록은 한 단계의 시작과 끝을 블록 단위로 남기는 흐름이다.

1. `Scope`, `Construct`처럼 스코프를 여는 API가 호출되면 `Blackbox`는 현재 스레드의 `LogContext`를 얻는다.
2. `Blackbox`는 새 스코프 id를 발급하고, `LogContext.OpenScope(...)`로 open 로그를 저장한다.
3. `Blackbox`는 해당 문맥과 스코프 id를 담은 `ScopeHandle`을 반환한다.
4. 이후 `using` 블록이 끝나면 `ScopeHandle.Dispose()`가 호출된다.
5. `ScopeHandle`은 `LogContext.CloseScope(...)`로 닫기 요청을 전달한다.
6. `LogContext`는 같은 문맥에 동일한 스코프 id를 담은 close 로그를 저장한다.

이 파이프라인에서 핵심은 '스코프를 연 호출자'와 '스코프를 닫는 시점'이 분리되어 있으면서도, 둘이 같은 `LogContext`와 스코프 id로 다시 연결된다는 점이다. 이 분리 덕분에 기록 구조가 코드의 블록 구조를 따라갈 수 있다.

부모 스코프가 자식 스코프보다 먼저 닫히면 `LogContext.CloseScope(...)`가 열린 더 나중 스코프를 확인한다. 같은 스레드에서 닫힌 경우에는 더 나중 스코프를 먼저 자동 close하고 warning을 남긴다. 다른 스레드에서 닫힌 경우에는 warning을 남기되 다른 스레드의 열린 스코프를 임의로 자동 close하지 않는다.

스코프 깊이 계산과, 스코프 내부에 별도 하위 기록이 없을 때 open/close 쌍을 step 로그처럼 보이게 정리하는 처리는 저장 시점이 아니라 출력 단계에서 수행된다. 출력 쪽 `ResolveScopeDepths`와 `FlattenSteps`는 원본 로그를 보존한 채 출력용 목록을 만든다.

## 4-3. exert 기록 흐름

exert 기록은 한 객체가 다른 객체와 상호작용하는 흐름을 양쪽에 동시에 남기는 구조이다.

1. 한 객체가 다른 객체에 작업을 넘기면 핸들은 양쪽 객체의 `Blackbox`를 찾는다.
2. `Blackbox`는 같은 `InteractionId`를 공유하는 로그를 양쪽에 기록한다.
3. 보내는 쪽 로그에는 `ExertingTo`가, 받는 쪽 로그에는 `ExertedBy`가 저장된다.
4. `ExertMessage(...)`는 여기서 끝나며, 두 로그는 한 줄 상호작용 기록으로 남는다.
5. `Exert(...)`는 같은 기록을 남긴 뒤 `ExertHandle`을 반환한다.
6. 반환된 `ExertHandle`을 닫으면, 생성 시점에 고정한 대상 문맥에서 같은 `InteractionId`의 상호작용 로그 바로 다음에 열린 스코프가 있는지 찾는다.
7. 조건이 맞으면 상호작용 로그를 invalid로 표시하고, 이어지는 open 스코프에 상호작용 정보를 병합한다.

이 구조의 장점은 한 객체의 로그만 읽어도 상호작용이 있었다는 사실을 알 수 있고, 다른 객체의 대응 로그까지 자연스럽게 따라갈 수 있다는 데 있다. 또한 병합 시점을 `ExertHandle.Dispose()`에 묶기 때문에, 핸들이 닫힌 뒤 새로 열린 스코프를 뒤늦게 같은 호출 범위로 합치지 않는다.

여기서 대상 문맥은 받는 쪽 상호작용 로그가 실제로 저장된 문맥이다. `TryMergeScope(...)`는 이 문맥 안에서 두 로그가 바로 이어지는 경우만 병합한다. 따라서 `Task.Run`처럼 상호작용과 받는 쪽 처리 스코프가 서로 다른 스레드 문맥에 나뉘면 병합하지 않는 것이 정책이다.

## 4-4. 출력 포맷팅 흐름

저장된 로그는 출력 단계에서 다시 읽기 좋은 문자열로 변환된다.

1. `BlackboxHandle.Export(...)`는 기준 `Blackbox`의 로그와 Exert 연결을 따라 포함할 peer 로그를 수집한다. 집중 출력은 `ExertedBy` 방향을 따라가고, 전체 출력은 `ExertingTo` 방향도 함께 따라간다.
2. 출력 도구는 `ResolveScopeDepths`와 `FlattenSteps`로 스코프 표시 깊이와 빈 스코프 표시 방식을 정리한다.
3. `TxtExporter`는 각 `LogData`를 `LogFormatter.RenderTextLine(...)` 경로로 한 줄 문자열로 만든다.
4. `HtmlExporter`는 `LogFormatter.RenderMethodLabel(...)`, `RenderMessage(...)`, `Arrow(...)`를 사용해 같은 기록 값을 HTML 구조에 맞게 표시한다.
5. 태그 치환 자리가 있는 메시지는 `LogFormatter`에서 실제 대상 참조 문자열로 치환된다.

이 흐름에서는 원본 로그의 구조 값과 출력 문자열이 분리된다. 따라서 태그 표시 정책이나 HTML/텍스트 표현이 바뀌어도 기록 생성 단계의 책임은 그대로 유지된다.

---

# 5. 스레드별 기록 안전성

Blackbox는 하나의 owner 객체가 여러 스레드에서 기록될 수 있다는 전제를 일부 반영한다. 이때 중요한 문제는 로그 한 줄의 저장 위치와 출력 순서가 스레드 사이에서 섞이지 않도록 하는 것이다.

현재 구현은 다음 방식으로 스레드별 안전성을 챙긴다.

- `Blackbox`는 `ThreadLocal<LogContext>`를 보유한다.
- 각 스레드는 자기 `LogContext` 안에 별도 ring buffer, 열린 스코프 목록, 스레드 metadata를 가진다.
- 스코프 open/close 로그는 핸들이 가진 문맥에 저장된다.
- `ScopeHandle`과 `ExertHandle`은 생성 스레드와 dispose 스레드가 다르면 warning을 남긴다.
- `Exert(...)` 병합은 받는 쪽 상호작용 로그가 저장된 대상 문맥 안에서만 시도하며, 상호작용과 받는 쪽 스코프가 서로 다른 스레드 문맥에 기록되면 두 로그를 병합하지 않는다.
- `.With(...)` 태그 해석은 원본 로그가 저장된 문맥에서 시작되고, 대상 로그는 대상 `Blackbox`의 현재 문맥에 저장된다.
- 스코프 id, interaction id, sequence는 `Blackbox` / `BlackboxRuntime`이 원자적으로 발급한다.
- `LogData`에는 `ThreadId`와 `ThreadName`이 저장되어 출력 결과에서 어느 스레드의 기록인지 확인할 수 있다.
- 모든 로그에는 전역 `Sequence`가 부여되어, 여러 문맥에서 모은 로그를 출력할 때 전체 발생 순서를 다시 맞출 수 있다.

이 구조는 스레드별 로그 버퍼와 전역 출력 순서를 분리하는 데 초점이 있다. 예를 들어 A 스레드가 스코프를 열고 B 스레드가 같은 owner 객체에 로그를 남기더라도, B 스레드의 기록은 B의 문맥에 쌓이고 출력 시점에 sequence 기준으로 다시 합쳐진다.

반대로 `LogContext` 하나를 여러 스레드가 동시에 직접 수정하는 상황은 안정적인 기본 사용 흐름으로 보지 않는다. 핸들이 생성 스레드와 다른 스레드에서 dispose되면 warning을 남기는 이유도 여기에 있다. registry reset, 설정 변경, 출력 완료 상태 같은 전역 상태 역시 안전한 시점에서 다루어야 한다. 특히 `ForceReset()`은 디버그나 테스트용 초기화에 가깝고, 다른 스레드가 동시에 기록 중일 때 호출하는 것을 전제로 하지 않는다.

---

# 6. 구조적 의도

Blackbox의 구현은 크게 여섯 가지 의도를 가진다.

## 6-1. 기록 규칙의 중심을 한 곳에 모은다

실제 기록 규칙을 `Blackbox`에 모아 두면, 외부 사용 코드가 많아져도 기록 방식은 한 곳에서 유지할 수 있다. 이 덕분에 스코프 규칙, 상호작용 규칙, 저장 규칙이 서로 다른 곳에서 중복 구현되지 않는다.

## 6-2. 스레드별 실행 문맥은 분리한다

기록 규칙은 `Blackbox`가 조율하지만, 스레드별 로그 버퍼와 열린 스코프 목록은 `LogContext`로 분리한다. 이 덕분에 같은 owner를 여러 스레드에서 기록해도 각 스레드의 기록 위치와 스레드 metadata가 독립적으로 유지된다.

## 6-3. 외부 사용 코드는 단순하게 유지한다

외부에서는 `BlackboxHandle.Of(this).Write(...)` 같은 형태로 기록을 사용한다. 내부 구조를 직접 노출하지 않으므로, 호출자는 '어디에 어떻게 저장되는가'보다 '어떤 맥락을 남길 것인가'에 집중할 수 있다.

## 6-4. `Dispose()` 후처리를 목적별 핸들로 나눈다

스코프 close와 exert merge는 둘 다 `using` / `Dispose()` 시점에 일어나지만, 의미가 다르다. 현재 구현은 이 둘을 하나의 action 핸들로 합치지 않고 `ScopeHandle`과 `ExertHandle`로 분리한다. 이 덕분에 각 핸들이 보관해야 할 정보와 dispose 정책이 명확해지고, `HandleManager`를 통해 struct 복사에 의한 중복 dispose도 막을 수 있다.

## 6-5. 로그를 나중에 읽기 좋은 구조로 만든다

Blackbox는 기록 시점부터 스코프 id, 스코프 종류, 상호작용 상대, 상호작용 번호, 태그 대상, sequence, 스레드 정보를 함께 저장한다. 스코프 깊이는 출력 시점에 계산된다. 이는 단순히 기록을 남기는 데서 멈추지 않고, 이후 출력이나 후속 분석에서 흐름을 다시 읽기 쉽게 만들기 위한 선택이다.

즉 이 프레임워크는 로그를 많이 남기는 것보다, 남겨진 로그가 구조를 가진 채 읽히도록 만드는 데 더 큰 비중을 둔다.

## 6-6. 출력 형식은 기록 데이터에서 분리한다

`LogFormatter`를 별도 도구로 두면 `LogData`가 직접 긴 문자열 규칙을 보유하지 않아도 된다. 기록 데이터는 owner, 스코프, 상호작용, 태그 같은 구조 값을 유지하고, 텍스트/HTML 출력에서 필요한 표시 문자열, 화살표, 태그 참조 문자열은 포맷팅 단계에서 조립된다. 이 분리는 출력 표현을 조정할 때 기록 저장 경로에 불필요한 변경이 번지는 것을 줄인다.

---

# 7. 구현 시 고려사항

## 7-1. 런타임 기록이 꺼진 경우

`BlackboxHandle.Of(subject)`와 주요 기록 메서드는 `UseBlackbox` 설정을 기준으로 실제 기록 여부를 결정한다. 이 값이 꺼져 있으면 기록용 핸들이 유효하지 않거나 일부 기록 호출이 조건부로 빠질 수 있다. 따라서 Blackbox를 실제 추적 도구로 사용할 때는 실행 초기 설정에서 `UseBlackbox`가 켜져 있는지 확인해야 한다.

Unity에서는 `BLACKBOX` 심볼이 없으면 기본값이 꺼진 상태로 시작한다. 이 심볼은 Unity 프로젝트에서 기본 기록 상태를 켤지 정하는 기본값 신호일 뿐이며, 심볼 유무와 관계없이 동일한 런타임 API를 사용한다. 네이티브 C#과 비 Unity 빌드에서는 별도 심볼 없이 기본 기록 상태가 켜져 있으며, 필요한 경우 `BlackboxHandle.UseBlackbox = false`나 `Configure(..., useBlackbox: UseBlackboxOption.DoNotUse)`로 끌 수 있다.

## 7-2. 출력 이후의 기록 중단

출력이 한 번 실행되면 내부 전역 상태가 출력 완료 상태로 바뀐다. 이후 기록이나 중복 출력은 의도적으로 제한된다. 같은 프로세스에서 여러 실험을 반복해야 한다면 안전한 시점에서 `BlackboxHandle.ForceReset()`으로 registry와 runtime 상태를 초기화해야 한다.

## 7-3. 전역 설정 변경 시점

`LogDirectory`, logger, 출력 option, `MaxLogCount`, `StrongReference`, `TagTargetTypes` 같은 값은 기록 구조의 동작 방식에 영향을 준다. 특히 `MaxLogCount`는 새 `Blackbox`의 `LogContext`가 만들어질 때 buffer 크기로 사용되므로, 실행 중 임의로 바꾸기보다 기록 시작 전에 설정하는 편이 안전하다.

## 7-4. 핸들 reset의 안전 지점

`BlackboxHandle.ForceReset()`은 registry와 runtime뿐 아니라 `ScopeHandle` / `ExertHandle`의 `HandleManager` 상태도 초기화한다. 이 초기화는 이전에 발급된 핸들이 더 이상 사용되지 않는 안전 지점에서만 호출해야 한다. reset 이후 이전 핸들의 dispose나 alive 조회가 의미 있게 보존되는 구조는 아니다.

## 7-5. 태그 대상 표시 정책

`.With(...)`로 연결한 대상은 원본 로그의 메시지 안에서 `%0`, `%1` 같은 치환 자리로 직접 표시할 수 있다. 메시지에서 사용하지 않은 대상은 출력 시 추가 태그 정보로 붙는다. 대상 쪽에 남는 태그 로그는 `TargetTypes` 정책에 따라 원본 이름, 상호작용 id, 원본 메시지를 어느 정도까지 보여줄지 결정한다.

대상이 없는 상황도 `.With(null)`로 직접 표현한다. `.With()`는 연결할 대상이 없다는 뜻이고, `.With(null)`은 null 대상 하나를 메시지 치환 자리나 추가 태그 정보에 남긴다는 뜻이다.
