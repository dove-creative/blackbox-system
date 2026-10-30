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
using var session = BlackboxHandle.BeginSession();
BlackboxHandle.Configure(logDirectory, Console.WriteLine,
    openLogOption: OpenLogOption.Never);
var system = new SystemUnderTest();
await system.RunAsync();
```

Create the objects inside their test session and await/join all child work before disposing it. `BeginSession()` is synchronous; ordinary Task/Thread execution-context flow inherits the session. Open sessions in test bodies rather than relying on NUnit SetUp to propagate AsyncLocal values. A session copies its parent's settings and isolates its storage and crash state. Each session has one recording lifetime; dispose it and call BeginSession() again to start another explicit session.

Handles remain bound to the session in which they were acquired, including handles cached by constructors. Using such an object in another session does not move its handle. Without `BeginSession()`, callers use the shared default session; tests that restart or configure that default must run serially.

**Undisposed explicit sessions are not released.** A process-wide list holds strong references until `Dispose()` or `BlackboxHandle.DisposeActiveSessions()` is called. Dropping references and running GC does not release a session or its outstanding Scope/Exert handle IDs; without cleanup they remain until process exit. `ActiveSessionCount` counts these sessions, and `GetActiveSessionKeys()` returns an independent `Guid[]` snapshot in start order. The default session and restart candidates are excluded.

Check for missed disposal before cleanup in NUnit TearDown:

```csharp
[NonParallelizable]
public abstract class BlackboxTestBase
{
    [TearDown]
    public void CheckSessions()
    {
        try
        {
            Assert.That(BlackboxHandle.ActiveSessionCount, Is.Zero,
                "Undisposed Blackbox sessions: " +
                string.Join(", ", BlackboxHandle.GetActiveSessionKeys()));
        }
        finally
        {
            BlackboxHandle.DisposeActiveSessions();
        }
    }
}
```

The list is global: parallel tests see each other's sessions, and bulk cleanup can dispose another test's sessions. Use this check with serial tests after all child work finishes. Bulk cleanup snapshots under a lock and disposes outside it in reverse start order, restoring nested bindings in the calling flow. Other flows keep their disposed bindings and reject recording until they call `Dispose()` themselves. Sessions started after the snapshot are not included. Only the first `Dispose()` ends a session and removes it from the list; every call restores the previous binding in the calling flow when that flow is still bound to the session.

Use `BlackboxHandle.CurrentSessionKey` and `BlackboxHandle.DefaultSessionKey` to inspect the current/default session identity. If an object's constructor cached `_bb` before `BeginSession()`, its valid handle still reports the default key inside the new session. Diagnose that situation with:

```csharp
if (_bb.SessionKey != BlackboxHandle.CurrentSessionKey)
{
    // This object was created in another session.
}
```

These read-only `Guid` properties expose no session object and perform no locking or allocation. An invalid handle (`IsValid == false`) returns `Guid.Empty`; check validity before interpreting a mismatch. Keys identify session lifetimes; restarting the default session changes DefaultSessionKey.

`Export()` can be repeated while recording continues. `CrashExport()` freezes only its own session and exports once; a failed crash export can be retried with ordinary `Export()`. Live exports are approximate snapshots. After all work has completed, exports contain the stable records still retained by the ring buffers. Files have a short session key and export ordinal and never overwrite an earlier export; retention is the caller's responsibility.

`BlackboxHandle.RestartDefaultSession()` replaces only the shared default session, inherits its settings and disposes it. The replacement gets a new key, storage, counters, export ordinal and crash/warning state. Explicit sessions and their child execution contexts are unchanged, even when called inside one. Unbound callers subsequently use the new default; old handles stay with the disposed session and stop recording. A write racing disposal may finish in the old session, whose buffers are never reused. Retained handles can still export old data. A child bound to a disposed explicit session remains there and cannot silently record into the default session.

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
- `BlackboxHandle.BeginSession()`: begins an isolated test/debug session.
- `BlackboxHandle.ActiveSessionCount` / `GetActiveSessionKeys()`: observes undisposed explicit sessions across the process.
- `BlackboxHandle.DisposeActiveSessions()`: disposes a snapshot of those sessions for cleanup.
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
- `RestartDefaultSession()`: ends and replaces the shared default session while preserving its settings; explicit sessions are unaffected.

## Documentation

See the [session and recording contract](docs/Wiki/05-Sessions.md) for lifecycle, default-session, lock-free recording and export details (Korean).

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
