using System.Text.Json;
using System.Collections.Concurrent;
using MauiApp1.Models;

namespace MauiApp1.Services
{
    public class AppDataCacheService
    {
        private readonly ApiService _apiService;
        private readonly SQLiteService _sqliteService;
        private readonly AudioCacheService _audioCacheService;
        private readonly object _refreshLock = new();

        private readonly ConcurrentDictionary<string, AppDataResponse> _memoryCache =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DateTime> _lastRefreshAttemptUtc =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _refreshingKeys =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan AppDataCacheMaxAge = TimeSpan.FromHours(12);
        private static readonly TimeSpan BackgroundRefreshInterval = TimeSpan.FromMinutes(5);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public AppDataCacheService(
            ApiService apiService,
            SQLiteService sqliteService,
            AudioCacheService audioCacheService)
        {
            _apiService = apiService;
            _sqliteService = sqliteService;
            _audioCacheService = audioCacheService;
        }

        public async Task<AppDataResponse> GetAsync(string lang = "vi", bool forceRefresh = false)
        {
            lang = string.IsNullOrWhiteSpace(lang) ? "vi" : lang.Trim().ToLowerInvariant();
            var cacheKey = $"appdata_{lang}";

            if (!forceRefresh && _memoryCache.TryGetValue(cacheKey, out var memoryData))
            {
                QueueRefreshIfDue(cacheKey, lang);
                return memoryData;
            }

            if (forceRefresh)
            {
                var refreshed = await TryFetchAndCacheFromApiAsync(lang, cacheKey);
                if (refreshed is not null)
                    return refreshed;

                if (_memoryCache.TryGetValue(cacheKey, out memoryData))
                    return memoryData;
            }

            var freshCached = await TryReadCachedResponseAsync(cacheKey, AppDataCacheMaxAge);
            if (freshCached is not null)
            {
                _memoryCache[cacheKey] = freshCached;
                return freshCached;
            }

            var staleCached = await TryReadCachedResponseAsync(cacheKey, maxAge: null);
            if (staleCached is not null)
            {
                _memoryCache[cacheKey] = staleCached;
                QueueRefresh(cacheKey, lang);
                return staleCached;
            }

            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                var apiData = await TryFetchAndCacheFromApiAsync(lang, cacheKey);
                if (apiData is not null)
                    return apiData;
            }

            try
            {
                var fallback = await _sqliteService.GetLatestCacheByPrefixAsync("appdata_");
                if (fallback != null && !string.IsNullOrWhiteSpace(fallback.JsonData))
                {
                    var cachedData = JsonSerializer.Deserialize<AppDataResponse>(fallback.JsonData, JsonOptions);
                    if (cachedData != null)
                        return cachedData;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppDataCacheService] Latest cached fallback error: {ex.Message}");
            }

            var empty = new AppDataResponse();
            _memoryCache[cacheKey] = empty;
            return empty;
        }

        public async Task RefreshAsync(string lang = "vi")
        {
            await GetAsync(lang, forceRefresh: true);
        }

        public async Task ClearAsync()
        {
            _memoryCache.Clear();
            _lastRefreshAttemptUtc.Clear();
            await _sqliteService.ClearAllCacheAsync();
        }

        public Task CleanupExpiredCacheAsync()
        {
            return _sqliteService.DeleteExpiredCacheAsync(AppDataCacheMaxAge);
        }

        public void ClearMemory()
        {
            _memoryCache.Clear();
            _lastRefreshAttemptUtc.Clear();
        }

        private async Task<AppDataResponse?> TryReadCachedResponseAsync(string cacheKey, TimeSpan? maxAge)
        {
            try
            {
                var local = maxAge.HasValue
                    ? await _sqliteService.GetCacheIfFreshAsync(cacheKey, maxAge.Value)
                    : await _sqliteService.GetCacheAsync(cacheKey);

                if (local is null || string.IsNullOrWhiteSpace(local.JsonData))
                    return null;

                return JsonSerializer.Deserialize<AppDataResponse>(local.JsonData, JsonOptions);
            }
            catch (Exception ex)
            {
                var cacheType = maxAge.HasValue ? "Fresh" : "Stale";
                System.Diagnostics.Debug.WriteLine($"[AppDataCacheService] {cacheType} SQLite error: {ex.Message}");
                return null;
            }
        }

        private async Task<AppDataResponse?> TryFetchAndCacheFromApiAsync(string lang, string cacheKey)
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return null;

            _lastRefreshAttemptUtc[cacheKey] = DateTime.UtcNow;

            try
            {
                var apiData = await _apiService.GetAppDataAsync(lang);

                if (apiData is null)
                    return null;

                await _sqliteService.UpsertCacheAsync(new AppCacheEntry
                {
                    CacheKey = cacheKey,
                    JsonData = JsonSerializer.Serialize(apiData, JsonOptions),
                    UpdatedAtUtc = DateTime.UtcNow
                });

                _memoryCache[cacheKey] = apiData;
                _ = PrefetchAudioInBackgroundAsync(apiData);
                return apiData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppDataCacheService] API error: {ex.Message}");
                return null;
            }
        }

        private void QueueRefreshIfDue(string cacheKey, string lang)
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return;

            if (_lastRefreshAttemptUtc.TryGetValue(cacheKey, out var lastAttempt) &&
                DateTime.UtcNow - lastAttempt < BackgroundRefreshInterval)
            {
                return;
            }

            QueueRefresh(cacheKey, lang);
        }

        private void QueueRefresh(string cacheKey, string lang)
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return;

            lock (_refreshLock)
            {
                if (!_refreshingKeys.Add(cacheKey))
                    return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await TryFetchAndCacheFromApiAsync(lang, cacheKey);
                }
                finally
                {
                    lock (_refreshLock)
                    {
                        _refreshingKeys.Remove(cacheKey);
                    }
                }
            });
        }

        private async Task PrefetchAudioInBackgroundAsync(AppDataResponse appData)
        {
            try
            {
                await _audioCacheService.PrefetchForAppDataAsync(appData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppDataCacheService] Audio prefetch error: {ex.Message}");
            }
        }
    }
}
