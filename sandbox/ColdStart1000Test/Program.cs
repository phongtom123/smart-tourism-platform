// Sandbox: simulate 1000 visitors opening the app for the FIRST TIME and entering
// an audio zone. Measure server-side load broken down by hot path.
//
// Per-visitor sequence (cold start):
//   1) GET /api/appdata?lang=vi             — fetch POI list + audio URLs
//   2) Prefetch N audio MP3 (parallel, in background — limited per HttpClient)
//   3) Walk into zone (random 30-90s after open)
//   4) Geofence engine picks priority winner (local — no server)
//   5) POST /api/poi/{id}/visit             — enqueued, batched server-side
//
// Server is modeled with:
//   - A shared "DB worker" semaphore (concurrent active queries)
//   - A static-file server pool (concurrent active downloads)
//   - The PoiVisitQueue (5s flush window or 50 items)

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

// Mode: "eager" = current behavior, "lazy" = PrefetchNearbyAsync (top 3 within 200m).
var mode = args.Length > 0 ? args[0] : "eager";

const int VisitorCount = 1000;
const int BoothCount = 20;
const int LazyTopN = 3;            // visitor only sees ~3 booths nearby per walk path
const int LazyDownloadSlot = 2;   // PrefetchSlot semaphore size in AudioCacheService
const int AudioFileSizeBytes = 150_000;       // ~150KB per MP3
const int AppDataResponseSizeBytes = 50_000;  // ~50KB JSON (POIs + audio URLs)
const int MaxConcurrentDbQueries = 8;          // Server DB pool
const int MaxConcurrentStaticDownloads = 50;  // Kestrel/IIS static pool
const int ClientAudioPrefetchParallelism = 4; // HttpClient default per-host limit

// Latency mocks (representative for typical 4G + small VPS)
TimeSpan AppDataDbQueryLatency = TimeSpan.FromMilliseconds(30);
TimeSpan AppDataSerializeLatency = TimeSpan.FromMilliseconds(5);
TimeSpan StaticFileServeLatency = TimeSpan.FromMilliseconds(10);
TimeSpan VisitEnqueueLatency = TimeSpan.FromMilliseconds(1);
TimeSpan AppDataNetworkRtt = TimeSpan.FromMilliseconds(80);    // 4G round-trip
TimeSpan AudioDownloadNetworkRtt = TimeSpan.FromMilliseconds(80);
double AudioDownloadBytesPerSec = 1_000_000;                    // 1 MB/s effective per client

// Visitor arrival window: all 1000 open app within 30s
TimeSpan ArrivalWindow = TimeSpan.FromSeconds(30);
TimeSpan WalkToZoneMin = TimeSpan.FromSeconds(15);
TimeSpan WalkToZoneMax = TimeSpan.FromSeconds(60);

// === Server simulation ===

var dbPool = new SemaphoreSlim(MaxConcurrentDbQueries, MaxConcurrentDbQueries);
var staticPool = new SemaphoreSlim(MaxConcurrentStaticDownloads, MaxConcurrentStaticDownloads);

var appDataCallCount = 0;
var audioDownloadCount = 0;
var visitEnqueueCount = 0;
var visitFlushBatches = 0;
var visitFlushDbOps = 0;
var totalAudioBytesServed = 0L;
var totalAppDataBytesServed = 0L;

var appDataLatencies = new ConcurrentBag<long>();
var audioLatencies = new ConcurrentBag<long>();
var visitEnqueueLatencies = new ConcurrentBag<long>();

int peakDbConcurrent = 0;
int peakStaticConcurrent = 0;
int currentDbConcurrent = 0;
int currentStaticConcurrent = 0;
var peakLock = new object();

void TrackDb(int delta)
{
    lock (peakLock)
    {
        currentDbConcurrent += delta;
        if (currentDbConcurrent > peakDbConcurrent) peakDbConcurrent = currentDbConcurrent;
    }
}
void TrackStatic(int delta)
{
    lock (peakLock)
    {
        currentStaticConcurrent += delta;
        if (currentStaticConcurrent > peakStaticConcurrent) peakStaticConcurrent = currentStaticConcurrent;
    }
}

async Task<string> ServerHandleAppDataAsync()
{
    var sw = Stopwatch.StartNew();
    Interlocked.Increment(ref appDataCallCount);
    Interlocked.Add(ref totalAppDataBytesServed, AppDataResponseSizeBytes);

    await dbPool.WaitAsync();
    TrackDb(+1);
    try
    {
        await Task.Delay(AppDataDbQueryLatency);
        await Task.Delay(AppDataSerializeLatency);
    }
    finally
    {
        TrackDb(-1);
        dbPool.Release();
    }

    sw.Stop();
    appDataLatencies.Add(sw.ElapsedMilliseconds);
    return "appdata";
}

