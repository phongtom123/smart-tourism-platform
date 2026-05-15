namespace Quan4TestSuite;

public sealed record Quan4StoreCandidate(
    int Id,
    string Name,
    double Latitude,
    double Longitude,
    decimal MonthlyPrice,
    bool HeardBefore);

public static class GeofencePriorityCalculator
{
    public static IReadOnlyList<(Quan4StoreCandidate Store, double DistanceMeters)> GetInsideStores(
        double userLatitude,
        double userLongitude,
        IEnumerable<Quan4StoreCandidate> stores,
        double radiusMeters)
    {
        return stores
            .Select(store => (Store: store, DistanceMeters: HaversineMeters(userLatitude, userLongitude, store.Latitude, store.Longitude)))
            .Where(x => x.DistanceMeters <= radiusMeters)
            .ToList();
    }

    public static Quan4StoreCandidate? PickWinner(
        double userLatitude,
        double userLongitude,
        IEnumerable<Quan4StoreCandidate> stores,
        double radiusMeters)
    {
        return GetInsideStores(userLatitude, userLongitude, stores, radiusMeters)
            .OrderBy(x => x.Store.MonthlyPrice)
            .ThenBy(x => x.Store.HeardBefore ? 1 : 0)
            .ThenBy(x => x.Store.Id)
            .Select(x => x.Store)
            .FirstOrDefault();
    }

    public static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6_371_000;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusMeters * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
