# 0.2.0 세션 구현 검증

이전 절은 각 구현 단계 당시의 검증 기록이다. 현재 세션 수명 계약과 활성 세션 추적 검증은 마지막의 2026-10-30 절을 참고한다.

검증 환경: Windows x64, .NET SDK 9.0.320, net9.0 NUnit 테스트, netstandard2.1 라이브러리. 기존 테스트 159개를 세션별 동결·반복 출력·명시적 Label 계약에 맞게 이관하고 동시성/수명 테스트 35개를 추가했다.

## 검증 범위

- 병렬 세션의 설정·기록·크래시 격리, Task/Thread 실행 문맥 상속, 중첩 및 이미 종료된 부모 복원.
- 기본 세션 공유 세대와 명시적 세션의 호출자만 바뀌는 ForceReset, 다른 async 메서드에서 Dispose한 문맥의 한계.
- 버킷 1개에서 동시 등록/조회/정리, Subject 및 스레드별 컨텍스트 유일성.
- 핸들 등록을 중간에 정지한 상태에서 Dispose/ForceReset을 실행하는 강제 경합, 이전 세대 게시 지연, 다른 세션 핸들 보존과 슬롯 반환.
- 논리 인덱스 확인, 이전 회전의 미게시 슬롯, 늦은 게시에 의한 덮어쓰기 방지, 비단조 시퀀스, 스코프 갱신 후 상호작용 무효화 순서.
- 태그 배열 복사와 동시 출력, 링 버퍼 순환 후 음수 깊이 방지, 크래시 출력 1회와 일반 반복 출력, 출력 실패 후 재시도.
- 사용자 ToString 자동 호출 0회, 최초 Label 지정 경쟁, 타입명/HTML 처리, 출력 내 이름 일관성.
- NoInlining 헬퍼로 분리한 약한/강한 참조 수명, Subject만 살아 있어도 과거 세션을 붙잡지 않는지, 수집된 컨텍스트의 남은 슬롯 반환.
- 원인별 경고 1회, await로 스레드가 바뀐 Scope/Exert 종료, 경고 로거 재진입, 등록된 Subject 조회 및 추적 슬롯 재사용의 무할당.

## 실행

핵심 구현 커밋 전 결과: Release 194/194, Debug 194/194, 잠금 핸들 풀 비교 구성 194/194 통과, 건너뜀 0개. 라이브러리와 콘솔 샘플 Release 빌드는 경고·오류 0개였다.

```powershell
dotnet build src/BlackThunder.BlackboxSystem -c Release
dotnet build samples/Blackbox.NativeCSharp.Samples -c Release
dotnet test tests/BlackThunder.BlackboxSystem.Tests -c Release
dotnet test tests/BlackThunder.BlackboxSystem.Tests -c Debug
dotnet test tests/BlackThunder.BlackboxSystem.Tests -c Release -p:BlackboxLockHandles=true
```

`BlackboxLockHandles`는 기존 잠금 핸들 풀을 비교하는 테스트 구성이다. 기본 라이브러리에는 활성화하지 않는다. 테스트는 실제 testhost 실행 결과로 판단하며 빌드 성공이나 호스트 연결 시간 초과를 통과로 처리하지 않는다.

초기 샌드박스 실행은 testhost 연결 시간 초과로 중단되었다. 샌드박스 밖의 기존 코드 실행은 159개 중 158개 통과, 약한 소유자의 자동 이름 변경에 의존하던 테이블 테스트 1개 실패였다. 변경 후에는 해당 검증을 명시적 이름 계약으로 이관했다. 결과 TRX는 무시되는 `tests/BlackThunder.BlackboxSystem.Tests/TestResults/`에 저장한다.

측정 가능한 처리량·할당 비용은 [벤치마크 보고서](../../benchmarks/README.md)를 참고한다. 실행 중 출력은 근사 스냅샷이므로, 모든 동시 기록의 포함이나 여러 컨텍스트를 한 순간에 읽는 원자성을 테스트 계약으로 삼지 않는다.

