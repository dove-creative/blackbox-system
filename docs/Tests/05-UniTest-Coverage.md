# C# UniTest 검증 범위

## 목차

1. 범위와 판정 기준
2. 실행 구조
3. 기능별 검증 대응
4. 보완한 누락과 유지 규칙
5. 실행 결과와 한계

## 1. 범위와 판정 기준

이 문서는 C# 구현의 공개 계약, 상태·동작 표, 기존 회귀 검증이 UniTest 실행 경로와 어떻게 연결되는지 설명한다. TypeScript 테스트는 별도 범위이다.

이번 보완의 '누락 없음' 기준은 현재 공개 API 대응 목록과 명시된 시나리오가 검증 실행에서 빠지지 않는 것이다. 모든 입력값, 모든 스레드 스케줄, 모든 미래 실행 경로의 수학적 완전성을 뜻하지 않는다. 테스트 수, 문장 커버리지, 공개 메서드 호출 여부 하나만으로 동작 검증의 충분성을 판단하지 않는다.

기존 표는 [상태·동작 표](01-Tables.md), 세션 수명 계약은 [세션 문서](../Wiki/05-Sessions.md), 실행 명령은 [테스트 계획](02-Plans.md)을 따른다.

## 2. 실행 구조

NUnit은 테스트 발견, TestCase 인자, fixture 준비·정리, assertion API를 맡는다. UniTest는 검증 본문을 실행하고 실패 보고서를 구성한다.

- 단위·회귀 검증: `RunWithUniTest` 실행 연결부가 기존 본문을 이름을 가진 단일 `Lab`과 `Node`에서 한 번 실행한다. 기존 assertion과 비동기 흐름을 유지하면서 실패를 NUnit 결과에 전달하고 UniTest 보고서를 제공한다.
- 표 검증: `BlackboxTableProject`가 상태·동작 시나리오를 실행한다. 결정적 실행은 각 시나리오를 새 기록 수명에서 준비하고 정의된 목록과 실제 완료 목록을 대조한다. 시드별 연속 실행은 같은 수명의 추가 경로 검증이다.
- 세션 검증: `BlackboxSessionUniTests`의 Project가 시작·중첩·종료·동결·기본 세션 교체를 연결하고, 전이 이후의 키, 설정, 핸들, 기록 격리를 검사한다. 안쪽·바깥쪽 우선 종료, 기본 세션 교체, 활성 목록, 출력 실패·동결·재시도, 병렬 격리의 여섯 전이 체인이 있다.
- C# 10 호출자 검증: 별도 언어 버전 프로젝트에서 컴파일러가 실제 선택하는 보간 문자열 오버로드를 UniTest로 실행한다. 런타임 라이브러리는 계속 C# 9와 `netstandard2.1` 기준이다.

기존 NUnit 테스트를 단순히 다시 호출하는 두 번째 테스트 묶음을 만들지 않는다. 하나의 검증 본문이 UniTest를 통과하고 NUnit에 결과를 전달하는 구조이다. 표 Project와 세션 Project는 개별 assertion 검증 위에 상태 전이와 실행 이력을 추가한다.

## 3. 기능별 검증 대응

아래 파일명은 `csharp/tests/BlackThunder.BlackboxSystem.Tests` 기준이다. C# 10 호출자 파일은 별도 프로젝트에 있다.

