# Blackbox System

[Korean README](README.ko.md)

Blackbox is a .NET tracing framework for recording object activity, execution scopes, and object-to-object interactions, then reading them later as connected logs.

Where a normal log focuses on a message at one moment, Blackbox records both 'what process this object went through' and 'which objects were connected during that process'.

<img src="docs/Images/blackbox-html-export.png" alt="Blackbox HTML export preview" width="900">

## Before You Start

Blackbox is a foundational tracing framework in the BlackThunder project family.

This repository is an early open-source project by dove-creative. If you find rough edges or areas to improve, please open a Discussion or contact vkdl4062@gmail.com.

Thank you for using Blackbox.

## Features

- Per-object log storage: creates a `Blackbox` for each target object and accumulates logs during execution.
- Scope records: records the start and end of processing ranges with `Scope(...)` and `using`.
- Interaction records: records object-to-object call relationships on both sides with `Exert(...)` and `ExertMessage(...)`.
- Tag links: attaches related targets to the source log with `Write(...).With(...)` or `Scope(...).With(...)`.
- Export: writes recorded logs to text or HTML files.
- Runtime switch: set `BlackboxHandle.UseBlackbox` to control whether recording handles are created and logs are written.

## Requirements and Installation

Consumers reference a pinned stable release of `BlackThunder.BlackboxSystem`. Register the directory containing the released `.nupkg` as a NuGet feed before restoring it.

```xml
<ItemGroup>
  <PackageReference Include="BlackThunder.BlackboxSystem" Version="[0.2.0]" />
</ItemGroup>
```

Samples in the Blackbox development repository use a `ProjectReference` to the current library project. The runtime library has no dependency on UniTest.

Building this repository and running its tests, samples, or benchmarks requires a .NET 10 SDK. The library targets `netstandard2.1`, executable projects target `net10.0`, and the language version is C# 9. See the [contribution guide](CONTRIBUTING.md) for shared-guideline submodule initialization and development rules.

Configure the log output location and logger early in execution.

```csharp
BlackboxHandle.Configure(
    logDirectory: Path.Combine(AppContext.BaseDirectory, "BlackboxLogs"),
    logger: Console.WriteLine);
```

Recording starts enabled by default. To temporarily stop recording, set `BlackboxHandle.UseBlackbox = false` or pass `useBlackbox: UseBlackboxOption.DoNotUse` to `Configure(...)`. When this switch is off, `BlackboxHandle.Of(subject)` returns an invalid handle and recording calls fall back to no-op/default behavior.

C# 9 callers use string overloads for interpolated messages, so expressions inside the string are evaluated first even when recording is disabled. Automatic short-circuiting through `WriteHandler` is supported for callers compiled with C# 10 or later.

## Quick Start

### Basic Usage Code

```csharp
using BlackThunder.BlackboxSystem;

public class Loader
{
    public Loader()
    {
        using var scope = BlackboxHandle.Of(this, label: "Loader").Construct();
    }

    public void Load(Worker worker)
    {
        using var _ = BlackboxHandle.Of(this).Scope("Load");

        BlackboxHandle.Of(this).Write("Prepare %0").With(worker);

        using (BlackboxHandle.Of(this).Exert(worker, "Worker.Load"))
        worker.Load();
    }

    public void ExportLogs() => BlackboxHandle.Of(this).Export();
}
```

This example records the following information.

- The `Load` scope of the `Loader` object
- The scope-start log
- Bidirectional interaction records for the `worker.Load()` call
- Connected log output centered on `Loader` when `ExportLogs()` is called

### Test Sessions

```csharp
using var session = BlackboxHandle.BeginSession();
BlackboxHandle.Configure(logDirectory, Console.WriteLine,
    openLogOption: OpenLogOption.Never);
var system = new SystemUnderTest();
await system.RunAsync();
```

Create objects inside their session and await/join all child work before disposal. Handles stay bound to the session in which they were acquired, and the global active list retains undisposed explicit sessions. See the [session and recording contract](docs/Wiki/05-Sessions.md) for isolation, leak diagnostics, default-session restart, and export behavior (Korean).

### C# Sample

