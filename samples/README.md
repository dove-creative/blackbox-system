# Blackbox Usage Samples

This console app demonstrates object activity, interactions, tags, and crash exports using the current Blackbox library project.

## Run

Use a .NET 10 SDK. Run these commands from the repository root.

Start the interactive prompt:

```powershell
dotnet run --project samples/BlackThunder.BlackboxSystem.Samples.csproj
```

Run a scenario once and exit:

```powershell
dotnet run --project samples/BlackThunder.BlackboxSystem.Samples.csproj -- write
dotnet run --project samples/BlackThunder.BlackboxSystem.Samples.csproj -- exception
dotnet run --project samples/BlackThunder.BlackboxSystem.Samples.csproj -- help
```

Commands supplied as arguments finish without waiting for console input and never open a browser. Without arguments, the app opens the `sample>` prompt. Empty input asks again, `help` lists the commands, and `exit` or end of input closes the app. Invalid commands and unexpected errors leave the interactive prompt available.

## Commands

| Command | Alias | Purpose |
| --- | --- | --- |
| `write` | `1` | Record changes to one object and nested execution scopes. |
| `exert` | `2` | Connect activity from objects interacting with one another. |
| `tag` | `3` | Attach related objects as context for recorded activity. |
| `exception` | `4` | Record a deliberate failure and export its crash history. |
| `help` | | Display the command list. |
| `exit` | | Exit successfully, including when supplied as a command argument. |

Commands are case-insensitive and do not accept additional arguments. The exception scenario handles only its deliberate failure and reports it as an expected result.

| Exit code | Meaning |
| --- | --- |
| `0` | Success, including the expected exception scenario, help, and exit. |
| `1` | An unexpected execution or output error occurred. |
| `2` | The command is unknown or includes unsupported arguments. |

Usage and execution errors are written to stderr. Successful scenarios print a short completion message and their output directory; detailed recorded activity stays in the exported file.

## Output

HTML files are written below the sample executable's directory, using `SampleOutput/Write`, `SampleOutput/Exert`, `SampleOutput/Tag`, or `SampleOutput/Exception`. The exception scenario creates a file whose name starts with `[CRASH]`. Source directories are not used as output locations.

During interactive execution, `write`, `exert`, and `tag` ask the operating system to open the exported HTML file. The exception scenario always leaves its crash file unopened. Browser-launch warnings are written to stderr.

Each scenario opens its own `using var session = BlackboxHandle.BeginSession()` before configuring Blackbox and constructing objects. Cached handles stay bound to that session, and disposal ends the session after the scenario completes. Constructors use explicit labels for readable identities; Blackbox does not call the subject's `ToString()` automatically.

Ordinary exports can be repeated and continue recording. A crash freezes only its own session. Each export creates a new file, so remove old sample output when it is no longer needed.
