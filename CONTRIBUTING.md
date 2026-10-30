# Contributing

Thank you for contributing to Blackbox. This document describes the basic standards for proposing changes or opening pull requests.

## Shared Guidelines

Code and documentation conventions are maintained in the [BlackThunder Conventions submodule](guidelines/blackthunder-conventions/README.md). The root [AGENTS.md](AGENTS.md) selects the applicable rules and describes Blackbox's verification requirements.

Initialize the pinned guidelines after cloning this repository:

```powershell
git submodule update --init --recursive
```

The submodule contains guideline documents and is not a NuGet package or build input. The parent repository records its commit; updating that commit is an explicit change to the shared rules used by Blackbox.

## Basic Principles

- Keep behavior and documentation in sync. Public API changes should update usage examples and wiki documentation together.
- If a test or verification step could not be run, mention the reason in the pull request.

## Development Environment

Blackbox uses an SDK-style .NET solution.

Basic checklist:

- Install a .NET 10 SDK.
- Restore the pinned `BlackThunder.UniTest` `0.2.0` package from the checked-in `packages/` local feed when building the Blackbox test project.
- Restore and build `BlackThunder.BlackboxSystem.sln` from the repository root.
- The library remains dependency-free and targets `netstandard2.1`.
- Tests, samples, and benchmarks target `net10.0`. The repository-wide `Directory.Build.props` selects C# 9.

Platform choices follow the shared [C# platform guidelines](guidelines/blackthunder-conventions/instructions/플랫폼%20기준%20-%20CSharp.md), including nullable usage and DLL-based integration with other repositories. Nullable reference types are permitted; the existing projects currently keep nullable analysis disabled.

Consumers pin stable `BlackThunder.BlackboxSystem` packages. Blackbox's own sample keeps its `ProjectReference` so development builds use the current source. The checked-in `packages/` directory is limited to immutable release packages and their provenance records; general NuGet caches and generated build output are not committed. The root `NuGet.config` maps framework package IDs to this local feed and restores third-party dependencies from nuget.org.

## Code Style

- C# files and documentation files use LF line endings.
- Code comments are written in English.
- Keep the runtime library free of external package dependencies.
- Update tests when changing shared behavior such as value-type handles, the export pipeline, or tag flow.

## Documentation Style

Korean wiki documentation is in `docs/Wiki`. The root README is available in English and Korean. Usage examples are maintained in [samples](samples/README.md), with the project at `samples/BlackThunder.BlackboxSystem.Samples.csproj` and scenario code in `samples/Scenarios`. Follow the shared [documentation convention](guidelines/blackthunder-conventions/conventions/01.%20dove%20documentation%20convention.md).

Keep the README pair aligned in structure, code blocks, and links. Use descriptive document titles and relative file links. Detailed session contracts belong in [the session document](docs/Wiki/05-Sessions.md); the README should provide the shortest useful starting flow. Dated test and benchmark reports retain the commands, paths, environments, and results from the time of execution, with current instructions identified separately.

Keep code identifiers unchanged. For example, names such as `ScopeHandle`, `TargetTypes`, and `BlackboxHandle.Export(...)` should stay as they are.

## Tests

Run the applicable verification for the changed area.

- Documentation-only changes: check links, terminology, line endings, and trailing whitespace.
- Formatting and naming changes that preserve behavior: build the affected projects.
- Behavior changes: run the affected tests in `tests/BlackThunder.BlackboxSystem.Tests/BlackThunder.BlackboxSystem.Tests.csproj`.
- Changes across modules, public APIs, build settings, dependencies, or packaging: run the full test project.
- Platform or handle-management changes: also run the full test project with `-p:BlackboxLockHandles=true`.
- UniTest table-flow changes: run the same test project against the pinned package; `BlackboxTableProject` uses `ExecutionMode.Inline` to preserve its global-state execution contract.
- Output or file-generation changes: verify both text and HTML output.
- Changes to recording disable behavior: also verify fallback behavior with `UseBlackbox = false`.
- Sample changes: verify interactive input and one-shot `help`, all scenarios, invalid commands, and error exit codes. One-shot execution must finish without opening a browser and write files only below the executable's `SampleOutput` directory.

The current benchmark project is `benchmarks/BlackThunder.BlackboxSystem.Benchmarks/BlackThunder.BlackboxSystem.Benchmarks.csproj`. Build it when shared runtime sources or project settings change; run measurements separately when the task calls for performance evidence.

Briefly include verification results in the pull request.

C# 9 callers use the string overloads for interpolated messages, so formatted expressions are evaluated before the logging call even when recording is disabled. The public `WriteHandler` API remains available; automatic interpolation short-circuiting requires a caller compiled with C# 10 or later. Keep tests for explicit handlers separate from C# 9 string-overload fallback behavior.

## Branch Naming

When submitting changes through a pull request, create a short-lived branch from the latest `main`.

Use this format:

- `<username>/<topic>`

Use lowercase kebab-case for `<topic>` when possible.

Examples:

- `dove/wiki-locale`
- `dove/html-export-fix`
- `dove/readme-install`
- `dove/sample-usage`

Avoid vague long-lived branch names such as `<username>/work`, `<username>/update`, or `<username>/main`.

## Pull Request

A pull request should include:

- Why the change was made
- Main changes
- Verification that was run, or verification that could not be run and why
- Whether documentation was updated

## License

Contributed code is considered distributed under this repository's MIT license. When bringing in external code or materials, check the original license and notice requirements, and add a separate notice file if needed.