## 후속 세션 키 진단 단계

핵심 구현을 `6d08696`으로 커밋한 뒤 `CurrentKey`, `DefaultKey`, 핸들의 `SessionKey`를 별도 단계로 추가했다. 기존 API 구현은 변경하지 않고 읽기 전용 속성만 추가했다.

`SessionKeyTests`의 15개 검증은 다음을 포함한다.

- 세션 밖의 기본 키, Begin 후 인스턴스 키, 중첩 Dispose 후 바깥 키 복원.
- Task.Run·새 Thread·await에서 키 상속, Begin 전에 시작한 Task와 Thread의 기본 키 유지.
- 생성자에서 보관한 기본 세션 핸들이 다른 세션에서도 원래 저장소에 기록하는 상황.
- 기본값 및 리셋·종료·비활성화·동결로 무효화된 핸들의 Guid.Empty, 경고 없는 조회.
- 리셋에도 유지되는 세션 키, 공개 세션 객체 속성 부재, 10,000회 조회에서 할당 0바이트.

후속 변경을 포함한 Release 전체 테스트는 **209/209** 통과했다. 새 진단 테스트는 Debug와 잠금 핸들 풀 비교 구성에서 각각 **15/15** 통과했고 건너뜀은 없었다. 라이브러리와 콘솔 샘플 Release 빌드도 경고·오류 0개로 통과했다.

## 2026-10-30: 트래커 누적 수정

컨텍스트별 트래커 등록 목록을 세대별 고정 트래커 배열(Scope/Exert 각각 16개)로 교체했다. 기존 전체 ID 반환 및 CAS 종료 처리는 유지한다. `TrackerScalabilityTests` 9개 사례는 기본 세션의 2만 컨텍스트·48스레드에서의 트래커 재사용, 6만 객체 Write-only churn 후 힙 잔류, 공유 트래커의 동시 등록·재사용, 등록을 정지시킨 상태의 Dispose/ForceReset과 새 세대 슬롯 보호를 검증한다.

Release 전체 **218/218**, 잠금 핸들 풀 비교 구성 전체 **218/218**, Debug의 새 회귀 테스트 **9/9**가 통과했다. 힙 테스트는 NoInlining 헬퍼와 GC → Subject 정리 → GC로 저장소 잔류와 트래커 누적을 구분하며, 2만/4만/6만 객체 시점의 기준 대비 증가량은 각각 0바이트였다. 솔루션(샘플 포함) Release 빌드는 `-m:1 -p:UseSharedCompilation=false`에서 경고·오류 0개로 통과했다.

별도 프로세스 벤치마크는 변경 전후를 번갈아 실행했다. 30만 객체 후 트래커는 60만 개에서 32개로 줄었고, Subject 정리 후 힙 증가량은 220,804,472바이트에서 1,400바이트로 줄었다. GC만 수행한 수치와 스코프 처리량은 [벤치마크 보고서](../../benchmarks/README.md)에 함께 기록했다.

## 2026-10-30: Subject 조회 확장성 수정

트래커 수정 커밋 `06b5de6` 이후 별도 단계로 기본 버킷을 65,536개로 늘리고, 조회 적중 경로가 해시 일치 노드의 Owner만 읽도록 바꿨다. 명시적 세션의 256개 버킷과 CAS 재탐색·유일성 계약은 유지한다. `SubjectLookupTests` 4개 사례는 세션별 버킷 크기와 리셋, 2만 살아 있는 객체의 동일 Blackbox 반환·조회 무할당, 조회 적중 시 정리 생략과 삽입 시 죽은 항목 정리를 검증한다. 기존 단일 버킷 동시 등록·정리 테스트도 그대로 통과했다.

최종 Release 전체 **222/222**, 잠금 핸들 풀 비교 구성 전체 **222/222**, Debug의 두 개선 회귀 테스트 **13/13**가 통과했다. 솔루션 및 샘플 Release 빌드는 경고·오류 0개였다. testhost 연결 실패를 통과로 간주하지 않았다.

