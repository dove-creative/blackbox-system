# Changelog

This file records public changes to Blackbox.

The format follows Keep a Changelog, and version numbers follow Semantic Versioning.

## [Unreleased]

### Changed

- Execute C# regression assertions through UniTest while retaining NUnit discovery, parameterized cases, and fixture lifetime. Table checks now require every registered scenario to complete, with public API and test-integration guards to detect omissions.
- Split the repository into `csharp/` and `typescript/` implementation roots and pinned this repository to the .NET 10 SDK through `global.json`. The C# library continues to target `netstandard2.1` with C# 9.
- Adopted the pinned BlackThunder Conventions submodule and its C# platform guidelines: repository-wide C# 9, `netstandard2.1` for the library, and `net10.0` for tests, samples, and benchmarks. C# 9 callers use string-overload interpolation; automatic `WriteHandler` short-circuiting remains available to C# 10 or later callers.
- Applied the shared C# and documentation conventions across runtime, tests, samples, and benchmarks, including private-field names, member grouping, formatting, document titles, and relative links.
- Declared `BlackboxHandle` as a `readonly` struct. Its references to the recording object and session remain fixed; the nested `WriteHandler` remains a mutable `ref struct` for message construction.
- Moved the single sample project to `samples/BlackThunder.BlackboxSystem.Samples.csproj`, grouped named scenarios under `samples/Scenarios`, and placed exports under the executable's `SampleOutput` directory. Command-line scenarios now run once without opening a browser and return exit code 0 on success, 1 on execution failure, or 2 for invalid commands or arguments. Interactive execution retains the prompt and each scenario's export-opening behavior.
- Renamed the benchmark project to `BlackThunder.BlackboxSystem.Benchmarks` and separated current run instructions from dated measurement records.
- Shortened the English and Korean README entry points and consolidated detailed session diagnostics in the session documentation while preserving historical test and benchmark results.
- Updated Blackbox tests to the pinned `BlackThunder.UniTest` `0.4.0` package. Table tests import the `BlackThunder.UniTest` namespace and print failures as JSON reports.
- Moved the shared `Nullable` and `ImplicitUsings` settings into `Directory.Build.props`.
- The benchmark project now references the library project instead of compiling runtime sources, so measurements use the `netstandard2.1` build. It is included in the solution, and `InternalsVisibleTo` now names only the benchmark assembly.
- Grouped runtime members under the shared `Front`, `Internal`, and `Content` regions, with named subgroups where a region has several.
- Split the TypeScript recording engine into session, context, handle, object-store, settings, and runtime modules. Package exports and generated output are unchanged.
- Documented that TypeScript `runScope()/runExert()` callbacks run in a branch of the handle's session; in Node.js, a handle bound to another session also switches the ambient session for the callback.
- Replaced `BlackboxHandle.Of(subject).Label(name)` with `BlackboxHandle.Of(subject, label: name)` and removed the public `Label` method. The optional label keeps the first-nonblank-name policy within each session; omitted or blank labels preserve the existing name, and conflicting names keep the first value and warn. Existing source calls to `Of(subject)` remain valid; consumers must rebuild against the updated signature.

### Added

- Added UniTest session transition chains, missing public-contract edge cases, and C# 10 interpolation checks for every recording overload across active, default, disabled, disposed, and frozen handles.
- Added the initial `@blackthunder/blackbox-system` TypeScript implementation for Node.js and browsers, with object records, scopes, interactions, tags, isolated sessions, TXT/HTML export, and crash diagnostics. Node.js propagates async contexts; browser callers pass a context only when isolating parallel work.
- Added TypeScript samples, package-consumer and real-browser checks, and separate C# and TypeScript CI jobs. npm publication remains a separate release step.
- Added `tests/BlackThunder.BlackboxSystem.Tests.CSharp10`, which compiles with C# 10 and verifies that callers compiled with C# 10 or later bind interpolated messages to `WriteHandler` and skip formatted expressions while recording is disabled.

### Fixed

- Ignore `ScopeHandle.With(...)` after the scope has been disposed, preventing stale scopes from tagging old records or registering new targets.
- Apply the same disposed-scope guard to TypeScript `ScopeHandle.with(...)`, including automatically closed inner scopes, with shared Node/browser regressions.
- Stop reporting successful TypeScript `openLog` launches on Windows as failures. `explorer.exe` returns exit code 1 after opening a file, so Windows reports only launch errors.
- Reject TypeScript `maxLogCount` values above the JavaScript array limit of 2^32 - 1 during configuration instead of failing at the first object lookup.
- Embed the TypeScript HTML report script as a build-time string literal so consumer bundler or coverage transforms cannot break exported reports.
- Accept TypeScript `TargetTypes` only as the final `with(...)` argument in declarations, and reject other positions at runtime with a descriptive TypeError.

## [0.2.0] - 2026-10-29

### Added

