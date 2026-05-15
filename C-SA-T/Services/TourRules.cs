using MauiApp1.Models;

namespace MauiApp1.Services;

public static class TourRules
{
    public const int BaseTourStopBoost = 1000;
    public const int InitialCurrentStopBoost = 4000;
    public const int InitialNextStopBoost = 3500;
    public const int ActiveCurrentStopBoost = 3000;
    public const int ActiveNextStopBoost = 4000;

    public static IReadOnlyList<TourStop> GetUsableStops(TourDetail detail)
    {
        return detail.DanhSachStop
            .Where(s => s.IsAvailable && HasValidCoordinate(s))
            .OrderBy(s => s.ThuTu)
            .ToList();
    }

    public static TourStop? ResolveCurrentStop(TourDetail detail, TourProgress? progress)
    {
        var stops = GetUsableStops(detail);
        if (stops.Count == 0)
            return null;

        var step = progress?.StepHienTai ?? 0;
        if (step <= 0)
            return stops[0];

        var nextAvailable = stops.FirstOrDefault(s => s.ThuTu >= step);
        if (nextAvailable is null)
            return stops[^1];

        return stops.LastOrDefault(s => s.ThuTu < nextAvailable.ThuTu) ?? nextAvailable;
    }

    public static TourStop? ResolveNextAvailableStop(TourDetail detail, TourStop? currentStop)
    {
        if (currentStop is null)
            return null;

        return GetUsableStops(detail)
            .FirstOrDefault(s => s.ThuTu > currentStop.ThuTu);
    }

    public static TourStop? ResolveExpectedGeofenceStop(TourDetail detail, TourProgress? progress)
    {
        if (progress?.IsCompleted == true)
            return null;

        var currentStop = ResolveCurrentStop(detail, progress);
        var nextStop = ResolveNextAvailableStop(detail, currentStop);

        return (progress?.StepHienTai ?? 0) <= 0
            ? currentStop
            : nextStop;
    }

    public static Dictionary<int, int> BuildGeofencePriorityBoosts(TourDetail detail, TourProgress? progress)
    {
        var boosts = new Dictionary<int, int>();
        var stops = GetUsableStops(detail);
        foreach (var stop in stops)
        {
            if (stop.IdGianHang > 0)
                boosts[stop.IdGianHang] = BaseTourStopBoost;
        }

        var currentStop = ResolveCurrentStop(detail, progress);
        var nextStop = ResolveNextAvailableStop(detail, currentStop);
        var step = progress?.StepHienTai ?? 0;

        if (currentStop is not null && currentStop.IdGianHang > 0)
            boosts[currentStop.IdGianHang] = step <= 0 ? InitialCurrentStopBoost : ActiveCurrentStopBoost;

        if (nextStop is not null && nextStop.IdGianHang > 0)
            boosts[nextStop.IdGianHang] = step <= 0 ? InitialNextStopBoost : ActiveNextStopBoost;

        return boosts;
    }

    public static double CalculateProgressRatio(TourDetail detail, TourStop? currentStop, TourStop? nextStop)
    {
        var stops = GetUsableStops(detail).ToList();
        var totalStops = Math.Max(1, stops.Count);
        var currentIndex = currentStop is null
            ? 0
            : Math.Max(0, stops.FindIndex(s => s.IdGianHang == currentStop.IdGianHang));
        if (currentIndex < 0)
            currentIndex = 0;

        var completedStops = nextStop is null ? totalStops : currentIndex + 1;
        return Math.Clamp((double)completedStops / totalStops, 0, 1);
    }

    public static bool HasValidCoordinate(TourStop stop)
    {
        return stop.Lat.HasValue &&
               stop.Lon.HasValue &&
               !double.IsNaN(stop.Lat.Value) &&
               !double.IsNaN(stop.Lon.Value) &&
               !double.IsInfinity(stop.Lat.Value) &&
               !double.IsInfinity(stop.Lon.Value) &&
               stop.Lat.Value is >= -90 and <= 90 &&
               stop.Lon.Value is >= -180 and <= 180;
    }
}
