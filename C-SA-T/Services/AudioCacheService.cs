using System.Collections.Concurrent;
using System.Security.Cryptography;
using MauiApp1.Models;
using MauiApp1.Utils;

namespace MauiApp1.Services;

public sealed class AudioCacheService
{
    private static readonly TimeSpan FreshAudioCacheAge = TimeSpan.FromDays(7);
    private static readonly ConcurrentDictionary<string, byte[]> MemoryCache =
        new(StringComparer.OrdinalIgnoreCase);

    // Lazy prefetch queue: cap dong thoi 2 download de tranh nghen mang/server.
    private static readonly SemaphoreSlim PrefetchSlot = new(2, 2);
    private static readonly ConcurrentDictionary<string, byte> PrefetchInFlight =
        new(StringComparer.OrdinalIgnoreCase);

    private static HttpClient? _audioHttpClient;

    private readonly SQLiteService _sqliteService;

    public AudioCacheService(SQLiteService sqliteService)
    {
        _sqliteService = sqliteService;
    }

    // Demo helper: xóa memory cache audio. SQLite cache xóa thông qua AppDataCacheService.ClearAsync().
    public static void ClearMemoryCache()
    {
        MemoryCache.Clear();
        PrefetchInFlight.Clear();
    }

    public async Task<byte[]?> GetAudioBytesAsync(string? audioUrl, CancellationToken cancellationToken = default)
    {
        var normalizedUrl = NormalizeAudioUrl(audioUrl);
        if (string.IsNullOrWhiteSpace(normalizedUrl))
            return null;

        if (MemoryCache.TryGetValue(normalizedUrl, out var memoryBytes) && memoryBytes.Length > 0)
            return memoryBytes;

        var cachedBytes = await TryReadCachedBytesAsync(normalizedUrl, allowStale: true).ConfigureAwait(false);
        if (cachedBytes is not null)
        {
            MemoryCache[normalizedUrl] = cachedBytes;
            return cachedBytes;
        }

        if (!HasInternet())
            return null;

        return await TryDownloadAndCacheAsync(normalizedUrl, cancellationToken).ConfigureAwait(false);
    }

    private const string EagerPrefetchDoneKey = "audio_eager_prefetch_done";

    /// <summary>
    /// Hybrid prefetch: lan dau cai app -> tai het audio (eager) de an toan offline,
    /// lan sau -> skip, de PrefetchNearbyAsync (lazy) tai theo nhu cau.
    /// </summary>
    public async Task PrefetchForAppDataAsync(AppDataResponse? appData, CancellationToken cancellationToken = default)
    {
        if (appData is null || appData.GianHangs.Count == 0 || !HasInternet())
            return;

        // Hybrid: nếu đã hoàn tất eager 1 lần -> skip, để lazy đảm nhiệm.
        var eagerDoneFlag = await _sqliteService.GetCacheAsync(EagerPrefetchDoneKey).ConfigureAwait(false);
        if (eagerDoneFlag is not null && !string.IsNullOrWhiteSpace(eagerDoneFlag.JsonData))
            return;

        var audioUrls = appData.GianHangs
            .Select(x => x.AudioFullUrl)
            .Select(NormalizeAudioUrl)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();

        foreach (var audioUrl in audioUrls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (MemoryCache.ContainsKey(audioUrl))
                    continue;

                var cacheKey = BuildAudioCacheKey(audioUrl);
                var freshEntry = await _sqliteService
                    .GetCacheIfFreshAsync(cacheKey, FreshAudioCacheAge)
                    .ConfigureAwait(false);

                if (freshEntry is not null && !string.IsNullOrWhiteSpace(freshEntry.JsonData))
                {
                    try
                    {
                        MemoryCache[audioUrl] = Convert.FromBase64String(freshEntry.JsonData);
                        continue;
                    }
                    catch (FormatException)
                    {
                        // Fall through and refresh this audio from the network.
                    }
                }

                await TryDownloadAndCacheAsync(audioUrl, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioCache] Prefetch failed for {audioUrl}: {ex.Message}");
            }
        }