조회 벤치마크의 40만 살아 있는 객체 조건에서 중앙값은 6,378.17ns에서 111.18ns로 줄었다. 모든 측정 루프의 할당은 0바이트였다. 메모리 비용도 별도로 확인했다. 중간 Subject 정리 없이 10만씩 6회 기록·GC하면 기존 힙 증가량은 79.00→429.91MiB, 수정 후는 68.49→86.89MiB로 평탄화됐다. 수정 후 트래커는 32개로 유지됐고, 마지막 Subject 정리 후 기준 대비 힙 증가량은 1,400바이트였다. 이 작업량의 측정값이며 전체 메모리의 보편적 상한을 뜻하지 않는다.

## 2026-10-30: 회전 커서로 죽은 Subject 항목 정리

커밋 `c3828a9`를 기준으로, 새 Subject 게시 CAS가 성공한 뒤 세대별 커서로 최대 16개 버킷을 정리하도록 수정했다. 이미 등록된 Subject 조회와 삽입 CAS 재시도에는 회전 정리를 수행하지 않는다. 정리 CAS가 다른 삽입에 지면 재시도 없이 다음 순회로 넘긴다. 기본 테이블의 크기와 무잠금 기록·등록 계약은 유지한다.

`SubjectCleanupTests` 9개 사례는 작은 테이블·명시적 세션·기본 세션에서 전체 순회에 따른 회수, 커서 정수 오버플로, 살아 있는 객체의 Blackbox·로그 보존, 조회 적중 시 정리 중단, 동시 등록의 유일성과 등록당 한 번의 정리 구간 예약을 검증한다. 정리 CAS 직전 작업을 멈추는 훅으로 동시 삽입과의 충돌, Dispose/ForceReset 이후 지연된 정리의 세대 격리도 확인한다. GC 테스트는 NoInlining 헬퍼로 버린 참조의 수명을 분리한다.

Release 전체 **231/231**, 잠금 핸들 풀 비교 구성 전체 **231/231**, Debug의 Subject 정리·조회 집중 검사 **13/13**이 통과했다. 샘플 포함 솔루션 Release 빌드는 `-m:1 -p:UseSharedCompilation=false`로 경고·오류 0개였다. testhost는 로컬 연결이 가능한 환경에서 실행했으며 연결 실패를 통과로 처리하지 않았다.

벤치마크는 같은 하네스와 수정 전후 런타임을 별도 프로세스에서 비교했다. 60만 객체를 한 번씩 기록한 뒤 GC한 힙 증가량은 MaxLogCount 100에서 **86.82→10.41MiB**, 1000에서 **537.87→23.09MiB**로 줄었다. 신규 등록을 멈춘 상태에서 GC만 반복하면 이 값은 유지됐고, 전체 Subject 정리 뒤에는 모두 1,400바이트가 남았다. 등록이 중단된 경우의 즉시 회수나 고정 메모리 상한은 보장하지 않는다.

조회는 계속 무할당이며 이번 비교에서 처리량은 비슷했다. 반면 살아 있는 객체 40만 개에서 단일 스레드 신규 등록은 **1.03→6.29µs**로 늘었다. 24스레드 등록 측정은 변동이 커 일정한 비율의 개선·악화로 일반화하지 않는다. 메모리 감소와 신규 등록 비용을 함께 [벤치마크 보고서](../../benchmarks/README.md)에 기록했다. 트래커·조회·회전 정리 수정 이력은 배포 전 0.2.0 CHANGELOG 항목으로 합쳤다.

## 2026-10-30 기본 세션 재시작 리팩터링

