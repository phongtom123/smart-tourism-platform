using System.Net;
using System.Text.Json;
using Xunit.Abstractions;

namespace Quan4TestSuite;

public sealed class PoiVisitApiTests
{
    private readonly ITestOutputHelper _output;

    public PoiVisitApiTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Quan4", Quan4TestCaseIds.Api001_BackendReachable)]
    [Trait("Area", "Api")]
    public async Task Q4_API001_BackendReachable()
    {
        var reachable = await BackendTestClient.IsReachableAsync();
        _output.WriteLine($"Base URL: {Quan4TestConfig.BaseUrl}");

        if (Quan4TestConfig.RequireBackend)
            Assert.True(reachable, $"Backend is not reachable at {Quan4TestConfig.BaseUrl}.");

        if (!reachable)
            _output.WriteLine("Soft-skip: backend is not running. Set QUAN4_REQUIRE_BACKEND=true to fail this case.");
    }

    [Fact]
    [Trait("Quan4", Quan4TestCaseIds.Pv001_PoiListHasGpsPoi)]
    [Trait("Area", "PoiVisit")]
    public async Task Q4_PV001_PoiListHasGpsPoi()
    {
        if (!await RequireReachableBackendAsync())
            return;

        using var client = BackendTestClient.Create();
        var pois = await GetPoisAsync(client);

        Assert.NotEmpty(pois);
        Assert.Contains(pois, poi => poi.Id > 0 && poi.Latitude.HasValue && poi.Longitude.HasValue);
    }

    [Fact]
    [Trait("Quan4", Quan4TestCaseIds.Pv002_VisitMissingDeviceHeaderBadRequest)]
    [Trait("Area", "PoiVisit")]
    public async Task Q4_PV002_VisitMissingDeviceHeaderBadRequest()
    {
        if (!await RequireReachableBackendAsync())
            return;

        using var client = BackendTestClient.Create();
        var poi = await GetFirstPoiOrSoftSkipAsync(client);
        if (poi is null)
            return;

        var response = await client.PostAsync($"/api/poi/{poi.Id}/visit", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Trait("Quan4", Quan4TestCaseIds.Pv003_VisitAcceptedAndQueued)]
    [Trait("Area", "PoiVisit")]
    public async Task Q4_PV003_VisitAcceptedAndQueued()
    {
        if (!await RequireReachableBackendAsync())
            return;

        using var client = BackendTestClient.Create();
        var poi = await GetFirstPoiOrSoftSkipAsync(client);
        if (poi is null)
            return;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/poi/{poi.Id}/visit");
        request.Headers.Add("X-Device-Id", $"Q4-TEST-{Guid.NewGuid():N}");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.True(ReadBool(body, "success"));
        Assert.True(ReadBool(body, "queued"));
    }

    [Fact]
    [Trait("Quan4", Quan4TestCaseIds.Pv004_DuplicateVisitDeduped)]
    [Trait("Area", "PoiVisit")]
    public async Task Q4_PV004_DuplicateVisitDeduped()
    {
        if (!await RequireReachableBackendAsync())
            return;

        using var client = BackendTestClient.Create();
        var poi = await GetFirstPoiOrSoftSkipAsync(client);
        if (poi is null)
            return;

        var deviceId = $"Q4-DUP-{Guid.NewGuid():N}";
        var first = await SendVisitAsync(client, poi.Id, deviceId);
        var second = await SendVisitAsync(client, poi.Id, deviceId);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.True(ReadBool(first.Body, "queued"));
        Assert.False(ReadBool(second.Body, "queued"));
    }

    [Fact]
    [Trait("Quan4", Quan4TestCaseIds.Pv005_BurstDifferentDevicesAccepted)]
    [Trait("Area", "PoiVisit")]
    public async Task Q4_PV005_BurstDifferentDevicesAccepted()
    {
        if (!await RequireReachableBackendAsync())
            return;

        using var client = BackendTestClient.Create();
        var poi = await GetFirstPoiOrSoftSkipAsync(client);
        if (poi is null)
            return;

        var runId = Guid.NewGuid().ToString("N");
        var tasks = Enumerable.Range(1, Quan4TestConfig.BurstRequestCount)
            .Select(i => SendVisitAsync(client, poi.Id, $"Q4-BURST-{runId}-{i:D4}"))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, result => Assert.Equal(HttpStatusCode.Accepted, result.StatusCode));
        Assert.Equal(Quan4TestConfig.BurstRequestCount, results.Count(result => ReadBool(result.Body, "queued")));
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

    private async Task<PoiSummary?> GetFirstPoiOrSoftSkipAsync(HttpClient client)
    {
        var pois = await GetPoisAsync(client);
        var poi = pois.FirstOrDefault(x => x.Id > 0);
        if (poi is not null)
            return poi;

        var message = "No POI was returned from /api/poi?lang=vi.";
        if (Quan4TestConfig.RequireBackend)
            Assert.Fail(message);

        _output.WriteLine($"Soft-skip: {message}");
        return null;
    }

    private static async Task<IReadOnlyList<PoiSummary>> GetPoisAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/poi?lang=vi");
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);

        return document.RootElement.EnumerateArray()
            .Select(element => new PoiSummary(
                ReadInt(element, "id"),
                ReadNullableDouble(element, "lat"),
                ReadNullableDouble(element, "lon")))
            .ToList();
    }

    private static async Task<(HttpStatusCode StatusCode, string Body)> SendVisitAsync(HttpClient client, int poiId, string deviceId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/poi/{poiId}/visit");
        request.Headers.Add("X-Device-Id", deviceId);

        using var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static int ReadInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;
    }

    private static double? ReadNullableDouble(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;
    }

    private static bool ReadBool(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.True;
    }

    private sealed record PoiSummary(int Id, double? Latitude, double? Longitude);
}