        // Đánh dấu đã hoàn tất eager — lần sau chuyển sang lazy.
        await _sqliteService.UpsertCacheAsync(new AppCacheEntry
        {
            CacheKey = EagerPrefetchDoneKey,
            JsonData = DateTime.UtcNow.ToString("O"),
            UpdatedAtUtc = DateTime.UtcNow
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Lazy prefetch: chi tai audio cho topN gian hang gan nhat (trong maxDistanceMeters)
    /// thay vi tai het. Gioi han 2 download dong thoi qua PrefetchSlot — queue ngam.
    /// Goi tu location-update handler khi du khach di chuyen.
    /// </summary>
    public async Task PrefetchNearbyAsync(
        double currentLat,
        double currentLon,
        IEnumerable<GianHang> gianHangs,
        double maxDistanceMeters = 200,
        int topN = 3,
        CancellationToken cancellationToken = default)
    {
        if (!HasInternet()) return;

        var nearby = gianHangs
            .Where(g => g.Lat.HasValue && g.Lon.HasValue && !string.IsNullOrWhiteSpace(g.AudioFullUrl))
            .Select(g => new
            {
                Url = NormalizeAudioUrl(g.AudioFullUrl),
                Distance = HaversineMeters(currentLat, currentLon, g.Lat!.Value, g.Lon!.Value)
            })
            .Where(x => x.Distance <= maxDistanceMeters && !string.IsNullOrWhiteSpace(x.Url))
            .OrderBy(x => x.Distance)
            .Take(topN)
            .Select(x => x.Url)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (nearby.Count == 0) return;

        var tasks = nearby.Select(async url =>
        {
            if (MemoryCache.ContainsKey(url)) return;
            if (!PrefetchInFlight.TryAdd(url, 1)) return;

            try
            {
                var cacheKey = BuildAudioCacheKey(url);
                var fresh = await _sqliteService
                    .GetCacheIfFreshAsync(cacheKey, FreshAudioCacheAge)
                    .ConfigureAwait(false);

                if (fresh is not null && !string.IsNullOrWhiteSpace(fresh.JsonData))
                {
                    try
                    {
                        MemoryCache[url] = Convert.FromBase64String(fresh.JsonData);
                        return;
                    }
                    catch (FormatException) { /* fall through to network */ }
                }

                await PrefetchSlot.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await TryDownloadAndCacheAsync(url, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    PrefetchSlot.Release();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioCache] Lazy prefetch failed for {url}: {ex.Message}");
            }
            finally
            {
                PrefetchInFlight.TryRemove(url, out _);
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6_371_000d;
        var dLat = (lat2 - lat1) * Math.PI / 180d;
        var dLon = (lon2 - lon1) * Math.PI / 180d;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180d) * Math.Cos(lat2 * Math.PI / 180d) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private async Task<byte[]?> TryDownloadAndCacheAsync(string audioUrl, CancellationToken cancellationToken)
    {
        try
        {
            _audioHttpClient ??= CreateAudioHttpClient();

            using var response = await _audioHttpClient
                .GetAsync(audioUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[AudioCache] Network fetch failed {audioUrl} -> {(int)response.StatusCode} {response.StatusCode}");
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (bytes.Length == 0)
                return null;

            MemoryCache[audioUrl] = bytes;

            await _sqliteService.UpsertCacheAsync(new AppCacheEntry
            {
                CacheKey = BuildAudioCacheKey(audioUrl),
                JsonData = Convert.ToBase64String(bytes),
                UpdatedAtUtc = DateTime.UtcNow
            }).ConfigureAwait(false);

            return bytes;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioCache] Download failed for {audioUrl}: {ex.Message}");
            return null;
        }
    }

    private async Task<byte[]?> TryReadCachedBytesAsync(string audioUrl, bool allowStale)
    {
        try
        {
            var cacheKey = BuildAudioCacheKey(audioUrl);
            var entry = allowStale
                ? await _sqliteService.GetCacheAsync(cacheKey).ConfigureAwait(false)
                : await _sqliteService.GetCacheIfFreshAsync(cacheKey, FreshAudioCacheAge).ConfigureAwait(false);

            if (entry is null || string.IsNullOrWhiteSpace(entry.JsonData))
                return null;

            return Convert.FromBase64String(entry.JsonData);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioCache] Cache read failed for {audioUrl}: {ex.Message}");
            return null;
        }
    }

    private static string NormalizeAudioUrl(string? audioUrl)
    {
        if (string.IsNullOrWhiteSpace(audioUrl))
            return string.Empty;

        var normalized = audioUrl.Trim();
        if (normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        return BackendUrlResolver.BuildUrl(normalized);
    }

    private static bool HasInternet()
    {
        return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
    }

    private static string BuildAudioCacheKey(string audioUrl)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(audioUrl));
        return $"aud_bin_{Convert.ToHexString(bytes)}";
    }

    private static HttpClient CreateAudioHttpClient()
    {
        var handler = new HttpClientHandler();

#if DEBUG
        handler.ServerCertificateCustomValidationCallback =
            (_, _, _, _) => true;
#endif

        var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(12)
        };

        BackendUrlResolver.ConfigureHttpClient(httpClient);
        return httpClient;
    }
}
