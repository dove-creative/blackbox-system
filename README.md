# Blackbox

[Korean README](README.ko.md)

Blackbox is a Unity/C# tracing framework for recording object activity, execution scopes, and object-to-object interactions, then reading them later as connected logs.

Where a normal log focuses on a message at one moment, Blackbox records both 'what process this object went through' and 'which objects were connected during that process'.

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
- Disable switch: when the `BLACKBOX` symbol is missing, the main recording calls do not write actual logs.

## Installation

The current structure assumes a folder-based Unity package.

1. Place this folder at `Packages/com.blackthunder.blackboxsystem` in a Unity project.
2. Enable the `BLACKBOX` symbol in Player Settings or asmdef define constraints.
3. Configure the log output location early in execution.

```csharp
using System.IO;
using com.BlackThunder.BlackboxSystem;
using UnityEngine;

public static class BlackboxBootstrap
{
    public static void Initialize()
    {
        BlackboxHandle.Configure(
            logDirectory: Path.Combine(Application.persistentDataPath, "BlackboxLogs"),
            logger: Debug.Log);
    }
}
```

If the `BLACKBOX` symbol is not defined, most recording calls fall back to default values or return the original message. This can be used to remove recording cost from release builds.

## Quick Start

```csharp
using com.BlackThunder.BlackboxSystem;

public class Loader
{
    public Loader()
    {
        BlackboxHandle.Of(this).Construct("Loader");
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

## Main APIs

- `BlackboxHandle.Of(subject)`: gets the recording entry point for the target object.
- `Write(message)`: records a one-line activity log.
- `Write(message).With(targets)`: attaches related targets to an activity log.
- `Scope(message)`: records a scope-start log and returns a `ScopeHandle`.
- `Scope(message).With(targets)`: attaches related targets to a scope-start log.
- `Exert(other, message)`: records an interaction with another object and returns an `ExertHandle` that can try to merge with the receiving-side scope.
- `ExertMessage(other, message)`: records a one-line interaction without merge behavior.
- `WriteError(message)`: records an error message and returns the same string.
- `Export(...)`: writes log files centered on the current object.
- `CrashExport(...)`: records an error and creates export output in one call.
- `ForceReset()`: clears internal state for tests or repeated experiments.

## Documentation

Detailed documentation is available in `Documentation~/Wiki-ENG`.

- [01-Overview.md](Documentation~/Wiki-ENG/01-Overview.md): purpose and overall flow
- [02-Implementations.md](Documentation~/Wiki-ENG/02-Implementations.md): implementation structure
- [02.1-Tag-Flow.md](Documentation~/Wiki-ENG/02.1-Tag-Flow.md): tag-linking flow
- [02.2-Export-Pipeline.md](Documentation~/Wiki-ENG/02.2-Export-Pipeline.md): export pipeline
- [02.3-Handle-Lifecycle.md](Documentation~/Wiki-ENG/02.3-Handle-Lifecycle.md): handle lifecycle
- [03-Object-Diagram.md](Documentation~/Wiki-ENG/03-Object-Diagram.md): object relationship diagram
- [04-Usage.md](Documentation~/Wiki-ENG/04-Usage.md): usage examples and call guidelines

Korean documentation is available in `Documentation~/Wiki-KOR`.

## Tests

Test code is in the `Tests` folder and uses Unity Test Framework with NUnit.

To run tests in Unity, use an Editor test environment where `BLACKBOX`, `BLACKBOX_TESTS`, and `UNITY_INCLUDE_TESTS` are enabled. If the package is used as a separated package, also check the Unity project's testables settings and test asmdef settings.

## License

Blackbox is distributed under the MIT license. See [LICENSE.md](LICENSE.md) for details.
