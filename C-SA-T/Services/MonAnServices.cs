using MauiApp1.Models;

namespace MauiApp1.Services
{
    public class MonAnService
    {
        private readonly AppDataCacheService _cacheService;
        private readonly LocalizationService _localizationService;

        public MonAnService(AppDataCacheService cacheService, LocalizationService localizationService)
        {
            _cacheService = cacheService;
            _localizationService = localizationService;
        }

        private string GetCurrentLang()
        {
            var lang = _localizationService.CurrentLanguage?.Trim().ToLowerInvariant();
            return string.IsNullOrWhiteSpace(lang) ? "vi" : lang;
        }

        public async Task<List<MonAn>> GetByGianHangAsync(int idGianHang, string? lang = null, bool forceRefresh = false)
        {
            var currentLang = lang ?? GetCurrentLang();
            var appData = await _cacheService.GetAsync(currentLang, forceRefresh);

            return appData.GianHangs
                .FirstOrDefault(x => x.IdGianHang == idGianHang)?
                .MonAns ?? new List<MonAn>();
        }

        public async Task<List<MonAn>> GetByChiNhanhAsync(int idChiNhanh, string? lang = null, bool forceRefresh = false)
        {
            return await GetByGianHangAsync(idChiNhanh, lang, forceRefresh);
        }

        public async Task<List<MonAn>> GetAllAsync(string? lang = null, bool forceRefresh = false)
        {
            var currentLang = lang ?? GetCurrentLang();
            var appData = await _cacheService.GetAsync(currentLang, forceRefresh);

            return appData.GianHangs
                .SelectMany(x => x.MonAns)
                .ToList();
        }
    }
}
