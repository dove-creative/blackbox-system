# Blackbox 테스트 결과

> 이 문서는 C# 구현의 검증 기준과 기록이다. TypeScript 사용법과 검증은 [TypeScript 안내](../../typescript/README.md)를 따른다.

이 문서는 `01-Tables.md`와 `02-Plans.md`를 기준으로 Blackbox 테스트를 정리하고 실행한 결과를 기록한다.

## 1. 260611 문서-테스트 정합 결과

### 1-1. 반영 범위

- `02-Plans.md`에 Unity Test Runner 축, 외부 NUnit 기본 기록 축, 런타임 기록 비활성 축을 구분해 적었다.
- `02-Plans.md`의 테스트 이름을 현재 `tests/BlackThunder.BlackboxSystem.Tests` 구현과 맞췄다.
- `BlackboxHandleTests.cs`에 `WriteHandler`의 valid/invalid handle 경로를 추가했다.
- invalid handler 경로에서는 interpolated string의 formatted expression이 평가되지 않는지도 side effect로 확인했다.
- `ExportToolsTests.cs`에 `TrimSmartSanitizesFileNameParts`를 추가했다.
- `03-Results.md`는 이 결과 기록 단계에서 새로 만들었다.

### 1-2. 실행 결과

| 구분 | 명령 | 결과 |
| --- | --- | --- |
| Blackbox runtime build | `dotnet build Blackbox.csproj --no-restore` | 성공. Unity 참조 충돌과 nullable 관련 경고가 있었지만 오류는 없었다. |
| Blackbox tests build | `dotnet build Blackbox.Tests.csproj --no-restore` | 성공. Unity 참조 충돌과 nullable 관련 경고가 있었지만 오류는 없었다. |
| 기본 기록 테스트(과거 기록) | `dotnet test --no-restore Packages/com.blackthunder.blackbox-system/Tests/ExternalNUnitExecutor~/ExternalNUnitExecutor.csproj` | 성공. 기록 당시 패키지 소유 실행기는 `total=97 passed=97 failed=0`을 출력했다. |
| 당시 .NET 테스트 모음 | `dotnet test tests/BlackThunder.BlackboxSystem.Tests/BlackThunder.BlackboxSystem.Tests.csproj -c Release --no-restore --no-build` | .NET 전용 전환 후 `159/159` 통과했다. |
| 기록 비활성 테스트 | `UseBlackbox=false` 런타임 축 | `BlackboxHandleTests`에서 fallback 동작으로 확인한다. |
| whitespace 검사 | `git diff --check` | 성공. |

### 1-3. 실행 중 확인한 사항

- 일반 샌드박스 실행에서는 Unity 생성 csproj 빌드가 `C:\Users\user\AppData\Local\Microsoft SDKs` 접근 권한 문제로 `MSB4184`를 냈다.
- 같은 빌드를 권한 있는 실행으로 재시도했을 때 `Blackbox.csproj`와 `Blackbox.Tests.csproj` 모두 성공했다.
- `ExternalNUnitExecutor~` 실행 후 생성된 `bin` / `obj` 산출물은 코드 변경이 아니므로 정리했다.
- 새 문서와 테스트 파일은 저장소의 LF 줄 끝 정책을 유지한다.

## 2. 2026-10-30 공통 플랫폼 기준 적용

BlackThunder Conventions를 guidelines/blackthunder-conventions 서브모듈로 추가하고, Directory.Build.props에서 저장소 전체의 언어 버전을 C# 9로 고정했다. 라이브러리는 netstandard2.1을 유지하며 테스트·샘플·벤치마크는 net10.0으로 전환했다.

검증 환경은 Windows x64, .NET SDK 10.0.403, .NET Runtime 10.0.14, Release이다. 시스템의 기존 SDK는 9.0.320이므로 검증에는 artifacts/platform-validation/dotnet에 별도로 준비한 공식 SDK를 사용했다. 해당 SDK와 검증 산출물은 Git 추적 대상이 아니다.

