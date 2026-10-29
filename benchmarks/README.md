# Recording benchmark

The console harness measures the same public write/scope/exert calls and internal registry lookup on one shared default session. Each worker warms up 500 operations before the measurement. Registration measures 2,000 operations per worker; the other cases measure 20,000. There are 1 and 24 workers on this machine. Threads share an owner and interaction target, and each thread receives its own log context.

## 2026-10-29 comparison

Windows x64, .NET SDK 9.0.320, Release, `DOTNET_TieredCompilation=0`. The baseline is commit `9f20c862171e5de6ab4c663c62f8a00070e9ca35` (0.1.2); the after column is the 0.2.0 session implementation. Both versions use the same harness. Before/after processes were alternated for three rounds with no tests running concurrently. Values below are the median of the three rounds.

`ns/op` is wall-clock elapsed time divided by **all workers' completed operations**. The 24-worker column measures aggregate throughput, not individual call latency. These short runs are a local comparison, not a statistical performance guarantee.

| Operation | Before, 1 worker ns/op | After, 1 worker ns/op | Before, 24 workers ns/op | After, 24 workers ns/op |
|---|---:|---:|---:|---:|
| Register fresh subject | 504.45 | 526.60 | 1393.00 | 1380.44 |
| Lookup registered subject | 23.40 | 18.95 | 2.69 | 2.82 |
| Write constant message | 93.96 | 168.72 | 38.37 | 59.19 |
| Open and close scope | 768.21 | 445.95 | 813.04 | 776.79 |
| Open and close exert | 2899.33 | 1773.92 | 685.37 | 765.20 |

The immutable publication design costs approximately **152 bytes per write**, compared with zero steady-state allocation in the baseline. Scope open/close allocates about 352 bytes (before: 368); an exert pair allocates 304 bytes (before: zero). Registered lookup remains allocation-free. Measurements include small process/harness allocations; these are rounded when giving per-operation allocations.

Writes took about 80% longer with one worker and 54% longer in the 24-worker aggregate measurement. This is the tradeoff for safely copying and replacing complete records during concurrent export, reset and disposal. Removing locks does not imply faster execution or no garbage collection. Single-worker scope/exert improvements should not be generalized to every workload; the multi-worker exert case became slower.

## Reproduce

```powershell
$env:DOTNET_TieredCompilation = '0'
dotnet run --project benchmarks/Blackbox.Benchmarks -c Release
```

Build before timing, then repeat with `--no-build`. The harness prints CSV containing elapsed time, aggregate cost and process allocation deltas. It compiles the runtime sources directly to include the registry lookup case. The registration case uses the default session's 4,096 buckets; explicit sessions use 256. The harness does not measure export I/O, label updates, cold type initialization, unusually large subject sets or cross-session application workloads.

Raw CSV from this local run is in ignored `artifacts/benchmark-{before,after}-{1,2,3}.csv`. The comparison source copy under `artifacts/benchmark-baseline` was reconstructed from the baseline commit and is not shipped.

## 2026-10-30: bounded session trackers

The baseline is `1d18d06` (the initial 0.2.0 session implementation). The revised implementation owns 16 trackers per handle type per generation, selected by thread-token hash, instead of retaining two new trackers for every context. Both versions use the new `--scopes` and `--retention` modes in the same harness. Windows x64, .NET SDK 9.0.320, Release, `DOTNET_TieredCompilation=0`; no tests ran during measurement.

Scope open/close warms up 2,000 times per worker, then measures 100,000 operations per worker. Before/after processes alternate for three rounds; the table reports medians. Parallel values remain aggregate ns/op, not per-thread latency. The variation between rounds means these results indicate comparable throughput, not an established speedup.

| Scope open/close | Per-context trackers | Shared trackers |
|---|---:|---:|
| 1 worker, ns/op | 391.40 | 388.15 |
| 24 workers, ns/op | 781.63 | 742.26 |
| Allocation, bytes/op | 352 | 352 |

The retention test warms up first, resets once, then creates and discards 100,000 objects per batch in the **default session**, each with one Write and a 100-entry buffer. It measures GC alone, then dead-subject cleanup followed by GC, without resetting between batches. The latter separates tracker ownership from dead registry entries awaiting cleanup. These are retained managed-heap deltas from the warmed baseline, not total allocated bytes or process working set.

| Cumulative objects | Old tracker count | New tracker count | Old retained bytes after cleanup | New retained bytes after cleanup |
|---|---:|---:|---:|---:|
| 100,000 | 200,000 | 32 | 73,604,472 | 1,400 |
| 200,000 | 400,000 | 32 | 147,204,472 | 1,400 |
| 300,000 | 600,000 | 32 | 220,804,472 | 1,400 |

With GC alone, the new implementation retained 11,839,064 / 11,747,048 / 11,876,648 bytes at those checkpoints. Dead registry nodes still hold their Blackboxes until cleanup; these measurements must not be described as immediate zero retention after GC. Tracker segments can also grow with outstanding scopes/exerts and are reused afterward; the fixed count bounds tracker objects, not every possible workload's total memory.

```powershell
$env:DOTNET_TieredCompilation = '0'
dotnet run --project benchmarks/Blackbox.Benchmarks -c Release -- --scopes
dotnet run --project benchmarks/Blackbox.Benchmarks -c Release -- --retention
```

Use `--no-build` after building to repeat measurements. Raw data is in ignored `artifacts/scalability-scopes-{before,after}-{1,2,3}.csv` and `artifacts/scalability-retention-{before,after}.csv`. The baseline source copy is `artifacts/scalability-baseline`.
