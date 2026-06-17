# Native C# Usage Sample

This sample runs the same Blackbox usage scenarios as the Unity sample without using Unity APIs.

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