Use the [console sample in samples](samples/README.md) to exercise the recording flows.

```powershell
dotnet run --project samples/BlackThunder.BlackboxSystem.Samples.csproj
dotnet run --project samples/BlackThunder.BlackboxSystem.Samples.csproj -- write
```

The first command opens the `sample>` prompt. Run `write`, `exert`, `tag`, or `exception` repeatedly; empty input is ignored, `help` shows usage, and `exit` closes the sample. The second command runs one scenario and exits without opening a browser. One-shot exit codes are 0 for success, 1 for execution failure, and 2 for an invalid command or arguments.

Recording uses the default `UseBlackbox = true` setting. Logs are written under `SampleOutput/Write`, `SampleOutput/Exert`, `SampleOutput/Tag`, or `SampleOutput/Exception` in the executable output directory. See the sample README for each command's purpose and interactive export-opening behavior.

## Main APIs

- `BlackboxHandle.Of(subject, label: name)`: gets the recording entry point for the target object in the current session. The label is optional; the first nonblank name wins, and omitting it preserves the existing name.
- `BlackboxHandle.BeginSession()`: begins an isolated test/debug session.
- `BlackboxHandle.ActiveSessionCount` / `GetActiveSessionKeys()`: observes undisposed explicit sessions across the process.
- `BlackboxHandle.DisposeActiveSessions()`: disposes a snapshot of those sessions for cleanup.
- `Write(message)`: records a one-line activity log.
- `Write(message).With(targets)`: attaches related targets to an activity log.
- `Scope(message)`: records a scope-start log and returns a `ScopeHandle`.
- `Scope(message).With(targets)`: attaches related targets to a scope-start log.
- `Exert(other, message)`: records an interaction with another object and returns an `ExertHandle` that can try to merge with the receiving-side scope.
- `ExertMessage(other, message)`: records a one-line interaction without merge behavior.
- `WriteError(message)`: records an error message and returns the same string.
- `Export(...)`: writes log files centered on the current object.
- `CrashExport(...)`: records an error and creates export output in one call.
- `RestartDefaultSession()`: ends and replaces the shared default session while preserving its settings; explicit sessions are unaffected.

## Documentation

- [Overview](docs/Wiki/01-Overview.md): purpose and main recording structures (Korean)
- [Usage](docs/Wiki/04-Usage.md): configuration and representative workflows (Korean)
- [Implementation](docs/Wiki/02-Implementations.md): component responsibilities and recording pipelines (Korean)
- [Session and recording contract](docs/Wiki/05-Sessions.md): lifecycle, default sessions, lock-free recording, and exports (Korean)

## Tests

Test code is in `tests/BlackThunder.BlackboxSystem.Tests` and `tests/BlackThunder.BlackboxSystem.Tests.CSharp10`, and both projects use NUnit.

The main test project compiles Blackbox runtime sources with `BLACKBOX_TESTS` for exporter hooks and references the stable `BlackThunder.UniTest` package at version `0.4.0`. `BlackboxTableProject` selects `ExecutionMode.Inline` to preserve the existing execution behavior around global Blackbox state.

`BlackThunder.BlackboxSystem.Tests.CSharp10` references the library project and compiles with C# 10, the only exception to the repository-wide C# 9 setting. It verifies that callers compiled with C# 10 or later bind interpolated messages to `WriteHandler` and skip formatted expressions while recording is disabled.

The root `NuGet.config` uses the `packages/` local feed and nuget.org. `packages/` contains the pinned UniTest release package and its provenance record, and the framework package IDs map exclusively to the local feed. General restore caches and `bin/obj` remain outside version control.

```powershell
dotnet test tests/BlackThunder.BlackboxSystem.Tests/BlackThunder.BlackboxSystem.Tests.csproj
dotnet test tests/BlackThunder.BlackboxSystem.Tests.CSharp10/BlackThunder.BlackboxSystem.Tests.CSharp10.csproj
```

See the [session validation report](docs/Tests/04-Sessions.md) and [recording benchmark](benchmarks/README.md).

## License

Blackbox is distributed under the MIT license. See [LICENSE.md](LICENSE.md) for details.
