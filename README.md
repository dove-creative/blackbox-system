# Blackbox System

[한국어](README.ko.md)

Blackbox is a tracing framework for recording object activity, execution scopes, and object-to-object interactions, then reading them as connected logs.

It records what an object went through and which objects were connected during that process. This repository provides the same recording concepts in C# and TypeScript, with independent builds, tests, and packages for each implementation.

<img src="docs/Images/blackbox-html-export.png" alt="Blackbox HTML export preview" width="900">

## Before You Start

Blackbox is a foundational tracing framework in the BlackThunder project family.

If you find a problem or have a suggestion, please open a [Discussion](https://github.com/dove-creative/blackbox-system/discussions) or contact vkdl4062@gmail.com. Thank you for using Blackbox.

## Features

- Per-object records: retains each object's activity and state changes as its own history.
- Scope records: connects the start and end of processing ranges to show execution flow.
- Interactions and tags: links object-to-object calls and related targets to the records.
- Sessions and runtime switch: separates recording lifetimes and disables recording when needed.
- TXT and HTML export: turns connected records and error context into readable output.

## Language Guides

| Implementation | Runtime | Package | Getting started |
| --- | --- | --- | --- |
| C# | .NET Standard 2.1 compatible runtimes | `BlackThunder.BlackboxSystem` | [Installation and usage](csharp/README.md) |
| TypeScript | Node.js and browsers | `@blackthunder/blackbox-system` | [Installation and usage](typescript/README.md) |

Each implementation's guide covers installation, a minimal example, samples, and test commands. Async context and output behavior follow the contract of the selected implementation.

## Repository Layout

```text
csharp/        C# source, tests, samples, benchmarks, and NuGet settings
typescript/    TypeScript source, tests, samples, and npm settings
docs/          C# implementation docs, diagrams, and existing validation records
guidelines/    Shared code and documentation guidelines
```

C# and TypeScript packages are versioned and released independently. See the [contribution guide](CONTRIBUTING.md) for shared-guideline initialization, development, and verification.

## Documentation

- [C# overview](docs/Wiki/01-Overview.md): purpose and main recording structures
- [C# usage](docs/Wiki/04-Usage.md): configuration and representative workflows
- [C# session and recording contract](docs/Wiki/05-Sessions.md): lifetimes, async context, and exports
- [C# validation records](docs/Tests/03-Results.md): results from existing implementation stages
- [TypeScript guide](typescript/README.md): Node.js and browser usage and verification

## License

Blackbox is distributed under the MIT license. See [LICENSE.md](LICENSE.md) for details.
