# Blackbox Test Results

This document records the results of organizing and running Blackbox tests based on `01-Tables.md` and `02-Plans.md`.

---

## 1. 260611 Documentation-Test Alignment Results

### 1-1. Applied Scope

- Split the Unity Test Runner axis, external NUnit default-recording axis, and runtime recording-disabled axis in `02-Plans.md`.
- Matched the test names in `02-Plans.md` with the current `Blackbox/Tests` implementation.
- Added valid/invalid handle paths for `WriteHandler` to `BlackboxHandleTests.cs`.
- In the invalid handler path, also verified through side effects that formatted expressions in interpolated strings are not evaluated.
- Added `TrimSmartSanitizesFileNameParts` to `ExportToolsTests.cs`.
- Created `03-Results.md` during this result-recording step.

### 1-2. Execution Results

| Category | Command | Result |
| --- | --- | --- |
| Blackbox runtime build | `dotnet build Blackbox.csproj --no-restore` | Succeeded. Unity reference conflicts and nullable-related warnings appeared, but there were no errors. |
| Blackbox tests build | `dotnet build Blackbox.Tests.csproj --no-restore` | Succeeded. Unity reference conflicts and nullable-related warnings appeared, but there were no errors. |
| Default recording tests | `dotnet test --no-restore Packages/com.blackthunder.blackbox-system/Tests/ExternalNUnitExecutor/ExternalNUnitExecutor.csproj` | Succeeded. At the time, the runner printed `total=97 passed=97 failed=0`; the current official command uses the external executor owned by the Blackbox package. |
| Recording-disabled tests | `UseBlackbox=false` runtime axis | Verified as fallback behavior in `BlackboxHandleTests`. |
| Whitespace check | `git diff --check` | Succeeded. |

### 1-3. Notes from Execution

- In the normal sandbox execution, building Unity-generated csproj files produced `MSB4184` because of an access permission issue under `C:\Users\user\AppData\Local\Microsoft SDKs`.
- When the same builds were retried in an elevated execution context, both `Blackbox.csproj` and `Blackbox.Tests.csproj` succeeded.
- The `bin` / `obj` artifacts generated after running `ExternalNUnitExecutor` were cleaned up because they were not code changes.
- New documentation and test files keep the repository's LF line-ending policy.
