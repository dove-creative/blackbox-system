# Blackbox System for TypeScript

[English](README.md)

Blackbox는 객체별 활동, 실행 스코프, 객체 간 상호작용을 기록하고 TXT·HTML로 읽는 TypeScript 추적 라이브러리이다.

Node.js와 브라우저가 같은 기록 엔진을 사용한다. 일반 서비스 코드는 `BlackboxHandle.of(subject)`로 시작하며, 별도 세션이나 문맥을 전달할 필요가 없다. C# 구현은 [C# 안내](../csharp/README.ko.md)를 참고한다.

## 주요 기능

- 객체별 기록, 불변 표시 이름과 약한 Subject 참조
- 스코프와 객체 간 상호작용, 관련 객체 태그
- 기록 비활성화, 세션 격리·종료·누락 진단
- 최근 기록을 보관하는 버퍼와 연결된 TXT·HTML 출력
- 오류 기록, 최초 크래시 동결과 출력 재시도

## 요구 사항과 설치

Node.js 22 이상을 지원한다. 브라우저는 ES2022 모듈, `WeakRef`, Web Crypto, Blob과 object URL을 지원해야 한다. 런타임 의존성은 없으며 ESM과 TypeScript 선언 파일을 배포한다. CommonJS 전용 빌드는 제공하지 않는다.

저장소에서 개발할 때는 `typescript/`에서 실행한다.

~~~sh
npm ci
npm run build
~~~

패키지 이름은 `@blackthunder/blackbox-system`이다. 이 저장소 변경만으로 npm 레지스트리에 게시되지는 않는다. 게시 전에는 `npm pack`으로 만든 패키지를 소비 프로젝트에 설치할 수 있다.

~~~sh
npm pack
npm install /path/to/blackthunder-blackbox-system-0.1.0.tgz
~~~

첫 명령은 이 디렉터리에서, 두 번째 명령은 소비 프로젝트에서 실행한다. 패키지 루트 import는 Node.js에서 Node 진입점을, 그 외 환경에서는 브라우저 진입점을 선택한다. `/node`와 `/browser` 진입점을 명시할 수도 있다. 빌드 도구 없이 브라우저에서 사용할 때는 [브라우저 샘플](samples/browser.html)처럼 생성된 `dist/browser.js`를 HTTP로 제공한다.

## 빠른 시작

~~~ts
import { BlackboxHandle } from "@blackthunder/blackbox-system";

const loader = {};
const worker = {};
const trace = BlackboxHandle.of(loader, { label: "Loader" });
const scope = trace.scope("Load", { methodName: "load" });

try {
    trace.write("Prepare %0").with(worker);
    trace.exertMessage(worker, "Dispatch");
} finally {
    scope.dispose();
}

const artifact = trace.export();
console.log(artifact?.fileName);
~~~

이 코드는 공유 기본 세션에서 객체의 스코프, 태그와 상호작용을 기록한다. `export()`는 결과 내용을 반환하며, 설정된 출력 콜백이나 Node 파일 출력이 있으면 함께 실행한다. 객체는 인터페이스나 기록 필드를 구현할 필요가 없다.

Subject는 null이 아닌 객체 또는 함수이다. 문자열·숫자 같은 원시값은 메시지나 label로 사용한다. 이름을 만들 때 Subject의 `toString()`은 호출하지 않는다. 첫 공백 아닌 label이 유지되고, 충돌하는 label은 경고한다. 형식 이름은 런타임 생성자 이름이므로 minify된 번들에서는 짧게 바뀔 수 있다. 운영 빌드에서도 읽을 수 있는 이름이 필요하면 label을 지정한다.

## 스코프와 비동기 문맥

`runScope(message, callback)`와 `runExert(other, message, callback)`는 callback의 반환값을 돌려주고, Promise가 완료되거나 예외가 발생해도 핸들을 정리한다. 기록이 비활성화되어도 callback 자체는 실행한다.

Node.js에서는 AsyncLocalStorage가 callback 문맥을 전달하므로 기존 서비스 코드의 `BlackboxHandle.of()` 호출을 그대로 사용할 수 있다.

~~~ts
await BlackboxHandle.runSession(async () => {
    await BlackboxHandle.of(loader).runScope("Load", async () => {
        await Promise.resolve();
        BlackboxHandle.of(loader).write("Loaded");
    });
});
~~~

브라우저는 비동기 ambient 문맥을 자동 전파하지 않는다. 병렬 세션이나 스코프를 분리할 때만 callback으로 전달된 문맥을 사용한다.

~~~ts
await BlackboxHandle.runSession(async context => {
    await context.of(loader).runScope("Load", async scopeContext => {
        await Promise.resolve();
        scopeContext.of(loader).write("Loaded");
    });
});
~~~

브라우저에서 위 callback 안의 일반 `BlackboxHandle.of()`는 여전히 공유 기본 세션을 사용한다. `await`를 가로질러 수동 스코프를 공유하며 독립 작업이 자동으로 분리된다고 가정하지 않는다. 일반적인 동기 기록은 빠른 시작처럼 문맥 없이 사용한다.