| 검증 | 결과 |
| --- | --- |
| 솔루션 빌드 | 경고 0개, 오류 0개 |
| 벤치마크 빌드 | 경고 0개, 오류 0개. 성능 측정은 실행하지 않음 |
| 기본 핸들 구성 전체 테스트 | 261/261 통과 |
| BlackboxLockHandles=true 전체 테스트 | 261/261 통과 |
| C# 9 소비자에서 DLL 참조 | 유효·무효 핸들과 기록 비활성 상태의 문자열 오버로드 동작 확인 |
| C# 10 소비자에서 DLL 참조 | 유효 핸들의 보간과 무효·기록 비활성 핸들의 식 평가 생략 확인 |

C# 9에서는 보간 문자열 호출이 string 오버로드를 선택하므로 기록이 꺼져 있어도 보간 식은 먼저 평가된다. 기존 테스트의 자동 평가 생략 기대를 C# 9의 fallback 검증으로 분리하고, WriteHandler를 직접 구성하는 테스트를 유지했다. C# 10 소비자 검증은 저장소의 플랫폼 예외가 아니라 별도의 임시 소비자 프로젝트로 수행했다.

전체 테스트 결과는 artifacts/platform-validation/results의 platform-default.trx와 platform-lock.trx에 기록했다. 최초 샌드박스 실행은 testhost 시작이 멈춰 중단했으며, 위 전체 테스트는 일반 권한에서 완료했다.

## 3. 2026-10-30 프로젝트 전반 컨벤션 적용

코드의 명명·서식·멤버 배치와 문서 구조를 정리하고 BlackboxHandle을 readonly partial struct로 변경했다. 샘플은 samples 바로 아래로 옮기고 명령 인자에 따른 일회 실행, 종료 코드, 오류 스트림과 SampleOutput 출력을 정리했다. 벤치마크 프로젝트 이름도 BlackThunder.BlackboxSystem.Benchmarks로 통일했다.

Windows x64에서 앞 절과 같은 .NET SDK 10.0.403과 Runtime 10.0.14로 검증했다. 모든 빌드는 Release이며 샘플 프로세스 테스트는 임시 디렉터리에서 실행했다.

| 검증 | 결과 |
| --- | --- |
| 솔루션과 벤치마크 빌드 | 각각 경고 0개, 오류 0개 |
| 기본 핸들 구성 전체 테스트 | 278/278 통과 |
| BlackboxLockHandles=true 전체 테스트 | 278/278 통과 |
| 샘플 CLI 회귀 | 전체 테스트에 17개 사례 포함. 열린 stdin에서도 일회 종료, 숫자 별칭, HTML·크래시 출력, 오류 코드와 대화형 복구 확인 |
| 샘플 복사 경로 보완 후 기본 출력 경로 | CLI 17/17 통과 |
| 별도 --artifacts-path 출력 경로 | CLI 17/17 통과. 참조 프로젝트의 실제 출력에서 샘플 파일 복사 확인 |
| 공개 API 비교 | 148개 메타데이터 항목에서 승인된 BlackboxHandle readonly 표시 외의 차이 없음 |
| 변경 전 DLL로 빌드한 C# 9·10 소비자 | DLL만 교체한 뒤 각각 실행 성공 |
| 변경 후 DLL로 빌드한 C# 9·10 소비자 | 일반 변수·readonly 필드·in 인자, Construct의 out 결과, 명시적 handler 소비와 보간 동작 확인 |
| 정적 검사 | 명명·구문, LF·공백, 상대 링크와 README 양언어 대응 확인 |
| 보존 검사 | AGENTS 본문은 고정된 공유 템플릿과 일치하며 HTML 출력 템플릿의 SHA-256은 변경 전과 동일 |

전체 테스트 이후 검토에서 샘플 복사가 기본 bin 경로에 의존하는 점을 발견했다. MSBuild의 GetTargetPath로 실제 출력 위치를 조회하도록 보완하고, 기본 경로와 별도 artifacts 경로에서 각각 CLI 전체 사례를 다시 검증했다. 런타임 코드와 테스트 시나리오는 이 보완에서 변경하지 않았다.

