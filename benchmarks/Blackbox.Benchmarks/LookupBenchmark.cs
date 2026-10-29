using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using BlackThunder.BlackboxSystem;

internal static class LookupBenchmark
{
    internal static void Run()
    {
        BlackboxHandle.Configure(null, (Action<string>)null, openLogOption: OpenLogOption.Never,
            useBlackbox: UseBlackboxOption.Use);
        Console.WriteLine("operation,live_subjects,lookups,elapsed_ms,ns_per_lookup,allocated_bytes");
        foreach (var count in new[] { 1_000, 10_000, 100_000, 400_000 }) Measure(count);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Measure(int count)
    {
        BlackboxHandle.ForceReset();
        var owners = new object[count];
        for (int i = 0; i < count; i++)
        {
            owners[i] = new object();
            BlackboxRegistry.GetBlackbox(owners[i]);
        }
        var indices = new int[4096];
        var random = new Random(1);
        for (int i = 0; i < indices.Length; i++) indices[i] = random.Next(count);
        Blackbox last = null;
        for (int i = 0; i < 100_000; i++) last = BlackboxRegistry.GetBlackbox(owners[indices[i & 4095]]);

        const int iterations = 2_000_000;
        var watch = new Stopwatch();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        watch.Start();
        for (int i = 0; i < iterations; i++) last = BlackboxRegistry.GetBlackbox(owners[indices[i & 4095]]);
        watch.Stop();
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Console.WriteLine(FormattableString.Invariant($"lookup_hit,{count},{iterations},{watch.Elapsed.TotalMilliseconds:F3},{watch.Elapsed.TotalNanoseconds / iterations:F2},{allocated}"));
        GC.KeepAlive(last);
        GC.KeepAlive(owners);
    }
}
