# Blackbox System for TypeScript

[한국어](README.ko.md)

Blackbox is a TypeScript tracing library that records object activity, execution scopes, and object-to-object interactions, then presents them as TXT or HTML.

Node.js and browsers use the same recording engine. Ordinary service code starts with `BlackboxHandle.of(subject)` without passing a session or context. See the [C# guide](../csharp/README.md) for the C# implementation.

## Features

- Per-object records, immutable display labels, and weak Subject references
- Scopes, object-to-object interactions, and related-object tags
- Recording switches, session isolation, disposal, and leak diagnostics
- Bounded recent history and connected TXT/HTML export
- Error records, first-crash freezing, and export retries

## Requirements and Installation

Node.js 22 or later is supported. Browsers need ES2022 modules, `WeakRef`, Web Crypto, Blob, and object URLs. The package has no runtime dependencies and ships ESM with TypeScript declarations. A separate CommonJS build is not provided.

For repository development, run from `typescript/`:

~~~sh
npm ci
npm run build
~~~

The package name is `@blackthunder/blackbox-system`. Repository changes do not publish it to the npm registry. Before publication, a consumer can install a package produced by `npm pack`.

~~~sh
npm pack
npm install /path/to/blackthunder-blackbox-system-0.1.0.tgz
~~~

Run the first command here and the second in the consumer project. A package-root import selects the Node entry in Node.js and the browser entry in other environments. Explicit `/node` and `/browser` entries are also available. Without a browser build tool, serve the generated `dist/browser.js` over HTTP as shown in the [browser sample](samples/browser.html).

## Quick Start

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

This records scopes, tags, and interactions in the shared default session. `export()` returns the generated content and also invokes a configured output callback or Node file output. Subjects do not need a tracing interface or field.

A Subject is a non-null object or function. Use primitives such as strings and numbers as messages or labels. Subject `toString()` is never called to create display names. The first nonblank label is retained; conflicting labels produce a warning. Type names come from the runtime constructor name, which minifiers may shorten. Pass a label when production bundles need readable names.

## Scopes and Async Context

`runScope(message, callback)` and `runExert(other, message, callback)` return the callback's result and clean up handles after Promise completion or exceptions. Disabling recording does not skip the callback itself.

In Node.js, AsyncLocalStorage carries the callback context, so existing service calls to `BlackboxHandle.of()` continue to work.

~~~ts
await BlackboxHandle.runSession(async () => {
    await BlackboxHandle.of(loader).runScope("Load", async () => {
        await Promise.resolve();
        BlackboxHandle.of(loader).write("Loaded");
    });
});
~~~

Browsers do not automatically propagate an ambient async context. Use the callback context only when parallel sessions or scopes need isolation.

~~~ts
await BlackboxHandle.runSession(async context => {
    await context.of(loader).runScope("Load", async scopeContext => {
        await Promise.resolve();
        scopeContext.of(loader).write("Loaded");
    });
});
~~~

In the browser, an ordinary `BlackboxHandle.of()` inside that callback still uses the shared default session. Do not assume that manual scopes shared across `await` automatically isolate independent operations. Ordinary synchronous recording remains context-free, as in the quick start.

Starting a receiver scope immediately inside `runExert` allows the adjacent incoming interaction and scope to merge. Merging never skips intervening records in that execution flow or crosses independent contexts or sessions. Manual `scope()/exert()` handles are also available, and repeated `dispose()` is safe.

## Sessions and Lifetimes

- `createSession(options?)` creates a detached session without changing the current context. Record through `session.of(subject)` and call `session.dispose()` yourself.
- `session.run(callback)` executes in an existing session's context. It does not automatically dispose the session.
- `runSession(callback, options?)` creates a session and disposes it when the callback completes.
- Node.js `session.run/runSession` binds ambient `of()` calls. In the browser, use the supplied `context.of()`.
- Handles stay bound to their original session. Node static `of()` handles follow the current ambient branch within that session; `session.of()/context.of()` handles stay bound to their explicit context. Disposal rejects new records but retained history remains exportable.
- `runScope()/runExert()` callbacks run in a branch of the handle's session. In Node.js, calling them on a handle bound to another session switches the ambient session for the callback, so ordinary `of()` calls inside it record to the handle's session rather than the enclosing `runSession`.
- `restartDefaultSession()` replaces only the shared default session and preserves its settings. Explicit sessions are unaffected.
- `activeSessionCount`, `getActiveSessionKeys()`, and `disposeActiveSessions()` diagnose and clean up undisposed explicit sessions. The default session is excluded.

`createSession()` differs from C#'s ambient `BeginSession()`. `BlackboxHandle.dispose()` writes an object's 'Disposed' record; it does not end the recording store or session. Scope, Exert, and Session `dispose()` methods end their corresponding lifetimes. When the runtime supports `Symbol.dispose`, those three types support `using`. Otherwise, use explicit disposal or callback APIs.

## Tags, Errors, and Export

`write("Prepare %0").with(target)` substitutes the target's name and writes a supplementary record on the target. `with()` and `with(null)` differ. A final `TargetTypes` argument selects how much source information appears in target records. Tags do not expand the export graph; interactions connect objects during graph traversal.

`writeError(message, options?)` returns the message string. Use `targets: [{ context: "worker", target: worker }]` to attach error-related subjects. Passing `exceptionHandling: "crashExport"` or calling `crashExport(message, options?)` directly freezes the session on its first crash, preserves the error and stack, and exports it. `crashExport()` also returns the message string.

`export({ format: "text" })`, or the default HTML export, returns `{ content, fileName, mimeType }`. An invalid handle without a recording store returns `undefined`. Full output follows interactions in both directions; `fullExport: false` follows only incoming influences on the root object. `recursionDepth: 0` includes only the root, while a negative depth removes the limit.

Ordinary exports can be repeated and do not stop recording. Synchronous output callback failures propagate to the caller. Recording continues after an ordinary export failure. A failed crash export keeps the session frozen, and `export()` can retry its output. Crash information is retained outside the ring buffer.

### Node.js File Output

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

Setting `logDirectory` saves exports synchronously. Existing files are never overwritten: collisions produce a new name, which is also reflected in the artifact's `fileName`. Opening output files is disabled by default and is attempted only with `openLog: true`. An opening failure keeps the saved file and reports through the warning logger. On Windows, `explorer.exe` returns exit code 1 even after opening the file, so only launch failures are reported.

### Browser Downloads and Output Callbacks

~~~ts
import { BlackboxHandle, downloadExport } from "@blackthunder/blackbox-system/browser";

const artifact = BlackboxHandle.of(loader).export();
if (artifact) downloadExport(artifact);
~~~

Call `downloadExport()` explicitly, for example from a user-requested download button. The browser controls the actual destination and completion. Browser `logDirectory/openLog` settings do not save files or open them automatically.

Use `configure({ onExport: artifact => { /* Consume synchronously. */ } })` to receive output directly. An explicit `onExport` takes precedence over Node's `logDirectory` sink. Async callbacks are unsupported. Browser `crashExport()` without a sink does not download automatically; configure `onExport` to capture the artifact immediately or call `export()` afterward.

## Settings and Disabled Recording

| Setting | Default |
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

`maxLogCount` and `strongReference` apply when a new object store is created. Configuration targets the current session, and new explicit sessions inherit current settings at creation. Browser static `createSession/runSession` calls inherit default-session settings. Use `context.createSession()/context.runSession()` when a nested browser session needs its parent's settings. Configure an explicit browser session through `session.configure()` or `context.configure()`.

`BlackboxHandle.useBlackbox = false` disables recording through new and existing handles. To skip expensive message evaluation, pass a function such as `write(() => buildMessage())`. Ordinary template strings are evaluated before the call. Disabled string messages retain their fallback text; the fallback for an unevaluated message function is empty.

## Samples and Verification

Run these commands from `typescript/`. The Node sample never opens a browser and writes output only below `samples/output/`.

~~~sh
npm run sample -- all
npm run sample -- help
npm run sample
~~~

Choose `write`, `exert`, `tag`, `exception`, or `async`. `all` runs every scenario once. With no arguments, the `sample>` prompt allows repeated commands and supports `help/exit`. Unknown commands return exit code 2, and unexpected execution failures return 1.

For the browser sample, run `npm run build`, serve `typescript/` with any local static HTTP server, and open `/samples/browser.html`. The sample uses no external network resources. Run a scenario to generate an artifact, then use its download button to save it.

~~~sh
npm run typecheck
npm run build
npm test
npx playwright install chromium firefox webkit
npm run test:browser
npm run test:package
~~~

Browser installation is an initial verification setup step, not a library runtime dependency. On Windows, a shell started by an MSIX-packaged desktop app virtualizes AppData, so Firefox cannot resolve its side-by-side assembly there; run browser tests from a regular terminal or set `PLAYWRIGHT_BROWSERS_PATH` outside AppData. `test:package` packs the previously built `dist/` and checks imports and declarations in a separate consumer project. See [Architecture](docs/architecture.md) for behavior contracts and platform differences.

## License

MIT. See [LICENSE.md](../LICENSE.md) in the repository or `dist/LICENSE.md` in the published package.
