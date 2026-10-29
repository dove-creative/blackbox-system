using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using BlackThunder.BlackboxSystem;

internal static class ScalabilityBenchmarks
{
    internal static void RunScopes()
    {
        Configure();
        Console.WriteLine("operation,threads,operations,elapsed_ms,ns_per_operation,allocated_bytes");
        var workerCounts = Environment.ProcessorCount == 1 ? new[] { 1 } : new[] { 1, Environment.ProcessorCount };
        foreach (var workers in workerCounts)
        {
            BlackboxHandle.ForceReset();
            var owner = new object();
            var handle = BlackboxHandle.Of(owner);
            const int count = 100_000;
            using var ready = new CountdownEvent(workers);
            using var start = new ManualResetEventSlim();
            var threads = new Thread[workers];
            for (int t = 0; t < workers; t++)
                (threads[t] = new Thread(() =>
                {
                    for (int i = 0; i < 2_000; i++) handle.Scope("scope").Dispose();
                    ready.Signal();
                    start.Wait();
                    for (int i = 0; i < count; i++) handle.Scope("scope").Dispose();
                })).Start();
            ready.Wait();
            var allocated = GC.GetTotalAllocatedBytes(true);
            var watch = Stopwatch.StartNew();
            start.Set();
            foreach (var thread in threads) thread.Join();
            watch.Stop();
            allocated = GC.GetTotalAllocatedBytes(true) - allocated;
            var total = (long)workers * count;
            Console.WriteLine(FormattableString.Invariant($"scope,{workers},{total},{watch.Elapsed.TotalMilliseconds:F3},{watch.Elapsed.TotalNanoseconds / total:F2},{allocated}"));
            GC.KeepAlive(owner);
        }
    }

    internal static void RunRetention(bool cleanupEachBatch = true, int maxLogCount = 100)
    {
        Configure(maxLogCount);
        // Warm runtime metadata and reflection separately from the retained-heap baseline.
        Churn(1_000);
        CountTrackers();
        BlackboxHandle.ForceReset();
        var baseline = HeapAfterCollection();
        Console.WriteLine("phase,objects_created,trackers,live_subjects,heap_delta_bytes");
        PrintRetention("baseline", 0, baseline, baseline);
        var batches = cleanupEachBatch ? 3 : 6;
        for (int batch = 1; batch <= batches; batch++)
        {
            Churn(100_000);
            var rawHeap = HeapAfterCollection();
            PrintRetention("after_gc", batch * 100_000, rawHeap, baseline);
            if (cleanupEachBatch)
            {
                // Registry dead-node cleanup is distinct from tracker retirement: no ForceReset here.
                BlackboxSession.CurrentState.Subjects.Cleanup();
                var cleanedHeap = HeapAfterCollection();
                PrintRetention("after_subject_cleanup", batch * 100_000, cleanedHeap, baseline);
            }
        }
        if (!cleanupEachBatch)
        {
            // With no new registrations or export, another GC cannot unlink registry entries.
            PrintRetention("after_idle_gc", batches * 100_000, HeapAfterCollection(), baseline);
            // Model a long-lived default session with neither exports nor explicit maintenance.
            // Observe all six batches first; only then separate registry retention from tracker retention.
            BlackboxSession.CurrentState.Subjects.Cleanup();
            PrintRetention("after_subject_cleanup", batches * 100_000, HeapAfterCollection(), baseline);
        }
        BlackboxHandle.ForceReset();
        PrintRetention("after_reset", batches * 100_000, HeapAfterCollection(), baseline);
    }

    private static void Configure(int maxLogCount = 100)
    {
        BlackboxHandle.Configure(null, (Action<string>)null, openLogOption: OpenLogOption.Never,
            useBlackbox: UseBlackboxOption.Use);
        BlackboxHandle.MaxLogCount = maxLogCount;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Churn(int count)
    {
        for (int i = 0; i < count; i++) BlackboxHandle.Of(new object()).Write("spawned");
    }

    private static long HeapAfterCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return GC.GetTotalMemory(true);
    }

    private static void PrintRetention(string phase, int count, long heap, long baseline) =>
        Console.WriteLine(FormattableString.Invariant($"{phase},{count},{CountTrackers()},{BlackboxRegistry.Count()},{heap - baseline}"));

    // Read both the 0.2.0 per-context tracker list and the fixed shared tracker arrays.
    // Reflection is used only outside timed/heap measurements and also works in the baseline copy.
    private static int CountTrackers()
    {
        var state = BlackboxSession.CurrentState;
        var fields = typeof(SessionState).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
        var count = 0;
        foreach (var field in fields)
        {
            if (field.Name == "_trackers")
            {
                var node = field.GetValue(state);
                if (node == null) continue;
                var next = node.GetType().GetField("Next", BindingFlags.Instance | BindingFlags.NonPublic);
                for (; node != null; node = next.GetValue(node)) count++;
            }
            else if (field.FieldType.IsArray && field.FieldType.GetElementType().IsGenericType &&
                field.FieldType.GetElementType().GetGenericTypeDefinition() == typeof(HandleTracker<>))
                count += ((Array)field.GetValue(state)).Length;
        }
        return count;
    }
}