현재 소스는 BlackboxHandle.RestartDefaultSession()만 공개하며 ForceReset 호환 별칭을 제공하지 않는다. 세션 내부 세대를 제거하고 세션마다 하나의 고정 SessionState를 보관한다. 기본 세션은 설정 스냅샷을 상속한 새 인스턴스로 CAS 교체한 뒤 이전 인스턴스를 종료한다. 기본 키·저장소·카운터·출력 일련번호·동결·경고 상태는 새로 시작한다. 명시적 세션과 자식 문맥은 그대로 유지되며, 새 명시적 세션은 Dispose 후 Begin으로 시작한다. 슬롯 재사용 보호용 HandleManager 세대는 유지한다.

검증 범위:

- 기본 키 변경, 설정 및 비활성화 설정 유지, 저장소·카운터·크래시·경고 상태 분리.
- 종료된 기본 핸들의 기록 거부와 이전 로그의 텍스트·HTML 반복 출력, 파일 덮어쓰기 방지.
- 중첩 명시적 세션 안에서 기본 세션을 재시작해도 설정·Scope/Exert·자식 작업을 유지하고 종료 후 최신 기본 세션으로 복귀.
- 이미 종료된 명시적 바인딩에서 재시작해도 그 문맥이 되살아나거나 기본 세션으로 이동하지 않는 동작.
- CAS 직전 두 재시작을 정지시켜 발생시킨 경합에서 실패 후보와 교체된 세션의 종료, 최종 기본 세션의 생존.
- 기본 세션 재시작 및 명시적 세션 종료와 경합하는 핸들 등록·로그 게시·Subject 정리, 오래된 ID의 반환 및 다른 세션의 슬롯 보호.
- 기존 조회 무할당, 트래커 수량·회수 및 전체 NUnit/UniTest 회귀 검사.

Windows x64, .NET SDK 9.0.320에서 확인한 결과:

| 검증 | 결과 |
| --- | --- |
| 세션·트래커·Subject 관련 집중 테스트 | 73/73 통과 |
| Release 전체 테스트, 기본 핸들 풀 | 239/239 통과, 건너뜀 0 |
| Release 전체 테스트, BlackboxLockHandles=true | 239/239 통과, 건너뜀 0 |
| Release 솔루션 빌드 | 경고 0, 오류 0 |
| Release 벤치마크 빌드 | 경고 0, 오류 0 |
| 수정 문서의 로컬 링크와 Draw.io XML 참조 | 통과 |

샌드박스에서는 VSTest와 testhost 연결이 90초 뒤 시간 초과되어 집중 테스트가 중단되었다. 같은 테스트와 두 전체 구성을 샌드박스 밖에서 실행한 결과만 통과로 집계했다. 솔루션 빌드 역시 샌드박스에서 진단 없이 실패한 뒤 샌드박스 밖에서 성공을 확인했다. TRX 결과는 무시되는 TestResults/restart-default-release.trx 및 TestResults/restart-default-lock-handles.trx에 저장했다. 과거 성능 측정값을 갱신하지 않았으며 새 성능 향상은 주장하지 않는다.

## 2026-10-30 SessionState 통합

세션 내부 세대 제거에 이어 SessionState를 BlackboxSession에 통합했다. 설정·SubjectStore·기록 카운터·크래시·경고·핸들 트래커를 세션이 직접 소유하며 Blackbox와 LogContext의 Session 참조도 해당 세션을 직접 가리킨다. 내부 CurrentState 접근은 Current로 바꿨다. SessionSettings는 복사 후 원자적 교체를 위해 유지하며 공개 API를 늘리지 않았다.

기존 기본 세션 재시작, 명시적 세션 격리, 중첩·자식 문맥 복원, 종료 후 Export, 경합 중 핸들 회수 및 조회 무할당 검증을 그대로 통과했다. 벤치마크의 트래커 관찰도 통합된 세션을 조회하도록 이전했다. 새로운 성능 측정은 수행하지 않았다.

- Release 전체 테스트: 기본 핸들 풀 239/239, 잠금 핸들 풀 비교 구성 239/239 통과, 건너뜀 0.
- 솔루션 및 벤치마크 Release 빌드: 각각 경고 0, 오류 0.
- 소스·테스트·벤치마크에 SessionState/CurrentState 참조 없음. 수정 문서의 로컬 링크 및 diff 공백 검사 통과.
- 같은 로컬 testhost 연결 환경에서 실행했으며 TRX는 TestResults/merged-session-release.trx와 TestResults/merged-session-lock-handles.trx에 저장했다.

