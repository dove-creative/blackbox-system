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

Build before timing, then repeat with `--no-build`. The harness prints CSV containing elapsed time, aggregate cost and process allocation deltas. It compiles the runtime sources directly to include the registry lookup case. At the time of the October 29 measurements, the registration case used the default session's 4,096 buckets; explicit sessions used 256. The original harness does not measure export I/O, label updates, cold type initialization, unusually large subject sets or cross-session application workloads. The October 30 additions below cover large subject sets separately.

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

## 2026-10-30: subject lookup at larger live populations

The default table now has 65,536 buckets; explicit sessions keep 256. A hit follows immutable nodes and dereferences an owner only after its identity hash matches. Dead-owner scanning and cleanup move to the insertion/miss path. The baseline remains `1d18d06`, with 4,096 default buckets and an owner read for every node inspected.

`--lookup` retains **all** subjects in an array through `GC.KeepAlive`. For each population, it prepares 4,096 indices using `Random(1)`, warms 100,000 lookups, then repeats those indices for 2,000,000 registry hits. Setup, insertion and index generation are outside timing. This measures a repeated hot subset of the live population; it is not a uniformly fresh random lookup on every operation. It calls the internal registry used by Of/interaction/tag lookup, so public API overhead is excluded.

The same Windows/.NET/Release/TieredCompilation settings apply. Before/after processes alternate for three rounds, without concurrent test runs. Medians:

| Live subjects | Before, ns/lookup | After, ns/lookup |
|---|---:|---:|
| 1,000 | 12.43 | 11.21 |
| 10,000 | 36.14 | 15.54 |
| 100,000 | 771.64 | 51.42 |
| 400,000 | 6378.17 | 111.18 |

All measured lookup loops allocated zero bytes. The 400,000-subject case is about 57 times faster in this workload. This remains a fixed-size table; still larger live populations will increase chain lengths.

The bucket array itself uses approximately 512 KiB of references per default generation on x64 (plus its array header). There is a separate retention cost: with the batch cleanup protocol above and 65,536 buckets, GC-only heap deltas were 71,808,872 / 71,382,488 / 71,635,208 bytes after 100k/200k/300k objects. Subject cleanup reduced each to 1,400 bytes, with 32 trackers throughout. More buckets can retain more dead entries pending cleanup; the 512 KiB array is not the entire memory tradeoff.

```powershell
$env:DOTNET_TieredCompilation = '0'
dotnet run --project benchmarks/Blackbox.Benchmarks -c Release -- --lookup
dotnet run --project benchmarks/Blackbox.Benchmarks -c Release -- --retention-unmaintained
```

The latter mode creates six batches of 100,000 objects, runs GC after each batch and **does not clean the subject store or reset between batches**. It performs one subject cleanup at the end, then resets. This distinguishes long-running default-session retention from the earlier per-batch cleanup experiment.

| Objects created without intermediate cleanup | Before: GC-only retained MiB | After: GC-only retained MiB | After: trackers |
|---|---:|---:|---:|
| 100,000 | 79.00 | 68.49 | 32 |
| 200,000 | 149.16 | 82.87 | 32 |
| 300,000 | 219.37 | 86.10 | 32 |
| 400,000 | 289.44 | 86.77 | 32 |
| 500,000 | 359.77 | 86.88 | 32 |
| 600,000 | 429.91 | 86.89 | 32 |

The original tracker count grew from 200,000 to 1,200,000. After the single final subject cleanup, retained growth was 441,604,472 bytes before and 1,400 bytes after. The remaining pre-cleanup memory in the new version approaches **87 MiB in this workload**, not 68 MiB as in the experiment that cleaned after every batch. This is retained dead registry data, not ongoing tracker accumulation. These results describe this workload and buffer size, not a universal heap limit.

Raw lookup data: ignored `artifacts/scalability-lookup-{before,after}-{1,2,3}.csv`. Final per-batch cleanup data: `artifacts/scalability-retention-final.csv`.
Data without intermediate cleanup: `artifacts/scalability-retention-unmaintained-{before,after}.csv`.

## 2026-10-30: rotating dead-entry maintenance

The baseline for this comparison is `c3828a9`: shared trackers, 65,536 default buckets and hash-first hits, but cleanup only in the inserted bucket or during explicit maintenance. The revised store reserves a range of 16 buckets with Interlocked after each successful new registration. Each bucket uses one cleanup CAS attempt; a losing CAS leaves the newer chain intact. Existing hits and insertion CAS retries do not advance the cursor. Explicit sessions use the same policy; tables smaller than 16 buckets visit each bucket once per insertion.