결과 파일은 Git에서 제외한 artifacts/conventions-validation/results에 conventions-default.trx, conventions-lock.trx, sample-standard-output.trx, sample-custom-output.trx로 보관했다. API 비교 도구와 외부 소비자 프로젝트도 같은 검증 디렉터리에만 두었다. 벤치마크의 성능 측정은 실행하지 않았다.

## 4. 2026-10-31 C# UniTest 누락 보완

공개 계약과 기존 검증을 [UniTest 검증 범위](05-UniTest-Coverage.md)에 대응시켰다. 기존 NUnit 회귀 assertion 본문은 유지하면서 단일 UniTest Lab에서 실행하도록 연결하였다. NUnit은 발견·인자·fixture 준비와 정리를 계속 담당한다. 테스트를 두 번 실행하는 별도 복제 묶음은 만들지 않았다.

기존 테이블 순차 실행은 상태에 따른 후보 필터와 고정 깊이 때문에 등록된 시나리오를 건너뛸 수 있었다. 등록 목록 95개를 모두 실행하고 성공 ID를 정확히 대조하도록 변경하였다. 시드별 연속 실행은 추가 검증으로 유지한다. 세션에는 중첩 종료 순서, 기본 세션 교체, 활성 목록과 일괄 정리, 출력 실패·크래시·재시도, 병렬 격리의 6개 체인과 40개 명시적 전이를 추가하였다.

추가 검증에서 종료된 ScopeHandle.With가 이전 open 기록에 태그와 대상 객체를 추가하는 오류를 발견하였다. 종료 뒤 아무 동작도 하지 않는다는 기존 표의 계약에 맞게 수정하였다. Construct의 기존 위치 문자열 인자는 methodName으로 해석된다는 동작을 유지하고, message 이름 지정 경로와 각각 검증하였다.

Windows x64, 저장소 전용 .NET SDK 10.0.403, Release 구성에서 확인하였다.

| 검증 | 결과 |
| --- | --- |
| C# 솔루션 빌드 | 경고 0개, 오류 0개 |
| 기본 핸들 구성 전체 | 308/308 통과, 건너뜀 0개 |
| BlackboxLockHandles=true 전체 | 308/308 통과, 건너뜀 0개 |
| C# 10 호출자 전체 | 47/47 통과, 건너뜀 0개 |
| C# 10 실제 보간 오버로드 | 7개 연산 × 6개 수명 상태, 42개 사례 포함 |
| 실행 연결부 | 어셈블리별 5개 검사. 단일 실행·동일 스레드, 원래 예외, 반환된 실패, await 이후 실패, 신규 테스트 연결 누락 검사 |
| 공개 API 목록 | 공개 멤버 서명 89개와 검증 코드 묶음 대응 일치 |
| 표 실행 누락 방지 | 14개 이상의 사례, 크래시 이후 사례, 실행 불가능한 사례, 중복 ID 검증 |
| 테스트 발견 누락 검사 보강 후 집중 확인 | 기본 프로젝트 6/6, C# 10 프로젝트 5/5 통과 |

결과 파일은 Git에서 제외한 artifacts/unitest-validation의 default.trx, lock.trx, csharp10.trx에 보관하였다. 마지막으로 누락 검사 대상을 NUnit의 테스트 생성 인터페이스 전체로 확장한 뒤 coverage-guards.trx와 csharp10-guards.trx로 관련 검사를 재확인하였다. 최초 집중 실행에서 발견된 임시 출력 폴더·문자열 표현 기대값 문제와 위 ScopeHandle.With 결함을 수정한 뒤 전체를 검증하였다. CI에도 잠금 핸들 구성 실행을 추가했으며 원격 CI 실행은 이 기록에 포함하지 않는다.

완료 기준은 현재 공개 계약과 등록된 검증 사례의 누락 방지이다. 문장·분기 커버리지 100% 또는 가능한 모든 입력값과 스레드 실행 순서의 검증을 주장하지 않는다.