`runExert` 내부에서 받는 객체의 스코프를 바로 시작하면 인접한 받는 쪽 상호작용과 스코프를 병합한다. 해당 실행 흐름의 중간 기록을 건너뛰거나 독립 문맥·세션을 가로질러 병합하지 않는다. 수동 `scope()/exert()`도 사용할 수 있으며 중복 `dispose()`는 안전하다.

## 세션과 수명

- `createSession(options?)`는 분리된 세션을 만들며 현재 문맥을 바꾸지 않는다. `session.of(subject)`로 기록하고 직접 `session.dispose()`한다.
- `session.run(callback)`은 기존 세션의 문맥에서 실행한다. 세션을 자동 종료하지 않는다.
- `runSession(callback, options?)`은 새 세션을 만들고 callback 완료 시 종료한다.
- Node.js의 `session.run/runSession`에서는 ambient `of()`가 세션을 선택한다. 브라우저에서는 전달받은 `context.of()`를 사용한다.
- 핸들은 생성 당시 세션에 고정된다. Node의 정적 `of()` 핸들은 같은 세션의 현재 ambient 분기를 따르고, `session.of()/context.of()` 핸들은 명시적 문맥에 고정된다. 종료 후 기록은 거부하지만 보관된 기록의 출력은 가능하다.
- `runScope()/runExert()` callback은 핸들이 속한 세션의 분기에서 실행된다. Node.js에서 다른 세션에 고정된 핸들로 호출하면 callback 동안 ambient 세션도 그 핸들의 세션으로 바뀌므로, 안쪽의 일반 `of()`는 바깥 `runSession`이 아닌 핸들의 세션에 기록한다.
- `restartDefaultSession()`은 설정을 유지하며 공유 기본 세션만 교체한다. 명시적 세션은 유지한다.
- `activeSessionCount`, `getActiveSessionKeys()`, `disposeActiveSessions()`로 미종료 명시적 세션을 진단하고 정리한다. 기본 세션은 이 목록에 포함하지 않는다.

`createSession()`은 C#의 ambient `BeginSession()`과 다르다. `BlackboxHandle.dispose()`는 객체의 'Disposed' 로그를 남기는 메서드이며, 기록 저장소나 세션을 종료하지 않는다. Scope·Exert·Session의 `dispose()`가 해당 수명을 끝낸다. 런타임이 `Symbol.dispose`를 지원하면 이 세 타입에 `using`을 사용할 수 있으며, 지원하지 않는 환경에서는 명시적 dispose나 callback API를 사용한다.

## 태그, 오류와 출력

`write("Prepare %0").with(target)`는 대상 이름을 치환하고 대상 쪽에 보조 기록을 남긴다. `with()`와 `with(null)`은 다르다. 마지막 인자로 `TargetTypes` 플래그를 넘겨 대상 기록의 정보량을 선택할 수 있다. 태그는 출력 그래프를 확장하지 않으며, 상호작용만 객체 간 출력 경로가 된다.

`writeError(message, options?)`는 메시지 문자열을 반환한다. `targets: [{ context: "worker", target: worker }]`로 오류 관련 대상을 붙일 수 있다. `exceptionHandling: "crashExport"`를 지정하거나 `crashExport(message, options?)`를 직접 호출하면 최초 호출만 세션을 동결하고 오류·스택을 보존한 뒤 출력한다. `crashExport()`의 반환값도 메시지 문자열이다.

`export({ format: "text" })` 또는 기본 HTML 출력은 `{ content, fileName, mimeType }`를 반환한다. 기록 저장소가 없는 무효 핸들은 `undefined`를 반환한다. 기본 출력은 양방향 상호작용을 포함하며 `fullExport: false`는 기준 객체에 영향을 준 incoming 방향만 따라간다. `recursionDepth: 0`은 기준 객체만, 음수는 깊이 제한 없이 수집한다.

일반 출력은 반복 가능하고 기록을 멈추지 않는다. 동기 출력 콜백이 실패하면 예외를 전파한다. 일반 출력 실패 후에도 기록할 수 있고, 크래시 출력 실패 후에는 동결을 유지한 채 `export()`로 다시 출력할 수 있다. 크래시 정보는 링 버퍼에서 밀려나지 않는다.

### Node.js 파일 저장

~~~ts
import { BlackboxHandle, saveExport } from "@blackthunder/blackbox-system/node";

BlackboxHandle.configure({ logDirectory: "./logs", openLog: false });
BlackboxHandle.of(loader).write("Ready");
BlackboxHandle.of(loader).export();

// Save an artifact received from another session or output callback.
saveExport(
    { content: "trace", fileName: "trace.txt", mimeType: "text/plain" },
    { directory: "./logs" },
);
~~~