| 계약 묶음 | 검증 대상 | 주요 검증 본문 |
| --- | --- | --- |
| 설정 | 두 Configure 오버로드, logger, 참조 정책, 버퍼 크기, 출력 기본값, BasedOnSettings 보존 | InfrastructureTests, DefaultSessionRestartTests, UniTest/BlackboxPublicContractUniTests |
| 객체 동일성·이름 | 같은 객체 재조회, null 입력, 최초 비어 있지 않은 label, 중복 label 경고, 동시 설정, subject의 ToString 미호출 | BlackboxHandleTests, IdentityLifetimeTests, SubjectLookupTests |
| 참조 수명 | 약한 owner 수집, 강한 참조, 보관 핸들과 이전 세션, 정리 중 경합 | IdentityLifetimeTests, SubjectCleanupTests, TrackerScalabilityTests |
| 기록 활성 상태 | 신규·기존 핸들의 비활성 fallback, 대상 미등록, 재활성화, 동결·종료 뒤 거부 | BlackboxHandleTests, DefaultSessionRestartTests, UniTest/BlackboxPublicContractUniTests |
| 생성·조건·정리 도우미 | Construct 오버로드와 cached handle, When 조건·예외, Dispose 기록, 호출자 이름 | BlackboxHandleTests, UniTest/BlackboxPublicContractUniTests |
| 메시지·태그 | 값·서식·자리 치환, null·빈 대상, 표시 정책, 보관 로그·동시 태그 갱신 | LogDataTests, LogFormatterTests, TagHandleTests, IdentityLifetimeTests |
| 스코프·상호작용 | 기본·유효·정리된 핸들, 외부 스코프 우선 종료, 다른 스레드, 인접 병합, 세션 경계 | ScopeHandleTests, ExertHandleTests, LogContextTests, SessionConcurrencyTests, IdentityLifetimeTests |
| 오류·크래시 | ErrorContainer 기본값·null·빈 배열·tuple, 문맥 이름, null 메시지, 동결·중복 크래시·재출력 | BlackboxHandleTests, SessionConcurrencyTests, UniTest/BlackboxPublicContractUniTests |
| 세션 수명·진단 | 키, 중첩 복원, 활성 세션 목록, 중복·일괄 종료, 다른 흐름의 종료, 유실된 명시적 세션 | SessionKeyTests, ActiveSessionTests, DefaultSessionRestartTests, UniTest/BlackboxSessionUniTests |
| 비동기·경합 | Task·Thread 문맥 상속, 동시 세션 격리, 등록·발행과 종료의 경합, 경고 재진입 | SessionConcurrencyTests, ActiveSessionTests, SubjectCleanupTests, TrackerScalabilityTests |
| 출력 변환 | 집중·전체 그래프, 순환·깊이 제한, 스코프 평탄화, TXT·HTML, 이스케이프·연결 | ExportToolsTests, ExporterTests, IdentityLifetimeTests |
| 출력 수명 | 일반 출력 반복, 파일명 충돌 회피, 크래시 동결, I/O 실패·재시도, 이전 세션 기록 출력 | DefaultSessionRestartTests, SessionConcurrencyTests, IntegrationTests |
| 언어 경계 | C# 9 string fallback·수동 handler, C# 10 보간 오버로드·평가 생략 | BlackboxHandleTests, 별도 CSharp10/InterpolatedWriteTests |
| 구조·확장성 | 공개 진단 표면, 내부 계층 참조, tracker 수명·메모리·할당 | SessionLayeringTests, SessionKeyTests, ActiveSessionTests, TrackerScalabilityTests, SubjectLookupTests |
| 샘플 실행 | 정보 명령, 시나리오·별칭, 잘못된 입력, 대화형 복구, 출력 오류 | SampleCliTests |

기존 표 Project는 로그 값, 서식, WriteHandler, TagHandle, ScopeHandle, ExertHandle, 카운터, 저장소, 설정, LogContext, Blackbox, BlackboxHandle, 출력 도구, exporter, 통합 흐름을 다룬다. 세션·동시성·수명·샘플 회귀 본문도 같은 UniTest 실행 경로에 포함하여 표 밖에 있던 검증을 누락시키지 않는다.

## 4. 보완한 누락과 유지 규칙

기존 구현에는 표 Project의 깊이 제한·시드 실행만으로 모든 시나리오가 방문되었다고 판단할 여지가 있었다. 표에 시나리오를 추가할 때 실제 실행 완료 목록과 일치하는지 확인해야 한다. 조건이 만족되지 않아 실행할 수 없는 시나리오를 조용히 건너뛰어 성공으로 처리하지 않는다.

