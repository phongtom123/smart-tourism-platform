using MauiApp1.Models;

namespace MauiApp1.Services;

public class TourService
{
    private readonly ApiService _apiService;
    private readonly LocalizationService _localizationService;

    public TourService(ApiService apiService, LocalizationService localizationService)
    {
        _apiService = apiService;
        _localizationService = localizationService;
    }

    public Task<List<TourSummary>> GetActiveToursAsync()
    {
        return _apiService.GetActiveToursAsync(MapLanguageToId(_localizationService.CurrentLanguage));
    }

    public Task<TourDetail?> GetTourDetailAsync(int idTour)
    {
        return _apiService.GetTourDetailAsync(idTour);
    }

    public Task<TourProgress?> GetProgressAsync(int idTour)
    {
        return _apiService.GetTourProgressAsync(idTour);
    }

    public Task<AdvanceTourResult?> AdvanceAsync(int idTour, int idGianHangVuaDen)
    {
        return _apiService.AdvanceTourAsync(idTour, idGianHangVuaDen);
    }

    public Task<TourRoute> GetRouteAsync(TourStop from, TourStop to)
    {
        if (!HasValidCoordinate(from) || !HasValidCoordinate(to))
        {
            return Task.FromResult(new TourRoute
            {
                Success = false,
                IsFallback = true,
                Message = "Missing coordinates."
            });
        }

        var fromLat = from.Lat.GetValueOrDefault();
        var fromLon = from.Lon.GetValueOrDefault();
        var toLat = to.Lat.GetValueOrDefault();
        var toLon = to.Lon.GetValueOrDefault();

        return _apiService.GetTourRouteAsync(fromLat, fromLon, toLat, toLon);
    }

    public static IReadOnlyList<TourStop> GetUsableStops(TourDetail detail)
    {
        return TourRules.GetUsableStops(detail);
    }

    public static TourStop? ResolveCurrentStop(TourDetail detail, TourProgress? progress)
    {
        return TourRules.ResolveCurrentStop(detail, progress);
    }

    private static int? MapLanguageToId(string? code)
    {
        return LocalizationService.NormalizeLanguageCode(code) switch
        {
            "vi" => 1,
            "en" => 2,
            "ko" => 3,
            "ja" => 4,
            _ => null
        };
    }

    private static bool HasValidCoordinate(TourStop stop)
    {
        return TourRules.HasValidCoordinate(stop);
    }
}