- Added synchronous `BlackboxHandle.BeginSession()` with ambient session binding, copied settings, nested restoration and isolated crash state. Each session has one recording lifetime; start another with Dispose followed by BeginSession.
- Added `BlackboxHandle.Label(string)` for an explicit, set-once display name without invoking Subject code.
- Added read-only `BlackboxHandle.CurrentSessionKey`, `BlackboxHandle.DefaultSessionKey` and `BlackboxHandle.SessionKey` diagnostics without exposing session objects or allocating during reads.
- Added `BlackboxHandle.ActiveSessionCount`, `GetActiveSessionKeys()` and `DisposeActiveSessions()` to detect and clean up undisposed explicit sessions. A locked, process-wide list strongly retains them until disposal; the default session and restart candidates are excluded. Keys are returned as a snapshot in start order. Bulk disposal runs outside the list lock and leaves other execution flows bound to their disposed sessions. Recording and registration remain lock-free.
- Added deterministic retirement/publication race tests, weak-reference lifetime checks and a reproducible recording benchmark.

### Changed

- Replaced `BlackboxHandle.ForceReset()` with `BlackboxHandle.RestartDefaultSession()`. Restart disposes and replaces only the shared default session, preserves its settings and assigns a new session key. Explicit sessions and their execution-context bindings are unaffected.
- Ordinary exports are repeatable and keep recording active. Crash export freezes only its own session, with crash metadata retained separately from the ring buffers.
- Handles stay bound to the session in which they were acquired. After that session is disposed or the default session restarts, old handles stop recording without resetting the global slot pools. Retained handles can still export their old records.
- Replaced ConditionalWeakTable/ThreadLocal registration with CAS-published identity buckets and context lists. Published ring entries are immutable, and updates replace entries atomically.
- Removed automatic Subject `ToString()` calls. Object references now render as `Type #Id` or `Label (Type #Id)`; `OwnerString` returns the label or type name.
- Strong-reference mode now retains Subjects while their Blackboxes remain reachable; it no longer has ConditionalWeakTable ephemeron semantics.
- Export files include session keys and ordinals and use exclusive creation. Files accumulate until the caller removes them.
- Warnings are emitted once per session and cause, including cross-thread scope/exert disposal.

### Fixed

- Prevented negative scope depth when a ring buffer has overwritten a scope's opening record.
- Prevented stale ring slots and delayed publications from being mistaken for the current logical record.
- Reclaimed abandoned handle slots during session retirement, including registration racing retirement and contexts collected before their session.
- Failed ordinary exports no longer disable recording; failed crash exports can be retried with ordinary export.
- Bounded handle tracker ownership to 16 shared stripes per handle type in each session, preventing write-only object churn from retaining a tracker pair per collected context. Registration/retirement still uses full IDs and lock-free CAS.
- Expanded the default session's subject table to 65,536 buckets (explicit sessions remain at 256). Registered lookups check hashes before dereferencing owners and do not perform cleanup.
- Added a rotating, lock-free dead-entry sweep of up to 16 buckets after each successful new Subject registration. This reduces retained log buffers during object churn without adding work to registered lookups; it does not promise immediate collection when registrations stop or a fixed heap limit.

## [0.1.2] - 2026-10-15

### Added

- Added NuGet package metadata for `BlackThunder.BlackboxSystem`, including the MIT license, README, and source repository commit.
- Added an explicit local NuGet feed for pinned framework release packages and their provenance records.

### Changed

- Changed Blackbox tests to consume `BlackThunder.UniTest` `0.2.0` as a private, exact-version package dependency instead of linking sibling source files.
- Selected `ExecutionMode.Inline` for the Blackbox table project while preserving `BLACKBOX_TESTS` exporter hooks.
- Excluded generated `bin` and `obj` files from the Blackbox runtime sources compiled by its tests.
- Converted the active library, tests, samples, and documentation to a .NET-only repository layout.
- Changed the library target to `netstandard2.1` and the tests and console sample to `net9.0`.
- Removed the Unity package metadata, assemblies, assets, and editor-only log exporter.

## [0.1.1] - 2026-06-17

### Added

- Added native C# usage samples under `Samples~/NativeCSharp` for running the `Write`, `Exert`, `Tag`, and exception-recording scenarios.
- Added an interactive native sample launcher that keeps accepting commands until `exit` is entered.
- Added the bool `BlackboxHandle.UseBlackbox` property and `Configure(..., useBlackbox: UseBlackboxOption.DoNotUse)` as runtime switches for disabling recording.
- Added contribution branch naming guidance for short-lived branches from `main` using the `<username>/<topic>` format.

### Changed

- Changed the public C# namespace and Unity asmdef names from `com.BlackThunder.BlackboxSystem` to `BlackThunder.BlackboxSystem`, and set the Unity package ID to `com.blackthunder.blackbox-system` so it matches the repository and embedded package folder naming.
- Changed `BLACKBOX` to only select Unity's default recording state; native C# and non-Unity builds record by default unless disabled at runtime.
- Updated package sample metadata so usage samples are registered separately by target environment.

## [0.1.0] - 2026-06-11

### Added

- Added the `BlackboxHandle` runtime for recording per-object activity, execution scopes, tags, and object-to-object interactions.
- Added an export pipeline for writing connected Blackbox logs to text and HTML files.
- Added Unity usage samples that demonstrate `Write`, `Scope`, `Exert`, tags, and error-recording flows.
- Added Korean and English wiki documentation, README, CONTRIBUTING, and MIT license files for the public package.
- Added Unity Test Framework/NUnit coverage for the runtime, handles, export tools, and integration flows.