## 2026-10-30 세션 레지스트리 분리

BlackboxSession의 static 개념(실행 흐름 바인딩, 기본 세션 자리, 현재 세션 조회, 기본 세션 재시작)을 내부 `BlackboxSessionRegistry`로 옮겼다. BlackboxSession은 세션 인스턴스 상태만 소유하며 `Begin()`, `CurrentKey`, `DefaultKey`는 공개 입구로 남아 레지스트리에 위임한다. 기존 `BlackboxRegistry`의 조회 메서드는 새 레지스트리로 합쳤고, SubjectStore는 별도 파일로 분리했다. 현재 흐름의 카운터를 노출하던 `BlackboxRuntime`은 제거했다. 동작과 공개 API는 바뀌지 않았다.

테스트 파일 `BlackboxRuntimeTests.cs`는 `SessionCounterTests.cs`로, `BlackboxRegistryTests.cs`는 `BlackboxSessionRegistryTests.cs`로 바꿨고 UniTest 표 이름도 `SessionCounters`, `BlackboxSessionRegistry`로 맞췄다.

- Release 전체 테스트: 기본 핸들 풀 239/239, 잠금 핸들 풀 비교 구성 239/239 통과, 건너뜀 0.
- 솔루션(벤치마크 포함) Release 빌드: 경고 0, 오류 0.
- 소스·테스트·벤치마크·문서에 BlackboxRuntime/BlackboxRegistry 참조 없음(이 기록 제외).

## 2026-10-30 세션 static 통합

세션 관련 static을 모두 `BlackboxSessionRegistry`에 모았다. `BlackboxSession.Begin()`, `CurrentKey`, `DefaultKey`를 제거해 `BlackboxSession`에는 static 멤버가 없다. 공개 진입점은 `BlackboxHandle`로 일원화해 `BeginSession()`, `CurrentSessionKey`, `DefaultSessionKey`, `RestartDefaultSession()`이 레지스트리에 위임한다. 테스트 전용 `BeginForTests(buckets)`는 `BlackboxSessionRegistry.Begin(buckets)`로 대체했다.

`SessionKeyTests`의 공개 표면 검사는 진단 키가 `BlackboxHandle`의 읽기 전용 Guid static 속성인지, `BlackboxHandle`의 static 속성이 세션 객체를 반환하지 않는지, `BlackboxSession`에 공개 static 멤버가 없는지 확인한다.

- Release 전체 테스트: 기본 핸들 풀 239/239, 잠금 핸들 풀 비교 구성 239/239 통과, 건너뜀 0.
- 솔루션(벤치마크·샘플 포함) Release 빌드: 경고 0, 오류 0.

## 2026-10-30 활성 세션 추적과 Dispose 누락 진단

`BlackboxSessionRegistry`에 잠금으로 보호하는 강한 참조 목록을 추가했다. Begin은 목록에 추가한 뒤 실행 흐름에 바인딩하고, 최초 Dispose는 핸들 ID 회수·목록 제거·호출 흐름의 바인딩 복원을 수행한다. 중복 Dispose는 바인딩도 바꾸지 않는다. 기본 세션과 재시작 경쟁 후보는 목록에 넣지 않는다. 공개 진입점은 `BlackboxHandle.ActiveSessionCount`, `GetActiveSessionKeys()`, `DisposeActiveSessions()`이며 `BlackboxSession`에는 공개 static 멤버를 추가하지 않았다.

`ActiveSessionTests`의 새 검증 12개는 다음을 포함한다.