async Task ServerHandleAudioDownloadAsync()
{
    var sw = Stopwatch.StartNew();
    Interlocked.Increment(ref audioDownloadCount);
    Interlocked.Add(ref totalAudioBytesServed, AudioFileSizeBytes);

    await staticPool.WaitAsync();
    TrackStatic(+1);
    try
    {
        await Task.Delay(StaticFileServeLatency);
        // Bandwidth-limited transfer time
        var transferMs = (int)(AudioFileSizeBytes / AudioDownloadBytesPerSec * 1000);
        await Task.Delay(transferMs);
    }
    finally
    {
        TrackStatic(-1);
        staticPool.Release();
    }

    sw.Stop();
    audioLatencies.Add(sw.ElapsedMilliseconds);
}

// PoiVisitQueue mimic
var visitChannel = Channel.CreateBounded<(int boothId, string deviceId)>(2000);
var visitDedupe = new ConcurrentDictionary<string, byte>();
using var workerCts = new CancellationTokenSource();

var visitWorker = Task.Run(async () =>
{
    var batch = new HashSet<string>();
    var perBoothCount = new Dictionary<int, int>();
    var flushInterval = TimeSpan.FromMilliseconds(500); // shortened for sandbox
    const int maxBatch = 50;

    while (!workerCts.IsCancellationRequested)
    {
        using var flushCts = CancellationTokenSource.CreateLinkedTokenSource(workerCts.Token);
        flushCts.CancelAfter(flushInterval);

        try
        {
            while (batch.Count < maxBatch)
            {
                var item = await visitChannel.Reader.ReadAsync(flushCts.Token);
                var key = $"{item.boothId}|{item.deviceId}";
                if (visitDedupe.TryAdd(key, 1) && batch.Add(key))
                {
                    perBoothCount[item.boothId] = perBoothCount.GetValueOrDefault(item.boothId) + 1;
                }
            }
        }
        catch (OperationCanceledException) { }

        if (batch.Count == 0) continue;

        Interlocked.Increment(ref visitFlushBatches);

        await dbPool.WaitAsync();
        TrackDb(+1);
        try
        {
            await Task.Delay(3);  // INSERT IGNORE multi-row
            Interlocked.Increment(ref visitFlushDbOps);

            foreach (var (boothId, count) in perBoothCount)
            {
                await Task.Delay(8);  // UPDATE +=count
                Interlocked.Increment(ref visitFlushDbOps);
                await Task.Delay(4);  // UPSERT daily
                Interlocked.Increment(ref visitFlushDbOps);
            }
        }
        finally
        {
            TrackDb(-1);
            dbPool.Release();
        }

        batch.Clear();
        perBoothCount.Clear();
    }
});

// === Visitor simulation ===

async Task SimulateVisitorAsync(int visitorIndex, Random rng)
{
    var deviceId = $"DEV-{visitorIndex:D5}";

    // 1) Cold start: fetch /api/appdata
    var appDataSw = Stopwatch.StartNew();
    await Task.Delay(AppDataNetworkRtt);
    await ServerHandleAppDataAsync();
    appDataSw.Stop();

    // 2) Prefetch audio
    int prefetchCount = mode == "lazy" ? LazyTopN : BoothCount;
    var prefetchSem = new SemaphoreSlim(mode == "lazy" ? LazyDownloadSlot : ClientAudioPrefetchParallelism);
    var audioTasks = Enumerable.Range(0, prefetchCount).Select(async i =>
    {
        await prefetchSem.WaitAsync();
        try
        {
            await Task.Delay(AudioDownloadNetworkRtt);
            await ServerHandleAudioDownloadAsync();
        }
        finally { prefetchSem.Release(); }
    });

    _ = Task.Run(async () => await Task.WhenAll(audioTasks)); // background

    // 3) Walk to zone
    var walkMs = rng.Next(
        (int)WalkToZoneMin.TotalMilliseconds,
        (int)WalkToZoneMax.TotalMilliseconds);
    await Task.Delay(walkMs);

    // 4) Priority pick (local, ~0ms)
    var pickedBooth = rng.Next(1, BoothCount + 1);

    // 5) POST visit (enqueued)
    var visitSw = Stopwatch.StartNew();
    await Task.Delay(VisitEnqueueLatency);
    await visitChannel.Writer.WriteAsync((pickedBooth, deviceId));
    Interlocked.Increment(ref visitEnqueueCount);
    visitSw.Stop();
    visitEnqueueLatencies.Add(visitSw.ElapsedMilliseconds);
}

// === Run ===

Console.WriteLine($"Simulating {VisitorCount} cold-start visitors entering audio zones [MODE: {mode.ToUpper()}]");
Console.WriteLine($"Booths: {BoothCount} | Arrival window: {ArrivalWindow.TotalSeconds}s | Walk-to-zone: {WalkToZoneMin.TotalSeconds}-{WalkToZoneMax.TotalSeconds}s");
Console.WriteLine($"Prefetch: {(mode == "lazy" ? $"top {LazyTopN} nearest (queue slot {LazyDownloadSlot})" : "all 20 eager")}");
Console.WriteLine($"Server caps: DB={MaxConcurrentDbQueries} concurrent | Static={MaxConcurrentStaticDownloads} concurrent");
Console.WriteLine(new string('-', 80));
Console.WriteLine();

