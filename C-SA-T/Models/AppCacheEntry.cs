using SQLite;

namespace MauiApp1.Models
{
    [Table("AppCacheEntries")]
    public class AppCacheEntry
    {
        [PrimaryKey]
        public string CacheKey { get; set; } = string.Empty;

        [NotNull]
        public string JsonData { get; set; } = string.Empty;

        public DateTime UpdatedAtUtc { get; set; }
    }
}