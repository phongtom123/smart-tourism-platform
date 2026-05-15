using SQLite;

namespace MauiApp1.Models
{
    [Table("AccessTokenCacheEntries")]
    public class AccessTokenCacheEntry
    {
        [PrimaryKey]
        public string AccessToken { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string DeviceCode { get; set; } = string.Empty;
        public string ClientDeviceId { get; set; } = string.Empty;
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public string LastStatus { get; set; } = string.Empty;
        public DateTime LastValidatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
