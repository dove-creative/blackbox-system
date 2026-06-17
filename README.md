# Blackbox

[Korean README](README.ko.md)

Blackbox is a Unity/C# tracing framework for recording object activity, execution scopes, and object-to-object interactions, then reading them later as connected logs.

Where a normal log focuses on a message at one moment, Blackbox records both 'what process this object went through' and 'which objects were connected during that process'.

<img src="Documentation~/Images/blackbox-html-export.png" alt="Blackbox HTML export preview" width="900">

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

## Installation

The current structure supports both Unity Package Manager installation and folder-based package installation.

### Install with Package Manager

1. In Unity, open `Window > Package Manager`.
2. Click the `+` button in the upper-left corner, then select `Add package from git URL...`.
3. Enter the URL below and click `Add`.

```text
https://github.com/dove-creative/blackbox-system.git
```

### Install as a Folder

1. Place this folder at `Packages/com.blackthunder.blackboxsystem` in a Unity project.

### Configure After Installation

1. Configure the log output location early in execution.

```csharp
BlackboxHandle.Configure(
	logDirectory: Path.Combine(Application.persistentDataPath, "BlackboxLogs"),
	logger: Debug.Log);
```

In Unity, recording starts disabled by default unless the `BLACKBOX` scripting define symbol is present. Add `BLACKBOX` in Player Settings or set `BlackboxHandle.UseBlackbox = true` during startup to enable recording by default.

If you want to temporarily stop recording, set `BlackboxHandle.UseBlackbox = false` or pass `useBlackbox: UseBlackboxOption.DoNotUse` to `Configure(...)`. When this runtime switch is off, `BlackboxHandle.Of(subject)` returns an invalid handle and recording calls fall back to no-op/default behavior.

## Quick Start

```csharp
using BlackThunder.BlackboxSystem;

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

Detailed documentation is available in `Documentation~/Wiki.en`.

- [01-Overview.md](Documentation~/Wiki.en/01-Overview.md): purpose and overall flow
- [02-Implementations.md](Documentation~/Wiki.en/02-Implementations.md): implementation structure
- [02.1-Tag-Flow.md](Documentation~/Wiki.en/02.1-Tag-Flow.md): tag-linking flow
- [02.2-Export-Pipeline.md](Documentation~/Wiki.en/02.2-Export-Pipeline.md): export pipeline
- [02.3-Handle-Lifecycle.md](Documentation~/Wiki.en/02.3-Handle-Lifecycle.md): handle lifecycle
- [03-Object-Diagram.md](Documentation~/Wiki.en/03-Object-Diagram.md): object relationship diagram
- [04-Usage.md](Documentation~/Wiki.en/04-Usage.md): usage examples and call guidelines

Korean documentation is available in `Documentation~/Wiki.ko`.

## Tests

Test code is in the `Tests` folder and uses Unity Test Framework with NUnit.

To run tests in Unity, use an Editor test environment where `BLACKBOX_TESTS` and `UNITY_INCLUDE_TESTS` are enabled. Add `BLACKBOX` too when the Unity default recording state should be enabled. If the package is used as a separated package, also check the Unity project's testables settings and test asmdef settings.

## License

Blackbox is distributed under the MIT license. See [LICENSE.md](LICENSE.md) for details.
