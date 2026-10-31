# TypeScript 구현 구조와 기록 계약

Blackbox TypeScript는 객체의 기록 수명과 실행 문맥을 분리하고, 같은 기록 엔진에서 Node.js 파일 출력과 브라우저 다운로드를 제공하는 구조이다.

## 목차

1. 구성 요소와 책임
2. 기록과 문맥 전달
3. 세션과 객체 수명
4. 출력과 오류 처리
5. C# 구현과의 대응
6. 검증 기준
7. 소스 색인

## 1. 구성 요소와 책임

| 구성 요소 | 책임 |
| --- | --- |
| 공개 핸들 | 객체 조회, 기록, 스코프·상호작용 생성, 태그와 출력 요청 |
| 세션 | 설정, 객체 동일성, 카운터, 종료·동결 상태와 명시적 수명 |
| 문맥 | 같은 세션 안의 실행 분기와 열린 스코프의 부모 관계 |
| 객체 저장소 | 해당 객체의 최근 기록과 표시 메타데이터 |
| 출력 변환 | 스냅샷을 TXT·HTML로 변환하고 스코프 깊이·연결 표시 계산 |
| Node 진입점 | AsyncLocalStorage 문맥 전달과 동기 파일 출력 |
| 브라우저 진입점 | 공통 API와 명시적 Blob 다운로드 |

공개 타입의 이름은 PascalCase이고 메서드·속성은 camelCase이다. 설정과 기록 매개변수는 options 객체로 받는다. 공개 진입점은 패키지 루트와 `/node`, `/browser`이며 내부 기록 모델과 출력 변환기는 직접 export하지 않는다.

플랫폼 어댑터는 같은 core를 사용하므로 한 실행 환경에서는 해당 환경의 진입점을 일관되게 선택한다. worker나 별도의 JavaScript realm 사이에 기록 상태를 공유하거나 자동 집계하는 기능은 제공하지 않는다.

## 2. 기록과 문맥 전달

일반적인 `BlackboxHandle.of(subject)`는 현재 Node 문맥의 세션을 선택하며, 바인딩이 없으면 공유 기본 세션을 선택한다. 브라우저의 일반 호출은 공유 기본 세션을 사용한다. 객체 조회의 키는 참조 동일성이며 label은 키에 관여하지 않는다.

스코프는 시작·끝 기록과 부모 스코프 식별자를 가진다. 같은 문맥에서 바깥 스코프를 먼저 닫으면 더 안쪽의 수동 스코프를 자동으로 닫고 경고한다. 다른 독립 문맥의 스코프를 함께 닫지 않는다.

직접 종료하거나 자동으로 닫힌 스코프의 `with()`는 같은 핸들을 반환하고 기록을 변경하지 않는다. 기존 태그를 교체하거나 태그 대상에 기록을 추가하지 않으며, 새로운 대상 객체도 등록하지 않는다.

`runScope`는 새 스코프 분기에서 callback을 실행하고 완료 시 스코프를 닫는다. `runExert`는 별도 분기에서 양쪽 객체의 상호작용을 기록하고 완료 시 받는 쪽 스코프 병합을 시도한다. 같은 상호작용 흐름의 바로 다음 받는 쪽 기록이 스코프 시작일 때만 병합하며, 그 흐름에서 중간 기록이 있으면 병합하지 않는다. callback이 만든 직접 자식 스코프 분기도 같은 흐름으로 취급한다.

callback API는 동기 반환값과 Promise를 지원한다. 성공·실패 모두에서 정리하고, callback의 예외는 보존하여 호출자에게 전달한다. 기록이 꺼져 있어도 사용자 callback은 실행한다. 호출자는 callback 밖으로 수명이 빠져나가는 작업을 만들기보다 자식 작업을 모두 await한 뒤 반환해야 한다.

Node.js에서는 AsyncLocalStorage가 문맥을 전달하므로 서비스 내부의 기존 `BlackboxHandle.of()` 호출이 같은 실행 분기에 참여한다. 브라우저에서는 callback으로 받은 `context.of()`를 사용해야 한다. 전역 변수를 await 동안 바꾸거나 Promise를 패치하여 ambient 문맥을 흉내 내지 않는다.

`context.fork()`와 `context.run()`은 같은 세션의 별도 실행 분기를 만든다. 브라우저에서 중첩 세션이 부모의 설정을 상속해야 하면 `context.createSession()/context.runSession()`을 사용한다. 정적 `BlackboxHandle.createSession()/runSession()`은 ambient 문맥이 없는 브라우저에서는 기본 세션의 설정을 기준으로 한다.

## 3. 세션과 객체 수명

명시적 세션은 시작 순서를 가진 활성 목록에 보관하고 최초 종료 때 목록에서 제거한다. 사용자가 참조를 버려도 종료하지 않은 명시적 세션은 진단할 수 있다. 기본 세션은 이 목록에 포함하지 않는다. 일괄 종료는 스냅샷의 역순으로 진행한다.

