# TypeScript 초기 구현 검증

2026-10-31에 언어별 저장소 구조와 TypeScript 초기 구현을 검증한 기록이다. 현재 실행 지침은 [사용 안내](../README.ko.md)를 따른다.

## 환경

- Windows x64
- 프로젝트 전용 .NET SDK 10.0.403: 저장소의 무시되는 `artifacts/dotnet/`에 설치하였다. 시스템 SDK 9와 다른 프로젝트는 변경하지 않았다.
- Node.js 22.23.4 및 24.15.0
- TypeScript 7.0.2, Playwright 1.64.1

## 결과

| 검증 | 결과 |
| --- | --- |
| C# Release 솔루션 빌드 | 경고 0개, 오류 0개 |
| C# 기본 테스트 | 278/278 통과 |
| C# 10 호출자 테스트 | 2/2 통과 |
| C# 잠금 핸들 비교 구성 | 278/278 통과 |
| NuGet 패키징 | 0.2.0 패키지 생성 및 C# README 포함 확인 |
| TS strict 타입 검사·ESM 빌드 | 통과 |
| Node.js 22 전체 TS 테스트 | 54/54 통과 |
| Node.js 24 전체 TS 테스트 | 54/54 통과 |
| Chromium·WebKit | 각 18개 공통 행위 검사, 다운로드 내용·파일명, HTML 접기·연결 이동, 브라우저 샘플 5종 통과 |
| npm tarball 소비 | 별도 프로젝트 설치·실행, ES2022+DOM 소비 프로젝트의 타입 선언 검사 통과 |
| GitHub Actions | 워크플로 작성 및 로컬 대응 명령 검증 완료, 원격 실행은 미확인 |

## 확인한 경계 사례

- 같은 객체의 병렬 스코프와 다른 세션에 보관된 핸들의 격리
- 기본 세션 재시작 중 명시적 세션 설정이 유입되지 않는 동작
- 약한 Subject 수집 이후 보관 핸들의 출력과 강한 참조 옵션
- 중복 정리, 바깥 스코프 우선 종료, 인접한 받는 쪽 스코프에만 Exert 병합
- 버퍼 순환, 태그 자리 치환, 집중·전체 그래프 출력 및 순환 관계
- 비활성 기록의 메시지 지연 평가 생략과 문자열 fallback
- 크래시 동결 후 출력 실패·재시도, 경고 callback 재진입
- HTML 사용자 문자열 이스케이프, 스코프 접기와 상호작용 이동
- Node 샘플의 단발 실행, 잘못된 명령, 비동기 실행 중 대기 입력과 EOF

## Firefox 실행 실패 원인

처음 검증에서는 Playwright Firefox 157.0의 Windows 바이너리가 테스트 시작 전에 실행에 실패하였다. Windows SideBySide 이벤트는 `mozglue, version="1.0.0.0"` 종속 어셈블리를 찾지 못했다고 보고하였고, 다시 설치해도 같은 오류가 재현되었다.

원인은 바이너리가 아니라 실행 환경이다. 검증 명령을 실행한 셸은 MSIX로 설치된 데스크톱 앱(Codex, Claude)의 하위 프로세스였다. 이런 프로세스가 AppData 아래에 쓴 파일은 `%LOCALAPPDATA%\Packages\<패키지>\LocalCache\Local`로 가상화된다. Playwright는 브라우저를 `%LOCALAPPDATA%\ms-playwright`에 설치하므로 실제 파일은 패키지 전용 위치에 있다. `firefox.exe`의 manifest는 같은 폴더의 `mozglue.dll`을 private side-by-side 어셈블리로 요구하는데, 프로세스를 만들 때 어셈블리를 찾는 단계는 가상화된 파일을 보지 못한다. Chromium과 WebKit은 private 어셈블리를 선언하지 않으므로 같은 위치에서도 실행된다.

`CreateActCtx`를 직접 호출하여 다음 결과를 확인하였다.

- 가상 경로(`%LOCALAPPDATA%\ms-playwright\...`)에서는 오류 14001(ERROR_SXS_CANT_GEN_ACTCTX)이 발생하였다. 같은 파일을 하드 링크한 Temp·Documents 폴더와 실제 패키지 경로에서는 성공하였다.
- AppData의 Local·Roaming·LocalLow 아래에 새로 만든 폴더는 모두 실패하였다. 가상화 대상이 아닌 Local\Temp, Local\Programs와 사용자 폴더는 성공하였다.
- 실제 패키지 경로의 `firefox.exe --version`은 정상 실행되었다.

