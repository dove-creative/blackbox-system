using System;
using System.Diagnostics;
using System.Threading;
using BlackThunder.BlackboxSystem;

if (args.Length != 0)
{
    switch (args[0])
    {
        case "--scopes": ScalabilityBenchmarks.RunScopes(); break;
        case "--retention": ScalabilityBenchmarks.RunRetention(maxLogCount: args.Length > 1 ? int.Parse(args[1]) : 100); break;
        case "--retention-unmaintained": ScalabilityBenchmarks.RunRetention(cleanupEachBatch: false, maxLogCount: args.Length > 1 ? int.Parse(args[1]) : 100); break;
        case "--lookup": LookupBenchmark.Run(); break;
        case "--registration": RegistrationBenchmark.Run(); break;
        default: throw new ArgumentException("Expected --scopes, --retention [maxLogCount], --retention-unmaintained [maxLogCount], --lookup or --registration, or no argument for the original benchmark.");
    }
    return;
}

Console.WriteLine("operation,threads,operations,elapsed_ms,ns_per_operation,allocated_bytes");
foreach (var workers in new[] { 1, Environment.ProcessorCount })
foreach (var operation in new[] { "register", "lookup", "write", "scope", "exert" })
{
    BlackboxHandle.RestartDefaultSession();
    BlackboxHandle.MaxLogCount = 100;
    var owner = new object();
    var target = new object();
    var handle = BlackboxHandle.Of(owner);
    var count = operation == "register" ? 2000 : 20000;
    using var ready = new CountdownEvent(workers);
    using var start = new ManualResetEventSlim();
    var threads = new Thread[workers];
    for (int t = 0; t < workers; t++)
        (threads[t] = new Thread(() =>
        {
            for (int i = 0; i < 500; i++) Run();
            ready.Signal();
            start.Wait();
            for (int i = 0; i < count; i++) Run();
            void Run()
            {
                switch (operation)
                {
                    case "register": BlackboxSessionRegistry.GetBlackbox(new object()); break;
                    case "lookup": BlackboxSessionRegistry.GetBlackbox(owner); break;
                    case "write": handle.Write("message"); break;
                    case "scope": handle.Scope("scope").Dispose(); break;
                    case "exert": handle.Exert(target, "exert").Dispose(); break;
                }
            }
        })).Start();
    ready.Wait();
    var allocated = GC.GetTotalAllocatedBytes(true);
    var watch = Stopwatch.StartNew();
    start.Set();
    foreach (var thread in threads) thread.Join();
    watch.Stop();
    allocated = GC.GetTotalAllocatedBytes(true) - allocated;
    var total = (long)count * workers;
    Console.WriteLine($"{operation},{workers},{total},{watch.Elapsed.TotalMilliseconds:F3},{watch.Elapsed.TotalNanoseconds / total:F2},{allocated}");
    GC.KeepAlive(owner);
    GC.KeepAlive(target);
}