`createSession`은 detached 생성이며 ambient 바인딩을 변경하지 않는다. `session.run`은 세션을 자동 종료하지 않고, `runSession`만 생성과 종료를 함께 관리한다. 세션 종료 후 실행 중인 콜백이 같은 문맥을 보관하고 있으면 기록은 거부되며 기본 세션으로 자동 전환되지 않는다.

핸들은 원래 세션에 고정된다. 정적 `BlackboxHandle.of()`로 얻은 핸들은 Node에서 같은 세션의 현재 ambient 분기에 기록한다. `session.of()`와 `context.of()`로 얻은 핸들은 해당 명시적 문맥에 고정되므로 다른 ambient 분기를 따라가지 않는다. 어느 핸들도 다른 세션으로 이동하지 않는다.

`runScope`와 `runExert`의 callback은 핸들이 속한 세션의 분기에서 실행된다. Node에서는 이 분기가 callback 동안의 ambient 문맥이 되므로, 다른 세션에 고정된 핸들로 호출하면 callback 안의 일반 `BlackboxHandle.of()`도 바깥 세션이 아닌 핸들의 세션에 기록한다. 바깥 세션에 기록해야 하는 작업은 그 세션에서 얻은 핸들로 callback API를 호출한다.

`restartDefaultSession`은 이전 기본 설정을 물려받은 새 기본 세션을 만들고 이전 세션을 종료한다. 기록, 식별자, 출력 일련번호와 동결 정보는 새 수명으로 시작한다.

Subject는 객체 또는 함수이며 기본적으로 약하게 보관한다. 객체 조회에는 WeakMap을, 핸들이 참조하는 저장소의 Subject 보관에는 WeakRef를 사용한다. `strongReference`가 켜진 상태에서 만들어진 객체 저장소는 Subject를 강하게 보관한다. GC 시점이나 즉시 회수를 약속하지 않는다.

첫 공백 아닌 label은 유지하고 다른 label 재지정은 경고한다. 기록·태그·자동 이름 생성은 Subject의 `toString()`을 호출하지 않는다. 명시적으로 전달한 오류 메시지를 문자열로 바꾸는 동작은 이 보장에 포함하지 않는다.

최근 기록 버퍼는 객체별로 `maxLogCount`개를 보관하며 논리 문맥별 기록이 같은 버퍼 용량을 사용한다. 버퍼는 배열이므로 `maxLogCount`는 1 이상 2^32−1 이하의 정수로 제한하고, 객체 조회가 아닌 설정 시점에 검사한다. 버퍼 용량과 강한 참조 정책은 객체 저장소 생성 때 고정된다. 스레드별 버퍼나 CAS 자료구조를 그대로 이식하지 않는다.

## 4. 출력과 오류 처리

출력은 현재 sequence를 기준으로 객체 그래프와 이름을 복사한 스냅샷을 만든다. full 출력은 양방향 상호작용을, focused 출력은 incoming 상호작용을 따라간다. 방문 객체는 한 번만 포함하고 태그는 그래프를 확장하지 않는다.

출력 변환은 원본 기록을 바꾸지 않는다. 빈 스코프를 한 줄로 정리하고 부모 관계에서 깊이를 계산하며, 버퍼 순환으로 시작 기록이 없어져도 음수 깊이를 만들지 않는다. HTML은 객체 이름과 메시지를 이스케이프하며 외부 자원 없이 접기·펼치기와 연결 탐색을 제공한다. 보고서 스크립트는 타입 검사를 받는 소스에서 컴파일한 뒤 빌드 단계에서 문자열 상수로 고정한다. 따라서 소비 프로젝트의 번들러나 커버리지 변환이 보고서에 삽입되는 코드를 바꾸지 않는다.

출력의 순서는 다음과 같다.

1. 보관된 기록과 이름의 스냅샷을 만든다.
2. `{ content, fileName, mimeType }` artifact를 생성한다.
3. 명시적 동기 `onExport`가 있으면 호출한다.
4. callback이 없으면 Node의 `logDirectory` 파일 sink를 호출한다. 브라우저에는 자동 sink가 없다.
5. artifact를 호출자에게 반환한다.

파일 sink는 기존 파일을 덮어쓰지 않으며 충돌 시 선택한 최종 이름을 artifact에 반영한다. 파일 열기는 기본적으로 비활성화한다. Windows의 `explorer.exe`는 파일을 연 경우에도 종료 코드 1을 반환하므로 실행 실패만 경고하고, 다른 플랫폼은 0이 아닌 종료 코드도 경고한다. 브라우저 다운로드는 `downloadExport`의 명시적 호출로만 시작하며 실제 저장 경로와 완료 여부는 브라우저가 관리한다.

`writeError`와 `crashExport`는 메시지 문자열을 반환한다. 최초 crash 호출은 오류와 스택을 세션에 보존하고 기록을 동결한 뒤 출력한다. 출력 sink가 예외를 던지면 호출자에게 전달하지만 동결은 유지한다. 이후 일반 `export`로 같은 보관 기록을 다시 출력할 수 있다. 일반 출력 실패는 기록을 동결하지 않는다.