실행 문제를 피하자 브라우저 테스트 서버의 버그가 드러났다. `/` 요청을 `resolve()`하면 끝 구분자가 사라져 폴더 범위 검사에 걸리고, 서버는 본문 없는 403을 반환하였다. Chromium·WebKit은 이 응답으로 페이지 이동을 마쳤지만 Firefox는 `page.goto`에서 시간 초과되었다. 서버가 `/`를 범위 검사보다 먼저 처리하도록 수정하였다. 이 버그는 실행 환경과 무관하므로 원격 CI의 Firefox 작업에서도 같은 시간 초과가 발생할 수 있었다.

패키지 앱 안의 셸에서 브라우저 테스트를 실행할 때는 일반 터미널을 사용하거나 `PLAYWRIGHT_BROWSERS_PATH`를 AppData 밖의 경로 또는 실제 패키지 경로로 지정한다. 기본 브라우저 테스트 명령은 세 엔진을 모두 실행하고 하나라도 실패하면 실패 상태를 반환한다.

특정 엔진만 진단할 때는 `BLACKBOX_BROWSERS=chromium,webkit` 환경 변수를 사용할 수 있다. 전체 검증에서는 이 필터를 생략한다.

## 종료된 스코프 태그 회귀 검증

2026-10-31 C# UniTest 보완에서 발견한 종료 후 스코프 태그 문제를 TS에서도 확인하였다. 수정 전에는 직접 종료한 스코프와 바깥 스코프 종료로 자동 닫힌 스코프 모두 `with()`로 기존 태그를 교체하고 대상 기록을 추가하였다. 두 회귀 테스트가 수정 전 실패하는 것을 확인한 뒤 `ScopeHandle.with()`에 수명 검사를 추가하였다.

검증은 같은 핸들 반환, 원래 태그·대상 로그·sequence 유지, 새 대상 미등록을 확인한다. Node·브라우저 공통 시나리오에도 종료 후 태그 동작을 포함하였다.

| 검증 | 결과 |
| --- | --- |
| 빌드와 Node.js 24 전체 테스트 | 56/56 통과 |
| Node.js 22 전체 테스트 | 56/56 통과 |
| Chromium·WebKit | 각 21개 공통 검사와 기존 다운로드·HTML·샘플 검증 통과 |

Firefox의 기존 Windows 실행 제한은 위 절에 기록된 상태이며, 이번 회귀 검증에서는 재실행하지 않았다.

## 리뷰 수정 검증

2026-10-31 리뷰에서 확인한 네 가지 문제를 수정하고 core를 모듈로 나눈 뒤 검증한 기록이다. 대상은 Windows 파일 열기 경고, `maxLogCount` 상한, 보고서 스크립트 삽입 방식, `with()` 인자 순서이다.

- Windows에서 `explorer.exe`로 HTML 파일을 직접 열어 보았다. 파일이 열린 뒤에도 종료 코드 1을 반환하였다. 회귀 테스트는 실행기를 대체하여 플랫폼별 종료 코드 처리와 실행 실패 보고를 확인한다.
- 수정 전 소스를 별도로 컴파일하여 같은 결정적 시나리오를 실행하였다. TXT·HTML 출력 6개는 세션 키, 시각, 스택 위치를 정규화했을 때 모두 같았다.
- 다른 세션의 핸들로 `runScope/runExert`를 호출하면 callback 동안 ambient 세션이 바뀌는 동작은 문서화된 계약으로 테스트에 포함하였다.
- Firefox 실행 실패의 원인을 확인하고 브라우저 테스트 서버의 `/` 응답 버그를 수정하였다. 자세한 내용은 Firefox 실행 실패 원인 절에 정리하였다.

| 검증 | 결과 |
| --- | --- |
| TS strict 타입 검사·ESM 빌드 | 통과 |
| Node.js 24 전체 TS 테스트 | 60/60 통과 |
| npm tarball 소비 | 설치·실행과 `with()` 인자 순서 타입 검사 통과 |
| Node 샘플 | `all` 실행 통과 |
| 브라우저 진입점 | Node에서 ambient 문맥 없이 공통 시나리오 21개 통과 |
| Node.js 22 | 미실행. 이 환경에는 Node.js 24만 설치되어 있다. |
| Chromium·Firefox·WebKit | 각 21개 공통 검사와 다운로드·HTML 조작·브라우저 샘플 검증 통과. `PLAYWRIGHT_BROWSERS_PATH`를 실제 패키지 경로로 지정하였다. |