Both executables use the same updated harness, Windows x64, .NET SDK 9.0.320, Release and `DOTNET_TieredCompilation=0`. Measurements run in separate processes, with no concurrent builds or tests. The baseline runtime is preserved under ignored `artifacts/subject-sweep-baseline`.

### Retained log buffers

`--retention-unmaintained [maxLogCount]` repeats the six batches of 100,000 discarded objects, each with one Write. It forces GC after each batch without export, full cleanup or reset between batches. A second final GC with no registrations models stopping creation; a full Subject cleanup and GC then isolates the remaining registry retention. Values below are managed-heap deltas after 600,000 objects, not process working set or a memory ceiling. Each retention configuration was measured once.

| MaxLogCount | Baseline after GC, MiB | Rotating sweep after GC, MiB | After another idle GC, MiB (baseline / sweep) |
|---|---:|---:|---:|
| 100 | 86.82 | 10.41 | 86.82 / 10.41 |
| 1000 | 537.87 | 23.09 | 537.87 / 23.09 |

All four runs retained 32 trackers and zero live Subjects. Full Subject cleanup followed by GC reduced each heap delta to 1,400 bytes. The lower observed retention is not an immediate-GC or fixed-memory guarantee: newly dead entries remain until later registrations or explicit maintenance. A completed lap takes 4,096 new registrations in the default table, but CAS conflicts, GC timing, other references and per-context buffer sizes affect actual collection. The number of buckets per registration is bounded; chain length, cleanup time and temporary allocations are not.

### Lookup and registration costs

Before/after processes alternate for three rounds; tables report medians. `--lookup` uses the same 4,096-index hot subset and 2,000,000 hits described above. These fresh paired results should be compared with each other, not directly with older runs taken at different times.

| Live subjects | Baseline hit, ns | Sweep hit, ns |
|---|---:|---:|
| 1,000 | 6.90 | 6.61 |
| 10,000 | 9.09 | 8.48 |
| 100,000 | 31.78 | 31.60 |
| 400,000 | 66.86 | 65.88 |

Every lookup loop allocated zero bytes. This supports comparable lookup throughput, not a claimed lookup speedup.

`--registration` keeps every existing and newly registered owner alive. It preallocates owners, initializes worker threads and forces GC before timing. It then registers 32,768 subjects with one worker, or 32,760 subjects split across 24 workers. Initial population is shown below; it grows during the measurement. Timed work includes canonical box creation, identity hashing, registry insertion and the new sweep, but no Write/log buffer allocation. Start signalling and worker joins are included. Parallel values are aggregate ns/registration, not individual latency.

| Initial live subjects | Baseline, 1 worker ns | Sweep, 1 worker ns | Baseline, 24 workers ns | Sweep, 24 workers ns |
|---|---:|---:|---:|---:|
| 1,000 | 240.86 | 394.32 | 342.38 | 784.00 |
| 10,000 | 149.50 | 313.54 | 586.16 | 333.57 |
| 100,000 | 331.20 | 1146.49 | 848.18 | 313.68 |
| 400,000 | 1033.10 | 6287.68 | 761.74 | 2351.81 |

Single-worker registration is measurably more expensive, especially with dense live chains: at 400,000 initial subjects it rises from about 1.03 to 6.29 microseconds. This is the cost of inspecting owners in 16 additional buckets. Allocation in this all-live workload remains approximately 384 bytes/registration on both sides; dead-entry cleanup may additionally allocate copied prefixes. The short parallel runs are noisy: at 400,000 subjects baseline ranged from 294.69 to 1054.95 ns and sweep from 779.22 to 4908.23 ns. They do not establish a parallel speedup at any population or a stable slowdown ratio.

```powershell
$env:DOTNET_TieredCompilation = '0'
dotnet build benchmarks/Blackbox.Benchmarks -c Release
dotnet run --project benchmarks/Blackbox.Benchmarks -c Release --no-build -- --retention-unmaintained 100
dotnet run --project benchmarks/Blackbox.Benchmarks -c Release --no-build -- --retention-unmaintained 1000
dotnet run --project benchmarks/Blackbox.Benchmarks -c Release --no-build -- --lookup
dotnet run --project benchmarks/Blackbox.Benchmarks -c Release --no-build -- --registration
```

Raw CSV: ignored `artifacts/subject-sweep-retention-{before,after}-{100,1000}.csv`, `artifacts/subject-sweep-lookup-{before,after}-{1,2,3}.csv` and `artifacts/subject-sweep-registration-{before,after}-{1,2,3}.csv`.
