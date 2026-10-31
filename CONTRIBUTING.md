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

Blackbox keeps its implementations in independent `csharp/` and `typescript/` project roots. Run commands in this guide from the repository root unless a language guide says otherwise.

### C#

- Install a stable .NET 10 SDK. The root `global.json` selects .NET 10 and rolls forward to the latest installed .NET 10 feature band; it does not install or change system SDKs.
- Restore the pinned `BlackThunder.UniTest` `0.4.0` package from the checked-in `csharp/packages/` local feed when building the Blackbox test project.
- Restore and build `csharp/BlackThunder.BlackboxSystem.sln` from the repository root.
- The library remains dependency-free and targets `netstandard2.1`.
- Tests, samples, and benchmarks target `net10.0`. The `csharp/Directory.Build.props` selects C# 9 and disables nullable analysis and implicit usings.
- Only `csharp/tests/BlackThunder.BlackboxSystem.Tests.CSharp10` overrides the language version, to C# 10, so it can verify the interpolated string handler path that C# 10 or later callers use.

Platform choices follow the shared [C# platform guidelines](guidelines/blackthunder-conventions/instructions/플랫폼%20기준%20-%20CSharp.md), including nullable usage and DLL-based integration with other repositories. Nullable reference types are permitted; the existing projects currently keep nullable analysis disabled.

A locally installed SDK may also be used without changing the machine's default SDK. If it is installed in the ignored `artifacts/dotnet/` directory, invoke `./artifacts/dotnet/dotnet.exe` on Windows in place of `dotnet`. The `global.json` requirement still applies. Installing a local SDK does not make it discoverable by an unrelated system `dotnet` executable.

Consumers pin stable `BlackThunder.BlackboxSystem` packages. Blackbox's own sample, benchmark, and C# 10 test projects keep a `ProjectReference` so development builds use the current source. The checked-in `csharp/packages/` directory is limited to immutable release packages and their provenance records; general NuGet caches and generated build output are not committed. The `csharp/NuGet.config` maps framework package IDs to this local feed and restores third-party dependencies from nuget.org.

### TypeScript

The npm package is developed in `typescript/` for Node.js and browsers. Follow its [guide](typescript/README.md) for the supported Node.js version, browser requirements, installation, scripts, and examples. Use the checked-in lockfile for reproducible dependency installation. Package versions and releases are independent of the C# package.

## Code Style

- C#, TypeScript, project settings, and documentation files use LF line endings.
- Code comments are written in English.
- Keep both runtime libraries free of external package dependencies. Development and test tools may use development dependencies.
- Update tests when changing shared behavior such as value-type handles, the export pipeline, or tag flow.

## Documentation Style

Existing Korean wiki documentation in `docs/Wiki` and the records in `docs/Tests` describe the C# implementation. TypeScript documentation is maintained with its [implementation guide](typescript/README.md). The root README introduces both implementations in English and Korean; the C# README pair lives in `csharp/`. Usage examples are maintained in [C# samples](csharp/samples/README.md), with the project at `csharp/samples/BlackThunder.BlackboxSystem.Samples.csproj` and scenario code in `csharp/samples/Scenarios`. Follow the shared [documentation convention](guidelines/blackthunder-conventions/conventions/01.%20dove%20documentation%20convention.md).

Keep the README pair aligned in structure, code blocks, and links. Use descriptive document titles and relative file links. Detailed session contracts belong in [the session document](docs/Wiki/05-Sessions.md); the README should provide the shortest useful starting flow. Dated test and benchmark reports retain the commands, paths, environments, and results from the time of execution, with current instructions identified separately.

Keep code identifiers unchanged. For example, names such as `ScopeHandle`, `TargetTypes`, and `BlackboxHandle.Export(...)` should stay as they are.

## Tests

Run the applicable verification for the changed area. The C# commands below are relative to the repository root. TypeScript checks are listed separately below.

- Documentation-only changes: check links, terminology, line endings, and trailing whitespace.
- Formatting and naming changes that preserve behavior: build the affected projects.
- Behavior changes: run the affected tests in `csharp/tests/BlackThunder.BlackboxSystem.Tests/BlackThunder.BlackboxSystem.Tests.csproj`.
- Changes across modules, public APIs, build settings, dependencies, or packaging: run both test projects.
- Interpolated message or `WriteHandler` changes: also run `csharp/tests/BlackThunder.BlackboxSystem.Tests.CSharp10/BlackThunder.BlackboxSystem.Tests.CSharp10.csproj`.
- Platform or handle-management changes: also run the full test project with `-p:BlackboxLockHandles=true`.
- UniTest table-flow changes: run the same test project against the pinned package; `BlackboxTableProject` uses `ExecutionMode.Inline` to preserve its global-state execution contract.
- Output or file-generation changes: verify both text and HTML output.
- Changes to recording disable behavior: also verify fallback behavior with `UseBlackbox = false`.
- Sample changes: verify interactive input and one-shot `help`, all scenarios, invalid commands, and error exit codes. One-shot execution must finish without opening a browser and write files only below the executable's `SampleOutput` directory.

The current benchmark project is `csharp/benchmarks/BlackThunder.BlackboxSystem.Benchmarks/BlackThunder.BlackboxSystem.Benchmarks.csproj`. It references the library project, is part of the solution build, and reaches internal registry types through `InternalsVisibleTo`. Run measurements separately when the task calls for performance evidence.

Briefly include verification results in the pull request.

C# 9 callers use the string overloads for interpolated messages, so formatted expressions are evaluated before the logging call even when recording is disabled. The public `WriteHandler` API remains available; automatic interpolation short-circuiting requires a caller compiled with C# 10 or later. Keep tests for explicit handlers separate from C# 9 string-overload fallback behavior; the C# 10 test project covers automatic handler binding.

### TypeScript Verification

Run the type check, build, Node.js tests, and browser tests documented in [the TypeScript guide](typescript/README.md). Verify the packed npm artifact as well as source-level behavior when changing package exports or distribution files. Test shared recording scenarios in both runtimes, and test Node.js async context and file output separately from browser context and downloads.

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
