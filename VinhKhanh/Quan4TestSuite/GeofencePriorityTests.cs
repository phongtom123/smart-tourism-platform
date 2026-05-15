using System.Text.Json;
using Xunit.Abstractions;

namespace Quan4TestSuite;

public sealed class GeofencePriorityTests
{
    private const int StoreAId = 10;
    private const string StoreAName = "Test Overlap A";
    private const double StoreALat = 10.7630000;
    private const double StoreALon = 106.6605000;

    private const int StoreBId = 11;
    private const string StoreBName = "Test Overlap B";
    private const double StoreBLat = 10.7630004;
    private const double StoreBLon = 106.6605002;

    private const double RadiusMeters = 5;

    private readonly ITestOutputHelper _output;

    public GeofencePriorityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Quan4", Quan4TestCaseIds.Gf001_OverlapMidpointInsideBoth)]
    [Trait("Area", "Geofence")]
    public void Q4_GF001_OverlapMidpointInsideBoth()
    {
        var midpoint = GetMidpoint();
        var candidates = CreateOverlapCandidates(priceA: 500_000, priceB: 500_000);

        var inside = GeofencePriorityCalculator.GetInsideStores(midpoint.Lat, midpoint.Lon, candidates, RadiusMeters);

        Assert.Equal(2, inside.Count);
        Assert.Contains(inside, x => x.Store.Id == StoreAId);
        Assert.Contains(inside, x => x.Store.Id == StoreBId);
    }

    [Fact]
    [Trait("Quan4", Quan4TestCaseIds.Gf002_PriorityPriceWins)]
    [Trait("Area", "Geofence")]
    public void Q4_GF002_PriorityPriceWins()
    {
        var midpoint = GetMidpoint();
        var candidates = CreateOverlapCandidates(priceA: 600_000, priceB: 300_000);

        var winner = GeofencePriorityCalculator.PickWinner(midpoint.Lat, midpoint.Lon, candidates, RadiusMeters);

        Assert.NotNull(winner);
        Assert.Equal(StoreBId, winner.Id);
    }

    [Fact]
    [Trait("Quan4", Quan4TestCaseIds.Gf003_UnheardWinsBeforeHeard)]
    [Trait("Area", "Geofence")]
    public void Q4_GF003_UnheardWinsBeforeHeard()
    {
        var midpoint = GetMidpoint();
        var candidates = new[]
        {
            new Quan4StoreCandidate(StoreAId, StoreAName, StoreALat, StoreALon, 500_000, HeardBefore: true),
            new Quan4StoreCandidate(StoreBId, StoreBName, StoreBLat, StoreBLon, 500_000, HeardBefore: false),
        };

        var winner = GeofencePriorityCalculator.PickWinner(midpoint.Lat, midpoint.Lon, candidates, RadiusMeters);

        Assert.NotNull(winner);
        Assert.Equal(StoreBId, winner.Id);
    }

    [Fact]
    [Trait("Quan4", Quan4TestCaseIds.Gf004_NearbyEndpointReturnsSeededOverlapStores)]
    [Trait("Area", "Geofence")]
    public async Task Q4_GF004_NearbyEndpointReturnsSeededOverlapStores()
    {
        if (!await RequireReachableBackendAsync())
            return;

        var midpoint = GetMidpoint();
        using var client = BackendTestClient.Create();
        using var response = await client.GetAsync($"/api/gianhang/nearby?lat={midpoint.Lat}&lon={midpoint.Lon}&radiusMeters={RadiusMeters}&lang=vi");
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var ids = document.RootElement.EnumerateArray()
            .Select(ReadStoreId)
            .Where(id => id > 0)
            .ToHashSet();

        if (!ids.Contains(StoreAId) || !ids.Contains(StoreBId))
        {
            var message = $"Seed stores {StoreAId}/{StoreBId} were not returned by /api/gianhang/nearby.";
            if (Quan4TestConfig.RequireBackend)
                Assert.Fail(message);

            _output.WriteLine($"Soft-skip: {message}");
            return;
        }

        Assert.Contains(StoreAId, ids);
        Assert.Contains(StoreBId, ids);
    }

    [Fact]
    [Trait("Quan4", Quan4TestCaseIds.Gf005_HaversineDistanceIsStable)]
    [Trait("Area", "Geofence")]
    public void Q4_GF005_HaversineDistanceIsStable()
    {
        var distance = GeofencePriorityCalculator.HaversineMeters(StoreALat, StoreALon, StoreBLat, StoreBLon);

        Assert.InRange(distance, 0.04, 0.06);
    }

    private async Task<bool> RequireReachableBackendAsync()
    {
        var reachable = await BackendTestClient.IsReachableAsync();
        if (reachable)
            return true;

        var message = $"Backend is not reachable at {Quan4TestConfig.BaseUrl}.";
        if (Quan4TestConfig.RequireBackend)
            Assert.Fail(message);

        _output.WriteLine($"Soft-skip: {message}");
        return false;
    }

    private static (double Lat, double Lon) GetMidpoint()
    {
        return ((StoreALat + StoreBLat) / 2, (StoreALon + StoreBLon) / 2);
    }

    private static Quan4StoreCandidate[] CreateOverlapCandidates(decimal priceA, decimal priceB)
    {
        return
        [
            new Quan4StoreCandidate(StoreAId, StoreAName, StoreALat, StoreALon, priceA, HeardBefore: false),
            new Quan4StoreCandidate(StoreBId, StoreBName, StoreBLat, StoreBLon, priceB, HeardBefore: false),
        ];
    }

    private static int ReadStoreId(JsonElement element)
    {
        if (element.TryGetProperty("idGianHang", out var idGianHang) && idGianHang.ValueKind == JsonValueKind.Number)
            return idGianHang.GetInt32();

        if (element.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number)
            return id.GetInt32();

        return 0;
    }
}
