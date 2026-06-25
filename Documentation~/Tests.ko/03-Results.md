# Blackbox 테스트 결과

이 문서는 `01-Tables.md`와 `02-Plans.md`를 기준으로 Blackbox 테스트를 정리하고 실행한 결과를 기록한다.

## 1. 260611 문서-테스트 정합 결과

### 1-1. 반영 범위

- `02-Plans.md`에 Unity Test Runner 축, 외부 NUnit 기본 기록 축, 런타임 기록 비활성 축을 구분해 적었다.
- `02-Plans.md`의 테스트 이름을 현재 Blackbox/Tests` 구현과 맞췄다.
- `BlackboxHandleTests.cs`에 `WriteHandler`의 valid/invalid handle 경로를 추가했다.
- invalid handler 경로에서는 interpolated string의 formatted expression이 평가되지 않는지도 side effect로 확인했다.
- `ExportToolsTests.cs`에 `TrimSmartSanitizesFileNameParts`를 추가했다.
- `03-Results.md`는 이 결과 기록 단계에서 새로 만들었다.

### 1-2. 실행 결과

| 구분 | 명령 | 결과 |
| --- | --- | --- |
| Blackbox runtime build | `dotnet build Blackbox.csproj --no-restore` | 성공. Unity 참조 충돌과 nullable 관련 경고가 있었지만 오류는 없었다. |
| Blackbox tests build | `dotnet build Blackbox.Tests.csproj --no-restore` | 성공. Unity 참조 충돌과 nullable 관련 경고가 있었지만 오류는 없었다. |
| 기본 기록 테스트 | `dotnet test --no-restore Packages/com.blackthunder.blackbox-system/Tests/ExternalNUnitExecutor~/ExternalNUnitExecutor.csproj` | 성공. 기록 당시 실행기는 `total=97 passed=97 failed=0`을 출력했다. 현재 공식 명령은 Blackbox 패키지가 소유한 external executor를 사용한다. |
| 기록 비활성 테스트 | `UseBlackbox=false` 런타임 축 | `BlackboxHandleTests`에서 fallback 동작으로 확인한다. |
| whitespace 검사 | `git diff --check` | 성공. |

### 1-3. 실행 중 확인한 사항

- 일반 샌드박스 실행에서는 Unity 생성 csproj 빌드가 `C:\Users\user\AppData\Local\Microsoft SDKs` 접근 권한 문제로 `MSB4184`를 냈다.
- 같은 빌드를 권한 있는 실행으로 재시도했을 때 `Blackbox.csproj`와 `Blackbox.Tests.csproj` 모두 성공했다.
- `ExternalNUnitExecutor~` 실행 후 생성된 `bin` / `obj` 산출물은 코드 변경이 아니므로 정리했다.
- 새 문서와 테스트 파일은 저장소의 LF 줄 끝 정책을 유지한다.
