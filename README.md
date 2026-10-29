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

## Test sessions

```csharp
using var session = BlackboxSession.Begin();
BlackboxHandle.Configure(logDirectory, Console.WriteLine,
    openLogOption: OpenLogOption.Never);
var system = new SystemUnderTest();
await system.RunAsync();
```

Create the objects inside their test session and await/join all child work before disposing it. `Begin()` is synchronous; ordinary Task/Thread execution-context flow inherits the session. Open sessions in test bodies rather than relying on NUnit SetUp to propagate AsyncLocal values. A session copies its parent's settings and isolates its storage, crash state and resets.

Handles remain bound to the session and generation in which they were acquired, including handles cached by constructors. Using such an object in another session does not move its handle. Without `Begin()`, callers use the shared default session; tests that reset or configure that default must run serially.

`Export()` can be repeated while recording continues. `CrashExport()` freezes only its own generation and exports once; a failed crash export can be retried with ordinary `Export()`. Live exports are approximate snapshots. After all work has completed, exports contain the stable records still retained by the ring buffers. Files have a short session key and export ordinal and never overwrite an earlier export; retention is the caller's responsibility.

`ForceReset()` retires the current generation. Old handles stop recording. In an explicit session, existing child execution contexts also remain in the retired generation. Unbound default callers instead follow the shared current default generation. Retirement racing a writer does not reuse its buffers: an in-flight write may finish in the old generation, while later writes are rejected. Retained handles can still export old data.

Subject `ToString()` is never called automatically. Assign an optional name once:

```csharp
using var scope = BlackboxHandle.Of(this).Label(Name).Construct(out _bb);
```

The display is `Forest (MonsterHome #2)`, or `MonsterHome #2` without a label. The first nonblank label wins. Record changing domain state explicitly in messages. Subjects are weakly held by default; `StrongReference` keeps them alive while their Blackbox remains reachable, including through retained handles after Dispose. Internal recording and registration use no locks, but immutable log publication allocates; runtime, GC, I/O and caller-provided formatting/loggers are outside that guarantee.

## Installation

Consumers reference a pinned stable release of `BlackThunder.BlackboxSystem`. Register the directory containing the released `.nupkg` as a NuGet feed before restoring it.

```xml
<ItemGroup>
  <PackageReference Include="BlackThunder.BlackboxSystem" Version="[0.2.0]" />
</ItemGroup>
```

Samples in the Blackbox development repository use a `ProjectReference` to the current library project. The runtime library has no dependency on UniTest.

Configure the log output location and logger early in execution.

```csharp
BlackboxHandle.Configure(
	logDirectory: Path.Combine(AppContext.BaseDirectory, "BlackboxLogs"),
	logger: Console.WriteLine);
```

Recording starts enabled by default. To temporarily stop recording, set `BlackboxHandle.UseBlackbox = false` or pass `useBlackbox: UseBlackboxOption.DoNotUse` to `Configure(...)`. When this switch is off, `BlackboxHandle.Of(subject)` returns an invalid handle and recording calls fall back to no-op/default behavior.

## Quick Start

### Basic Usage Code

```csharp
using BlackThunder.BlackboxSystem;

public class Loader
{
    public Loader()
    {
        using var scope = BlackboxHandle.Of(this).Label("Loader").Construct();
    }

    public void Load(Worker worker)
    {
        using var _ = BlackboxHandle.Of(this).Scope("Load");

        BlackboxHandle.Of(this).Write("Prepare %0").With(worker);

        using (BlackboxHandle.Of(this).Exert(worker, "Worker.Load"))
		worker.Load();
    }

    public void ExportLogs()
    {
        BlackboxHandle.Of(this).Export();
    }
}
```

This example records the following information.

- The `Load` scope of the `Loader` object
- The scope-start log
- Bidirectional interaction records for the `worker.Load()` call
- Connected log output centered on `Loader` when `ExportLogs()` is called

### C# Sample

Run the console sample under `samples/Blackbox.NativeCSharp.Samples` to exercise the recording flows.

```powershell
dotnet run --project samples/Blackbox.NativeCSharp.Samples/Blackbox.NativeCSharp.Samples.csproj
```

At the `sample>` prompt, enter `write`, `exert`, `tag`, or `exception`. Empty input waits for another command, and `exit` closes the sample.

The sample runs with the default `UseBlackbox = true` setting. Logs are written under `BlackboxSystem/Samples/NativeCSharp` in the sample output directory.

## Main APIs

- `BlackboxHandle.Of(subject)`: gets the recording entry point for the target object.
- `BlackboxSession.Begin()`: begins an isolated test/debug session.
- `Label(name)`: assigns the first nonblank display label and returns the same handle.
- `Write(message)`: records a one-line activity log.
- `Write(message).With(targets)`: attaches related targets to an activity log.
- `Scope(message)`: records a scope-start log and returns a `ScopeHandle`.
- `Scope(message).With(targets)`: attaches related targets to a scope-start log.
- `Exert(other, message)`: records an interaction with another object and returns an `ExertHandle` that can try to merge with the receiving-side scope.
- `ExertMessage(other, message)`: records a one-line interaction without merge behavior.
- `WriteError(message)`: records an error message and returns the same string.
- `Export(...)`: writes log files centered on the current object.
- `CrashExport(...)`: records an error and creates export output in one call.
- `ForceReset()`: replaces the current session's generation and invalidates its old recording handles.

## Documentation

See the [0.2.0 session and recording contract](docs/Wiki/05-Sessions.md) for lifecycle, default-session, lock-free recording and export details (Korean).

## Tests

Test code is in `tests/BlackThunder.BlackboxSystem.Tests` and uses NUnit.

The test project compiles Blackbox runtime sources with `BLACKBOX_TESTS` for exporter hooks and references the stable `BlackThunder.UniTest` package at version `0.2.0`. `BlackboxTableProject` selects `ExecutionMode.Inline` to preserve the existing execution behavior around global Blackbox state.

The root `NuGet.config` uses the `packages/` local feed and nuget.org. `packages/` contains the pinned UniTest release package and its provenance record, and the framework package IDs map exclusively to the local feed. General restore caches and `bin/obj` remain outside version control.

```powershell
dotnet test tests/BlackThunder.BlackboxSystem.Tests/BlackThunder.BlackboxSystem.Tests.csproj
```

See the [session validation report](docs/Tests/04-Sessions.md) and [recording benchmark](benchmarks/README.md).

## License

Blackbox is distributed under the MIT license. See [LICENSE.md](LICENSE.md) for details.
