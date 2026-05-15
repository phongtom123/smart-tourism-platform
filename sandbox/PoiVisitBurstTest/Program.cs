// Sandbox: simulate a burst of POI visit POSTs hitting the same gian hang.
// Compares the CURRENT path (no queue, per-request DB transaction with row lock
// contention on gianhang.luotTruyCap) against a PROPOSED queue+batch path that
// mirrors DeviceTouchQueue (dedup window + bounded channel + batch flush).
//
// Goal: quantify the difference in DB ops & wall time so we can decide whether
// a queue is worth adding to the POI visit hot path.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

const int VisitorCount = 200;
const int BoothId = 1;

// Mocked DB latencies. Realistic ranges for a hot-row UPDATE behind a network hop.
TimeSpan SelectLatency = TimeSpan.FromMilliseconds(2);
TimeSpan InsertIgnoreLatency = TimeSpan.FromMilliseconds(3);
TimeSpan UpdateLatencyHotRow = TimeSpan.FromMilliseconds(8);   // row-locked, serializes
TimeSpan UpsertLatency = TimeSpan.FromMilliseconds(4);

// === Path A: current production behavior ===
// Each request opens a transaction, does SELECT + INSERT IGNORE + UPDATE + UPSERT.
// The UPDATE on gianhang.luotTruyCap serializes across all concurrent requests
// because they target the same row.

async Task<(int dbOps, long ms, int finalCount)> RunCurrentPathAsync()
{
    var dbOps = 0;
    var hotRowLock = new SemaphoreSlim(1, 1); // simulates MySQL row lock on gianhang row
    var seenDevices = new ConcurrentDictionary<string, byte>(); // simulates luot_truy_cap_thiet_bi_ngay PK
    var visitCount = 0;
    var sw = Stopwatch.StartNew();

    var tasks = Enumerable.Range(0, VisitorCount).Select(async i =>
    {
        var deviceId = $"device-{i}";

        await Task.Delay(SelectLatency);
        Interlocked.Increment(ref dbOps);

        // INSERT IGNORE — dedup at DB layer
        await Task.Delay(InsertIgnoreLatency);
        Interlocked.Increment(ref dbOps);
        var newRow = seenDevices.TryAdd($"{BoothId}|{deviceId}", 1);

        if (newRow)
        {
            // UPDATE gianhang — HOT ROW, serialized
            await hotRowLock.WaitAsync();
            try
            {
                await Task.Delay(UpdateLatencyHotRow);
                Interlocked.Increment(ref dbOps);
                Interlocked.Increment(ref visitCount);
            }
            finally
            {
                hotRowLock.Release();
            }

            // UPSERT luot_truy_cap_ngay
            await Task.Delay(UpsertLatency);
            Interlocked.Increment(ref dbOps);
        }
    }).ToArray();

    await Task.WhenAll(tasks);
    sw.Stop();
    return (dbOps, sw.ElapsedMilliseconds, visitCount);
}

// === Path B: proposed queue+batch (mirrors DeviceTouchQueue semantics) ===
// Each request enqueues (deviceId, boothId) into a bounded channel (after a 5s
// per-device-per-booth dedup), then a worker batch-flushes every 5s OR when 50
// items accumulate. Flush coalesces all visits for the same booth into a single
// UPDATE with += N.

