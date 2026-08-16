# Contributing

Thank you for contributing to Blackbox. This document describes the basic standards for proposing changes or opening pull requests.

## Basic Principles

- Keep behavior and documentation in sync. Public API changes should update usage examples and wiki documentation together.
- If a test or verification step could not be run, mention the reason in the pull request.

## Development Environment

Blackbox uses an SDK-style .NET solution.

Basic checklist:

- Install a .NET 9 SDK.
- Keep the sibling `unitest` repository available when building the Blackbox test project.
- Restore and build `BlackThunder.BlackboxSystem.sln` from the repository root.
- The library remains dependency-free and targets `netstandard2.1`.

## Code Style

- C# files and documentation files use LF line endings.
- Code comments are written in English.
- Do not use nullable syntax.
- Do not add new dependencies.
- Update tests when changing shared behavior such as value-type handles, the export pipeline, or tag flow.

## Documentation Style

English documentation is in `docs/Wiki.en`. Korean documentation is in `docs/Wiki.ko`. Usage examples are maintained with `samples/Blackbox.NativeCSharp.Samples`.

Keep code identifiers unchanged. For example, names such as `ScopeHandle`, `TargetTypes`, and `BlackboxHandle.Export(...)` should stay as they are.

## Tests

Run the applicable verification for the changed area.

- Documentation-only changes: check links, terminology, line endings, and trailing whitespace.
- Code changes: run `dotnet test tests/BlackThunder.BlackboxSystem.Tests/BlackThunder.BlackboxSystem.Tests.csproj`.
- UniTest table-flow changes: run the same test project and confirm the sibling UniTest source links resolve.
- Output or file-generation changes: verify both text and HTML output.
- Changes to recording disable behavior: also verify fallback behavior with `UseBlackbox = false`.

Briefly include verification results in the pull request.

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
