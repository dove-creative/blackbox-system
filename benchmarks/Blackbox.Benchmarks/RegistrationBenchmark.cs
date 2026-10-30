using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using BlackThunder.BlackboxSystem;

internal static class RegistrationBenchmark
{
    internal static void Run()
    {
        BlackboxHandle.Configure(null, (Action<string>)null, openLogOption: OpenLogOption.Never,
            useBlackbox: UseBlackboxOption.Use);
        for (int i = 0; i < 2000; i++) BlackboxSessionRegistry.GetBlackbox(new object());
        Console.WriteLine("operation,initial_live_subjects,threads,registrations,elapsed_ms,ns_per_registration,allocated_bytes");
        var workerCounts = Environment.ProcessorCount == 1 ? new[] { 1 } : new[] { 1, Environment.ProcessorCount };
        foreach (var count in new[] { 1_000, 10_000, 100_000, 400_000 })
            foreach (var workers in workerCounts) Measure(count, workers);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Measure(int count, int workers)
    {
        BlackboxHandle.RestartDefaultSession();
        var owners = new object[count];
        for (int i = 0; i < owners.Length; i++) BlackboxSessionRegistry.GetBlackbox(owners[i] = new object());
        int perWorker = 32768 / workers;
        var fresh = new object[perWorker * workers];
        for (int i = 0; i < fresh.Length; i++) fresh[i] = new object();
        using var ready = new CountdownEvent(workers);
        using var start = new ManualResetEventSlim();
        var threads = new Thread[workers];
        for (int t = 0; t < workers; t++)
        {
            int offset = t * perWorker;
            (threads[t] = new Thread(() =>
            {
                // Exercise the ambient lookup on each worker without registering extra subjects.
                BlackboxSessionRegistry.GetBlackbox(owners[0]);
                ready.Signal();
                start.Wait();
                for (int i = 0; i < perWorker; i++) BlackboxSessionRegistry.GetBlackbox(fresh[offset + i]);
            })).Start();
        }
        ready.Wait();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var watch = new Stopwatch();
        var allocated = GC.GetTotalAllocatedBytes(true);
        watch.Start();
        start.Set();
        foreach (var thread in threads) thread.Join();
        watch.Stop();
        allocated = GC.GetTotalAllocatedBytes(true) - allocated;
        Console.WriteLine(FormattableString.Invariant($"register,{count},{workers},{fresh.Length},{watch.Elapsed.TotalMilliseconds:F3},{watch.Elapsed.TotalNanoseconds / fresh.Length:F2},{allocated}"));
        GC.KeepAlive(owners);
        GC.KeepAlive(fresh);
    }
}