동결·종료로 `isValid`가 false인 기존 핸들도 저장소를 보관하므로 출력할 수 있다. 저장소가 없는 기본 핸들만 경고 후 `undefined`를 반환한다. 새로운 파일명 일련번호는 출력 시도마다 진행한다.

## 5. C# 구현과의 대응

| C# 기능 | TypeScript 대응 |
| --- | --- |
| `BlackboxHandle.Of(subject, label: ...)` | `BlackboxHandle.of(subject, { label })` |
| `using`과 Scope/Exert 핸들 | 명시적 dispose, callback API, 지원 런타임의 `Symbol.dispose` |
| ambient `BeginSession` | detached `createSession`과 callback 기반 `runSession` |
| 생성자의 out 핸들 | 먼저 `of()`로 핸들을 보관하고 `construct()`로 생성 스코프 기록 |
| 보간 문자열 handler | 메시지 함수 `() => string`을 통한 선택적 평가 |
| CallerMemberName | 선택적 `{ methodName }` 명시 |
| 스레드 문맥 | 논리 실행 문맥과 callback 분기 |
| .NET 타입 이름 | 런타임 생성자 이름 또는 Object/Function 표시 |
| 파일 출력 | 공통 artifact와 Node 파일 sink 또는 브라우저 다운로드 |

`BlackboxHandle.dispose`는 C#과 같이 객체의 종료 사실을 기록하는 도우미이다. 세션이나 Scope/Exert 핸들의 수명을 끝내는 dispose와 구분한다. 태그의 implicit string 변환 대신 `with()`의 문자열 반환값이나 `toString()`을 사용한다. C#의 `params object[]`와 달리 `with()`의 타입은 `TargetTypes`를 마지막 인자로만 허용하고, JavaScript 호출자가 다른 위치에 넘기면 TypeError로 거부한다. 런타임 생성자 이름은 minify된 번들에서 짧아질 수 있으므로 읽을 수 있는 이름이 필요하면 label을 지정한다.

기능의 기록 의미를 맞추되 C#의 스레드 경합, 고정 버킷 수, 슬롯 재사용과 할당량 수치를 JS 구현의 보장으로 옮기지 않는다. 시간은 ISO 문자열로, 실행 문맥은 context ID로 표시한다. C#과 출력 텍스트의 바이트 단위 동일성을 보장하지 않는다.

## 6. 검증 기준

공통 시나리오는 Node와 실제 Chromium·Firefox·WebKit에서 실행한다. 객체 동일성, 태그, full/focused 출력, 스코프 정리, await 이후 문맥, 세션 격리, 버퍼 제한과 HTML 이스케이프를 같은 결과 의미로 검증한다.

Node 전용 검증은 AsyncLocalStorage 전파, 다른 세션 핸들의 callback 분기, 파일 충돌 방지, 플랫폼별 파일 열기 실패 보고, 출력 callback 우선순위와 크래시 실패 후 재출력을 확인한다. 브라우저 검증은 명시적 문맥 격리, 다운로드 내용과 파일명, 생성한 HTML 동작을 확인한다. npm 패키지를 별도 소비 프로젝트에 설치해 공개 import와 TypeScript 선언도 검사한다.

실행 명령과 예제는 [사용 안내](../README.ko.md)를 따른다. 기존 [C# 구현 문서](../../docs/Wiki/02-Implementations.md)와 [C# 검증 기록](../../docs/Tests/03-Results.md)은 해당 런타임의 계약과 과거 결과를 설명한다.

## 7. 소스 색인

| 파일 | 내용 |
| --- | --- |
| `core.ts` | 공개 클래스 재노출과 공유 기본 세션 생성 |
| `session.ts` | 세션 설정·카운터·동결, 출력 스냅샷과 artifact 생성 |
| `context.ts` | 실행 분기와 객체별 스코프 스택 |
| `handles.ts` | 공개 핸들과 태그·스코프·상호작용 핸들 |
| `box.ts` | 객체 저장소, 최근 기록 버퍼와 상호작용 병합 |
| `settings.ts` | 기본 설정과 설정 검사 |
| `runtime.ts` | 플랫폼 hook, ambient 문맥 조회, 기본·명시적 세션 목록 |
| `lifetime.ts` | callback 완료 후 정리와 `Symbol.dispose` 연결 |
| `render.ts`, `html-template.ts`, `report-script.ts` | TXT·HTML 변환, 보고서 문서 틀과 스크립트 |
| `node.ts`, `open-export.ts`, `browser.ts` | 플랫폼 진입점, 파일 저장·열기, 브라우저 다운로드 |
| `types.ts`, `export-model.ts`, `public.ts` | 공개 타입, 출력 입력 모델, 공개 export 목록 |

`core.ts`는 다른 모듈이 모두 평가된 뒤 기본 세션을 만든다. 세션·문맥·핸들 모듈은 서로를 참조하지만 모듈 최상위에서는 서로의 클래스를 사용하지 않으므로 순환 import가 평가 순서에 영향을 주지 않는다.
