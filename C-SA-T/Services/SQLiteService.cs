using MauiApp1.Models;
using SQLite;

namespace MauiApp1.Services
{
    public class SQLiteService
    {
        private readonly SQLiteAsyncConnection _db;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private bool _initialized;
        private static readonly TimeSpan DefaultExpiredGracePeriod = TimeSpan.FromDays(30);

        public SQLiteService()
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "vinhkhanh_cache.db3");
            _db = new SQLiteAsyncConnection(dbPath);
        }

        public async Task InitAsync()
        {
            if (_initialized)
                return;

            await _initLock.WaitAsync();
            try
            {
                if (_initialized)
                    return;

                await _db.CreateTableAsync<AppCacheEntry>();
                await _db.CreateTableAsync<AccessTokenCacheEntry>();
                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task<int> UpsertCacheAsync(AppCacheEntry entry)
        {
            await InitAsync();
            return await _db.InsertOrReplaceAsync(entry);
        }

        public async Task<AppCacheEntry?> GetCacheAsync(string cacheKey)
        {
            await InitAsync();
            return await _db.Table<AppCacheEntry>()
                .FirstOrDefaultAsync(x => x.CacheKey == cacheKey);
        }

        public async Task<AppCacheEntry?> GetCacheIfFreshAsync(string cacheKey, TimeSpan maxAge)
        {
            var entry = await GetCacheAsync(cacheKey);
            if (entry is null)
                return null;

            var age = DateTime.UtcNow - entry.UpdatedAtUtc;
            if (age <= maxAge)
                return entry;

            return null;
        }

        public async Task<AppCacheEntry?> GetLatestCacheByPrefixAsync(string cacheKeyPrefix)
        {
            await InitAsync();
            return await _db.Table<AppCacheEntry>()
                .Where(x => x.CacheKey.StartsWith(cacheKeyPrefix))
                .OrderByDescending(x => x.UpdatedAtUtc)
                .FirstOrDefaultAsync();
        }

        public async Task<int> DeleteCacheAsync(string cacheKey)
        {
            await InitAsync();
            return await _db.DeleteAsync<AppCacheEntry>(cacheKey);
        }

        public async Task<int> ClearAllCacheAsync()
        {
            await InitAsync();
            return await _db.DeleteAllAsync<AppCacheEntry>();
        }

        public async Task<int> DeleteExpiredCacheAsync(TimeSpan maxAge)
        {
            await InitAsync();
            var cutoff = DateTime.UtcNow - maxAge;
            var expiredEntries = await _db.Table<AppCacheEntry>()
                .Where(x => x.UpdatedAtUtc < cutoff)
                .ToListAsync();

            var deleted = 0;
            foreach (var entry in expiredEntries)
            {
                deleted += await _db.DeleteAsync<AppCacheEntry>(entry.CacheKey);
            }

            return deleted;
        }

        public Task<int> CleanupOldCacheAsync()
        {
            return DeleteExpiredCacheAsync(DefaultExpiredGracePeriod);
        }

        public async Task<int> UpsertAccessTokenCacheAsync(AccessTokenCacheEntry entry)
        {
            await InitAsync();
            return await _db.InsertOrReplaceAsync(entry);
        }

        public async Task<AccessTokenCacheEntry?> GetAccessTokenCacheAsync(string accessToken)
        {
            await InitAsync();
            return await _db.Table<AccessTokenCacheEntry>()
                .FirstOrDefaultAsync(x => x.AccessToken == accessToken);
        }

        public async Task<AccessTokenCacheEntry?> GetLatestValidAccessTokenCacheAsync(string clientDeviceId)
        {
            await InitAsync();

            var now = DateTime.UtcNow;
            var entries = await _db.Table<AccessTokenCacheEntry>().ToListAsync();
            return entries
                .Where(x =>
                    string.Equals(x.ClientDeviceId, clientDeviceId, StringComparison.OrdinalIgnoreCase) &&
                    x.ExpiresAtUtc.HasValue &&
                    x.ExpiresAtUtc.Value > now &&
                    string.Equals(x.LastStatus, "hieu_luc", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.UpdatedAtUtc)
                .FirstOrDefault();
        }

        public async Task<int> DeleteAccessTokenCacheAsync(string accessToken)
        {
            await InitAsync();
            return await _db.DeleteAsync<AccessTokenCacheEntry>(accessToken);
        }

        public async Task<int> ClearAccessTokenCachesAsync()
        {
            await InitAsync();
            return await _db.DeleteAllAsync<AccessTokenCacheEntry>();
        }
    }
}