async Task<(int dbOps, long ms, int finalCount, int rejected)> RunQueuedPathAsync()
{
    var dbOps = 0;
    var visitCount = 0;
    var rejected = 0;

    var dedupe = new ConcurrentDictionary<string, long>();
    var dedupeCooldown = TimeSpan.FromSeconds(5);
    var channel = Channel.CreateBounded<(int boothId, string deviceId)>(
        new BoundedChannelOptions(2000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

    var batchFlushInterval = TimeSpan.FromMilliseconds(50); // shortened for sandbox
    const int maxBatchSize = 50;

    using var workerCts = new CancellationTokenSource();

    var seenDevicesPerBooth = new ConcurrentDictionary<string, byte>();

    var workerTask = Task.Run(async () =>
    {
        var batch = new HashSet<string>();
        var perBoothCount = new Dictionary<int, int>();

        while (!workerCts.IsCancellationRequested)
        {
            using var flushCts = CancellationTokenSource.CreateLinkedTokenSource(workerCts.Token);
            flushCts.CancelAfter(batchFlushInterval);

            try
            {
                while (batch.Count < maxBatchSize)
                {
                    var item = await channel.Reader.ReadAsync(flushCts.Token);
                    var key = $"{item.boothId}|{item.deviceId}";
                    if (seenDevicesPerBooth.TryAdd(key, 1))
                    {
                        if (batch.Add(key))
                        {
                            if (!perBoothCount.ContainsKey(item.boothId))
                                perBoothCount[item.boothId] = 0;
                            perBoothCount[item.boothId]++;
                        }
                    }
                }
            }
            catch (OperationCanceledException) { /* flush window elapsed */ }

            if (batch.Count == 0) continue;

            // One INSERT IGNORE batch for dedupe table (1 op, multi-row VALUES)
            await Task.Delay(InsertIgnoreLatency);
            Interlocked.Increment(ref dbOps);

            // One UPDATE per affected booth (here: just 1 booth)
            foreach (var (boothId, count) in perBoothCount)
            {
                await Task.Delay(UpdateLatencyHotRow);
                Interlocked.Increment(ref dbOps);
                Interlocked.Add(ref visitCount, count);

                // One UPSERT per booth
                await Task.Delay(UpsertLatency);
                Interlocked.Increment(ref dbOps);
            }

            batch.Clear();
            perBoothCount.Clear();
        }
    });

    var sw = Stopwatch.StartNew();

    var tasks = Enumerable.Range(0, VisitorCount).Select(async i =>
    {
        var deviceId = $"device-{i}";
        var key = $"{BoothId}|{deviceId}";
        var nowTicks = DateTime.UtcNow.Ticks;
        var lastTicks = dedupe.GetOrAdd(key, 0L);

        if (nowTicks - lastTicks < dedupeCooldown.Ticks)
        {
            Interlocked.Increment(ref rejected);
            return;
        }

        dedupe[key] = nowTicks;
        await Task.Yield();
        if (!channel.Writer.TryWrite((BoothId, deviceId)))
            Interlocked.Increment(ref rejected);
    }).ToArray();

    await Task.WhenAll(tasks);

    // Drain: wait until channel empty + last flush window passes
    while (channel.Reader.Count > 0)
        await Task.Delay(10);
    await Task.Delay(batchFlushInterval + TimeSpan.FromMilliseconds(50));

    workerCts.Cancel();
    try { await workerTask; } catch (OperationCanceledException) { }

    sw.Stop();
    return (dbOps, sw.ElapsedMilliseconds, visitCount, rejected);
}

// === Run both ===

Console.WriteLine($"Burst: {VisitorCount} visitors hitting booth #{BoothId} simultaneously");
Console.WriteLine(new string('-', 70));

var current = await RunCurrentPathAsync();
Console.WriteLine($"[CURRENT  - no queue]");
Console.WriteLine($"  DB ops      : {current.dbOps}    (4 ops per visitor: SELECT + INSERT IGNORE + UPDATE + UPSERT)");
Console.WriteLine($"  wall time   : {current.ms} ms");
Console.WriteLine($"  final count : {current.finalCount}");
Console.WriteLine($"  bottleneck  : {VisitorCount} sequential UPDATEs serialized on gianhang row lock");
Console.WriteLine();

var queued = await RunQueuedPathAsync();
Console.WriteLine($"[PROPOSED - queue + batch flush]");
Console.WriteLine($"  DB ops      : {queued.dbOps}    (1 batched INSERT IGNORE + 1 UPDATE + 1 UPSERT per flush)");
Console.WriteLine($"  wall time   : {queued.ms} ms");
Console.WriteLine($"  final count : {queued.finalCount}");
Console.WriteLine($"  rejected    : {queued.rejected} (dedup cooldown / channel full)");
Console.WriteLine();

Console.WriteLine(new string('-', 70));
Console.WriteLine($"Speedup     : {(double)current.ms / queued.ms:F1}x faster wall time");
Console.WriteLine($"DB op reduction: {current.dbOps} -> {queued.dbOps} ({(1.0 - (double)queued.dbOps / current.dbOps) * 100:F0}% fewer)");
Console.WriteLine($"Counts match: {(current.finalCount == queued.finalCount ? "YES" : "NO")} ({current.finalCount} vs {queued.finalCount})");

return 0;
