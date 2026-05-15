using MauiApp1.Models;

namespace MauiApp1.Services
{
    public class GianHangService
    {
        private readonly AppDataCacheService _cacheService;
        private readonly LocalizationService _localizationService;

        public GianHangService(AppDataCacheService cacheService, LocalizationService localizationService)
        {
            _cacheService = cacheService;
            _localizationService = localizationService;
        }

        private string GetCurrentLang()
        {
            var lang = _localizationService.CurrentLanguage?.Trim().ToLowerInvariant();
            return string.IsNullOrWhiteSpace(lang) ? "vi" : lang;
        }

        public async Task<List<GianHang>> GetAllAsync(string? lang = null, bool forceRefresh = false)
        {
            var currentLang = lang ?? GetCurrentLang();
            var appData = await _cacheService.GetAsync(currentLang, forceRefresh);
            return appData.GianHangs;
        }

        public async Task<GianHang?> GetByIdAsync(int idGianHang, string? lang = null, bool forceRefresh = false)
        {
            var currentLang = lang ?? GetCurrentLang();
            var appData = await _cacheService.GetAsync(currentLang, forceRefresh);

            return appData.GianHangs.FirstOrDefault(x => x.IdGianHang == idGianHang);
        }

        public async Task<List<GianHang>> GetNearbyAsync(
            double lat,
            double lon,
            double radiusMeters = 100,
            string? lang = null)
        {
            var currentLang = lang ?? GetCurrentLang();
            var appData = await _cacheService.GetAsync(currentLang);

            return appData.GianHangs
                .Where(x => x.Lat.HasValue && x.Lon.HasValue)
                .Select(x => new
                {
                    GianHang = x,
                    Distance = CalculateDistanceMeters(lat, lon, x.Lat!.Value, x.Lon!.Value)
                })
                .Where(x => x.Distance <= radiusMeters)
                .OrderBy(x => x.Distance)
                .Select(x => x.GianHang)
                .ToList();
        }

        private static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000.0;
            var dLat = DegreesToRadians(lat2 - lat1);
            var dLon = DegreesToRadians(lon2 - lon1);

            var a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }
}