기존에 NUnit만 사용하던 세션, 객체 수명, 경합, 샘플, C# 10 검증은 UniTest 실행 연결부로 포함한다. 검증 본문의 테스트 인자와 준비·정리는 유지한다.

별도로 보완한 공개 계약은 다음과 같다.

- 비활성 상태에서 Construct·Write·Scope·Exert·오류·크래시가 기록·대상 등록·파일·동결을 만들지 않고, 재활성화 후 기존 핸들이 이어서 기록하는 동작
- 모든 핸들 상태에서 When 조건 함수의 한 번 평가, null 거부, 원래 예외 전달
- 단일 logger Configure 오버로드와 BasedOnSettings 정책 유지
- ErrorContainer 기본값·null·빈 배열과 tuple 변환, 빈 문맥 이름의 fallback, null 대상 자리 유지
- 동결 후 신규 객체·label을 추가하지 않으면서 보관된 이름으로 재출력하는 동작
- Construct 전달 오버로드와 CallerMemberName, C# 10 보간 문자열 오버로드별 활성·비활성 동작

표 보완 중 종료된 ScopeHandle의 `With`가 이전 open 기록에 태그를 붙이던 누락을 확인하였다. 종료 후에는 태그와 대상 기록을 바꾸지 않는다는 표의 계약에 맞게 고치고, 순차·별도 시드 실행에서 해당 무동작을 검증한다.

Construct의 기존 오버로드에는 주의할 구분이 있다. 문자열 하나를 위치 인자로 전달하는 `Construct("name")`은 메서드 이름을 지정한다. 생성 메시지를 전달하면서 호출자 이름을 자동으로 받으려면 `Construct(message: "message")`를 사용한다. 두 경로를 각각 검증하며 공개 오버로드를 변경하지 않는다.

모든 NUnit 진입 메서드는 `RunWithUniTest`를 사용한다. 실제 UniTest Project를 실행하는 메서드도 바깥 Lab에서 전체 성공과 실행 이력 assertion을 검사한다. 두 테스트 어셈블리의 자동 검사는 이 연결부가 빠진 신규 테스트를 실패시킨다.

[PublicApiCoverage.txt](../../csharp/tests/BlackThunder.BlackboxSystem.Tests/UniTest/PublicApiCoverage.txt)는 현재 공개 멤버 서명 89개를 검증 코드 묶음에 대응시킨다. [PublicApiCoverageTests](../../csharp/tests/BlackThunder.BlackboxSystem.Tests/UniTest/PublicApiCoverageTests.cs)는 실제 공개 서명과 목록이 일치하고, 연결된 코드가 UniTest를 사용하는지 검사한다. C# 10의 compiler-selected ref 오버로드는 별도 호출자 프로젝트의 7개 연산 × 6개 수명 상태, 총 42개 사례로 검증한다.

공개 API 변경 시 대응 목록과 관련 상태·동작 시나리오도 함께 갱신한다. 자동 목록 검사는 누락을 발견하는 장치이며, assertion이 요구사항을 충분히 검증하는지는 코드 검토에서 함께 확인한다. fixture 준비·정리 assertion은 NUnit의 수명 관리 단계에서 실행된다.

## 5. 실행 결과와 한계

최종 실행 결과는 [테스트 결과](03-Results.md)에 기록한다. 기본 핸들 구현, `BlackboxLockHandles=true` 비교 구성, C# 10 호출자 프로젝트를 각각 검증한다.

시드별 연속 실행과 강제로 조정한 경합은 재현 가능한 특정 경로를 확인한다. 가능한 모든 동시 실행 순서를 열거하지는 않는다. 표와 대응 목록의 완전성은 현재 명시한 계약을 기준으로 하며, 새로운 기능과 발견된 회귀에는 새로운 상태·행동 검증을 추가해야 한다.
