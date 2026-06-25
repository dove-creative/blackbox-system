# Contributing

Thank you for contributing to Blackbox. This document describes the basic standards for proposing changes or opening pull requests.

## Basic Principles

- Keep behavior and documentation in sync. Public API changes should update usage examples and wiki documentation together.
- If a test or verification step could not be run, mention the reason in the pull request.

## Development Environment

Blackbox assumes a folder-based package structure inside a Unity project.

Basic checklist:

- The `Packages/com.blackthunder.blackbox-system` folder exists inside a Unity project.
- The test assembly uses the `BLACKBOX_TESTS` and `UNITY_INCLUDE_TESTS` symbols.
- Tests run on Unity Test Framework and NUnit.
- UniTest-based verification also keeps the sibling `Packages/com.blackthunder.unitest` package available.

## Code Style

- C# files and documentation files use LF line endings.
- Code comments are written in English.
- Do not use nullable syntax for Unity compatibility.
- Do not add new dependencies.
- Update tests when changing shared behavior such as value-type handles, the export pipeline, or tag flow.

## Documentation Style

English documentation is in `Documentation~/Wiki.en`. Korean documentation is in `Documentation~/Wiki.ko`. Usage examples are maintained together with the `Samples~/Unity` sample.

Keep code identifiers unchanged. For example, names such as `ScopeHandle`, `TargetTypes`, and `BlackboxHandle.Export(...)` should stay as they are.

## Tests

Run the applicable verification for the changed area.

- Documentation-only changes: check links, terminology, line endings, and trailing whitespace.
- Code changes: run `Blackbox.Tests` in Unity Test Framework.
- UniTest table-flow changes: build the external NUnit executor under `Tests/ExternalNUnitExecutor~`.
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
