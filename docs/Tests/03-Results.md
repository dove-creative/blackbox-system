# Blackbox 테스트 결과

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
