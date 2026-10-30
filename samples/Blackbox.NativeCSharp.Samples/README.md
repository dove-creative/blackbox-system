# Native C# Usage Sample

This console sample runs the Blackbox usage scenarios against the library project.

## Run

```powershell
dotnet run --project Blackbox.NativeCSharp.Samples.csproj
```

Enter a command at the `sample>` prompt. Empty input asks again without running a scenario, and `exit` closes the sample app.

```text
write
exert
tag
exception
help
exit
```

The sample runs with the default `UseBlackbox = true` setting. Logs are written under the sample app output directory at `BlackboxSystem/Samples/NativeCSharp`, split into one folder per scenario.

Each scenario opens its own `using var session = BlackboxHandle.BeginSession()` before configuring Blackbox and constructing the objects. Cached handles stay bound to that session. Constructors use `Label(Name).Construct(out _bb)` for readable identities; Blackbox never calls the subject's `ToString()` automatically.

Ordinary exports can be repeated and continue recording. Crash output freezes only its session. Each export creates a new file, so remove old sample output when it is no longer needed.