- 세션 밖 0 → Begin 후 1 → Dispose 후 0, 중첩 시작 순서와 해당 키 제거, 중복 Dispose 및 동시 중복 Dispose.
- 키 스냅샷의 호출자 수정과 이후 Begin/Dispose가 서로 영향을 주지 않는지 확인.
- 기본 세션과 재시작 후 기본 세션 제외, 빈 목록 일괄 정리 후 기본 세션 유지.
- NoInlining 헬퍼가 별도 ExecutionContext에서 시작한 세션과 미종료 Scope/Exert를 버린 뒤 GC해도 세션·키·전역 핸들 슬롯이 유지됨. 일괄 Dispose 후 목록과 슬롯이 비워지고 세션의 약한 참조도 수집됨.
- Task와 Thread에서 Dispose한 세션의 목록 제거, 호출자에게 남은 종료된 바인딩과 중복 호출의 무효 동작.
- 8개 스레드가 각각 100회 중첩 Begin/Dispose와 키 스냅샷 조회를 반복한 뒤 예외 없이 활성 세션 0.
- 일괄 종료 후 BlackboxHandle 무효화와 Scope/Exert ID 회수, 호출 흐름의 중첩 바인딩 복원, 다른 흐름의 종료된 바인딩 유지 및 기록 거부.
- 다른 흐름에서 시작한 세션도 전역 일괄 정리 대상에 포함되며, 공개 API는 키·개수만 노출함.

기존 `CompetingRestartsDisposeReplacedSessionsAndUnpublishedCandidates`에 교체 전 후보 생성 시점·경합 대기 중·교체 완료 후 활성 목록이 비어 있다는 검사를 추가했다. 다른 async 메서드에서 Dispose한 뒤 호출자가 다시 Dispose하면 바인딩을 복원하던 기존 기대값은 중복 호출이 아무것도 하지 않는 계약에 맞췄다.

`BlackboxTestBase.TearDown`은 활성 개수가 0인지 먼저 검사하고 실패 메시지에 남은 키를 넣는다. 검사 실패 시에도 `finally`에서 일괄 종료한 뒤 임시 파일과 기본 세션을 정리한다. 전체 테스트에서 추가로 발견된 Dispose 누락은 없었다. 목록 검사는 전역이므로 테스트 기반 클래스의 `[NonParallelizable]`을 유지한다.

Windows x64, .NET SDK 9.0.320에서 확인한 결과:

| 검증 | 결과 |
| --- | --- |
| 활성 세션·기본 재시작·세션 동시성 집중 테스트 | 44/44 통과 |
| Release 전체 테스트, 기본 핸들 풀 | 251/251 통과, 건너뜀 0 |
| Release 전체 테스트, `BlackboxLockHandles=true` | 251/251 통과, 건너뜀 0 |
| Release 솔루션 빌드(라이브러리·테스트·샘플) | 경고 0, 오류 0 |
| Release 벤치마크 프로젝트 별도 빌드 | 경고 0, 오류 0 |
| 수정 문서 7개의 로컬 링크 대상 검사 | 통과 |

```powershell
dotnet test tests/BlackThunder.BlackboxSystem.Tests/BlackThunder.BlackboxSystem.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false --logger "trx;LogFileName=active-sessions-release.trx"
dotnet test tests/BlackThunder.BlackboxSystem.Tests/BlackThunder.BlackboxSystem.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -p:BlackboxLockHandles=true --logger "trx;LogFileName=active-sessions-lock-handles.trx"
dotnet build BlackThunder.BlackboxSystem.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false
dotnet build benchmarks/Blackbox.Benchmarks/Blackbox.Benchmarks.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false
```

샌드박스의 첫 집중 테스트는 컴파일 후 VSTest/testhost 연결 시간 초과로 중단되었다. 통과 수치는 샌드박스 밖에서 실행한 결과이며 TRX는 무시되는 `tests/BlackThunder.BlackboxSystem.Tests/TestResults/active-sessions-*.trx`에 저장했다. 벤치마크는 현재 솔루션에 포함되지 않아 별도로 빌드했다.