`logDirectory`가 설정되면 출력 시 동기적으로 파일을 저장한다. 기존 파일을 덮어쓰지 않고 충돌 시 새 이름을 선택하며, artifact의 `fileName`에도 최종 이름을 반영한다. 파일 열기는 기본적으로 꺼져 있고 `openLog: true`일 때만 시도한다. 열기 실패는 파일을 보존하고 warning logger에 알린다. Windows의 `explorer.exe`는 파일을 연 경우에도 종료 코드 1을 반환하므로 실행 자체의 실패만 알린다.

### 브라우저 다운로드와 출력 콜백

~~~ts
import { BlackboxHandle, downloadExport } from "@blackthunder/blackbox-system/browser";

const artifact = BlackboxHandle.of(loader).export();
if (artifact) downloadExport(artifact);
~~~

`downloadExport()`는 사용자가 다운로드를 요청한 버튼 이벤트 등에서 명시적으로 호출한다. 브라우저가 실제 저장 위치와 완료 여부를 관리한다. 브라우저의 `logDirectory/openLog`는 파일 저장이나 자동 열기를 수행하지 않는다.

`configure({ onExport: artifact => { /* Consume synchronously. */ } })`로 출력 결과를 직접 받을 수 있다. 명시적 `onExport`는 Node의 `logDirectory` 저장보다 우선한다. 비동기 callback은 지원하지 않는다. 브라우저에서 sink가 없는 `crashExport()`는 자동 다운로드하지 않으므로 결과를 즉시 받으려면 `onExport`를 설정하거나 이후 `export()`를 호출한다.

## 설정과 비활성화

| 설정 | 기본값 |
| --- | --- |
| `useBlackbox` | `true` |
| `maxLogCount` | `100` |
| `defaultRecursionDepth` | `100` |
| `strongReference` | `false` |
| `exportFormat` | `"html"` |
| `fullExport` | `true` |
| `openLog` | `false` |
| `exceptionHandling` | `"none"` |
| `tagTargetTypes` | `TargetTypes.Full` |

`maxLogCount`와 `strongReference`는 새 객체 저장소를 만들 때 적용된다. 설정은 현재 세션에 적용하며, 새 명시적 세션은 생성 시 현재 설정을 상속한다. 브라우저의 정적 `createSession/runSession`은 기본 세션의 설정을 상속한다. 중첩 브라우저 세션이 부모 설정을 상속해야 하면 `context.createSession()/context.runSession()`을 사용한다. 브라우저의 명시적 세션 설정에는 `session.configure()` 또는 `context.configure()`를 사용한다.

`BlackboxHandle.useBlackbox = false`는 신규·기존 핸들의 기록을 비활성화한다. 값비싼 메시지 평가를 건너뛰려면 `write(() => buildMessage())`처럼 함수를 전달한다. 일반 템플릿 문자열은 호출 전에 평가된다. 비활성 상태의 문자열 메시지는 fallback으로 유지하지만, 평가하지 않은 메시지 함수의 fallback은 빈 문자열이다.

## 샘플과 검증

아래 명령은 `typescript/`에서 실행한다. Node 샘플은 브라우저를 열지 않으며 출력은 `samples/output/`에만 저장한다.

~~~sh
npm run sample -- all
npm run sample -- help
npm run sample
~~~

`write`, `exert`, `tag`, `exception`, `async` 중 하나를 선택할 수 있다. `all`은 전체 시나리오를 한 번 실행한다. 인자가 없으면 `sample>` 프롬프트에서 반복 실행하며 `help/exit`를 지원한다. 알 수 없는 명령은 종료 코드 2, 예상하지 못한 실행 실패는 1을 반환한다.

브라우저 샘플은 `npm run build` 후 `typescript/`를 임의의 로컬 정적 HTTP 서버로 제공하고 `/samples/browser.html`을 연다. 샘플은 외부 네트워크 자원을 사용하지 않는다. 시나리오 실행으로 결과를 만들고 다운로드 버튼을 눌러 저장한다.

~~~sh
npm run typecheck
npm run build
npm test
npx playwright install chromium firefox webkit
npm run test:browser
npm run test:package
~~~

브라우저 설치는 검증에 필요한 최초 준비이며 라이브러리의 런타임 의존성이 아니다. Windows에서 MSIX로 설치된 데스크톱 앱이 연 셸은 AppData를 가상화하므로 Firefox가 side-by-side 어셈블리를 찾지 못한다. 이때는 일반 터미널에서 브라우저 테스트를 실행하거나 `PLAYWRIGHT_BROWSERS_PATH`를 AppData 밖으로 지정한다. `test:package`는 앞서 생성한 `dist/`를 패키징하고 별도 소비 프로젝트의 import와 선언 파일을 확인한다. 동작 계약과 플랫폼 차이는 [아키텍처](docs/architecture.md)를 참고한다.

## 라이선스

MIT 라이선스이다. 저장소에서는 [LICENSE.md](../LICENSE.md)를, 배포 패키지에서는 `dist/LICENSE.md`를 참고한다.