var globalSw = Stopwatch.StartNew();

var rng = new Random(42);
var visitorTasks = new Task[VisitorCount];
for (var i = 0; i < VisitorCount; i++)
{
    var idx = i;
    var visitorRng = new Random(42 + idx);
    var arrivalDelay = rng.NextDouble() * ArrivalWindow.TotalMilliseconds;
    visitorTasks[i] = Task.Run(async () =>
    {
        await Task.Delay((int)arrivalDelay);
        await SimulateVisitorAsync(idx, visitorRng);
    });
}

await Task.WhenAll(visitorTasks);

// Wait for queue to drain
await Task.Delay(2000);
workerCts.Cancel();
try { await visitWorker; } catch (OperationCanceledException) { }

globalSw.Stop();

// === Report ===

double Pct(IEnumerable<long> samples, double pct)
{
    var sorted = samples.OrderBy(x => x).ToArray();
    if (sorted.Length == 0) return 0;
    var idx = (int)Math.Min(sorted.Length - 1, sorted.Length * pct);
    return sorted[idx];
}

Console.WriteLine($"=== Results (wall time: {globalSw.Elapsed.TotalSeconds:F1}s) ===");
Console.WriteLine();

Console.WriteLine($"[1] /api/appdata fetch (cold start)");
Console.WriteLine($"    requests           : {appDataCallCount}");
Console.WriteLine($"    bytes served       : {totalAppDataBytesServed / 1024:N0} KB ({totalAppDataBytesServed / 1024.0 / 1024:F1} MB)");
Console.WriteLine($"    latency p50/p95/p99: {Pct(appDataLatencies, 0.50):F0} / {Pct(appDataLatencies, 0.95):F0} / {Pct(appDataLatencies, 0.99):F0} ms");
Console.WriteLine($"    avg latency        : {appDataLatencies.Average():F0} ms");
Console.WriteLine();

Console.WriteLine($"[2] Audio MP3 prefetch (static files)");
Console.WriteLine($"    requests           : {audioDownloadCount} ({audioDownloadCount / VisitorCount} per visitor on average)");
Console.WriteLine($"    bytes served       : {totalAudioBytesServed / 1024:N0} KB ({totalAudioBytesServed / 1024.0 / 1024:F1} MB)");
Console.WriteLine($"    latency p50/p95/p99: {Pct(audioLatencies, 0.50):F0} / {Pct(audioLatencies, 0.95):F0} / {Pct(audioLatencies, 0.99):F0} ms");
Console.WriteLine($"    avg latency        : {audioLatencies.Average():F0} ms");
Console.WriteLine();

Console.WriteLine($"[3] POST /api/poi/{{id}}/visit (queued)");
Console.WriteLine($"    enqueue requests   : {visitEnqueueCount}");
Console.WriteLine($"    enqueue p99 latency: {Pct(visitEnqueueLatencies, 0.99):F0} ms (client-perceived)");
Console.WriteLine($"    background flushes : {visitFlushBatches} batches");
Console.WriteLine($"    DB ops in flushes  : {visitFlushDbOps}");
Console.WriteLine($"    -> ratio: {(double)visitEnqueueCount / Math.Max(1, visitFlushDbOps):F1}x compression");
Console.WriteLine();

Console.WriteLine($"[Server pressure]");
Console.WriteLine($"    peak DB concurrency      : {peakDbConcurrent}/{MaxConcurrentDbQueries}");
Console.WriteLine($"    peak static concurrency  : {peakStaticConcurrent}/{MaxConcurrentStaticDownloads}");
Console.WriteLine($"    total bytes served       : {(totalAudioBytesServed + totalAppDataBytesServed) / 1024.0 / 1024:F1} MB");
Console.WriteLine($"    bandwidth (avg over wall): {(totalAudioBytesServed + totalAppDataBytesServed) / 1024.0 / 1024 / globalSw.Elapsed.TotalSeconds:F2} MB/s");
Console.WriteLine();

// === Hot-path ranking ===
Console.WriteLine($"[Hot-path summary]");
var byCount = new[]
{
    ("audio MP3 prefetch", audioDownloadCount, totalAudioBytesServed),
    ("/api/appdata",       appDataCallCount,   totalAppDataBytesServed),
    ("/visit (queued)",    visitEnqueueCount,  0L),
}.OrderByDescending(x => x.Item2).ToArray();

foreach (var (name, cnt, bytes) in byCount)
{
    Console.WriteLine($"    {name,-25}: {cnt,6} requests, {bytes / 1024.0 / 1024:F1} MB");
}

return 0;