기존 등록된 Subject 조회·준비된 추적 슬롯 재사용·세션 키 조회의 무할당 검증도 두 전체 구성에서 통과했다. 기록·등록 경로는 변경하지 않았으며 추가 잠금은 세션 시작·종료·활성 목록 조회에만 있다. 잠금 핸들 풀 비교 구성에는 기존대로 무잠금 보장이 적용되지 않는다. 성능 벤치마크를 새로 측정하지 않았으며 `.drawio` 파일은 수정하지 않았다.

## 2026-10-30 Dispose 바인딩 복원 수정

직전 절의 "중복 Dispose는 바인딩도 바꾸지 않는다" 규칙을 바꿨다. 이 규칙에서는 다른 흐름(Task, Thread, 다른 async 메서드, 일괄 정리)이 먼저 세션을 종료하면, 세션을 연 흐름이 `using`을 벗어난 뒤에도 종료된 세션에 계속 묶여 기록이 모두 거부됐다. 이제 세션 종료와 활성 목록 제거는 최초 Dispose에서만 수행하고, 바인딩 복원은 호출한 흐름이 그 세션에 묶여 있으면 매 Dispose에서 수행한다.

- `ActiveSessionTests.DisposalFromAnotherFlowLeavesCallerBoundUntilCallerDisposes`(Thread/Task), `SessionConcurrencyTests.DisposeInAnotherAsyncMethodDoesNotRestoreCallersBinding`: 다른 흐름이 종료한 뒤에도 호출자의 바인딩은 남아 있고, 호출자 자신의 Dispose 후에는 기본 세션으로 돌아오며 활성 목록은 0을 유지한다.
- `ActiveSessionTests.BulkDisposalInvalidatesHandlesAndRestoresOnlyCallingFlow`: 일괄 정리 뒤 자식 Task가 안쪽 세션을 직접 Dispose하면 자식의 바인딩은 바깥 세션(역시 종료됨)으로 돌아가고 기록은 계속 거부된다.
- Release 전체 테스트: 기본 핸들 풀 251/251, 잠금 핸들 풀 비교 구성 251/251 통과. 솔루션 Release 빌드 경고 0, 오류 0.

## 2026-10-30 SessionState 데이터 객체 분리

기록 코드(Blackbox, LogContext)가 BlackboxSession 전체를 참조하던 구조를 바꿨다. 세션이 소유한 저장소와 핸들 추적기에 기록 코드가 접근하지 않도록 계층을 나눴다.

- `SessionState`(Core): 설정·크래시·종료 여부·경고 표시·기록 카운터와 원자적 접근자만 가진 데이터 객체. 세션·저장소·추적기·레지스트리를 참조하지 않는다. 기록 가능 판정과 경고 출력은 `SessionStateGuard` 확장 메서드가 맡는다.
- `BlackboxSession`(Front): SessionState·SubjectStore·핸들 추적기·수명을 소유한다. Scope/Exert 핸들 등록은 `OpenScope`/`Exert`로 세션 쪽에서 수행한다.
- `Blackbox`는 생성자로 SessionState만 주입받는다. `WriteScope`/`Exert`는 기록만 하고 핸들을 만들지 않는다. 레지스트리를 쓰던 공개 생성자와 `GetCurrentSequence()`는 제거했다.
- `BlackboxHandle`은 Blackbox와 소유 세션을 함께 보관한다. Exert·CrashExport 대상 조회, 출력 전 정리, 출력 파일명은 이 세션을 쓴다. `TagHandle`은 BlackboxHandle이 묶어 준 세션의 저장소에서 대상을 찾는다.
- 테스트는 기록 코드를 직접 부를 때 `BlackboxTestDoubles.Scope/Exert/Bind`로 현재 세션을 거치도록 바꿨다. `SessionLayeringTests`가 Core 타입에 세션·저장소·추적기 필드가 없음을 확인한다.
- Release 전체 테스트: 기본 핸들 풀 256/256, 잠금 핸들 풀 비교 구성 256/256 통과. 솔루션(벤치마크·샘플 포함) Release 빌드 경고 0, 오류 0.
