using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var backendUrl = builder.Configuration["GEORUNNER_BACKEND_URL"]
    ?? Environment.GetEnvironmentVariable("GEORUNNER_BACKEND_URL")
    ?? "http://localhost:5114";

builder.Services.AddSingleton(new RunnerLogStore());
builder.Services.AddHttpClient("backend", client =>
{
    client.BaseAddress = new Uri(backendUrl.TrimEnd('/'));
    client.Timeout = TimeSpan.FromSeconds(8);
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/config", () => new
{
    backendUrl,
    radiusMeters = 28,
    pollMs = 1000
});

app.MapGet("/api/pois", async (
    IHttpClientFactory httpClientFactory,
    RunnerLogStore logs,
    CancellationToken cancellationToken) =>
{
    try
    {
        var client = httpClientFactory.CreateClient("backend");
        using var adminResponse = await client.GetAsync("/api/admin/poi-map?idTaiKhoan=1", cancellationToken);
        var adminBody = await adminResponse.Content.ReadAsStringAsync(cancellationToken);

        if (adminResponse.IsSuccessStatusCode)
        {
            var adminPois = ParseAdminPois(adminBody);
            if (adminPois.Count > 0)
            {
                logs.AddServer($"GET /api/admin/poi-map?idTaiKhoan=1 -> 200, loaded {adminPois.Count} backend map POI");
                return Results.Json(new PoiListResponse(adminPois, IsFallback: false, Error: null));
            }

            logs.AddServer("GET /api/admin/poi-map?idTaiKhoan=1 -> 200, but no usable POI");
        }
        else
        {
            logs.AddServer($"GET /api/admin/poi-map?idTaiKhoan=1 -> {(int)adminResponse.StatusCode} {adminResponse.ReasonPhrase}");
        }

        using var response = await client.GetAsync("/api/poi?lang=vi", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        var pois = ParsePois(body);
        if (response.IsSuccessStatusCode && pois.Count > 0)
        {
            logs.AddServer($"GET /api/poi?lang=vi -> 200, loaded {pois.Count} public POI fallback");
            return Results.Json(new PoiListResponse(pois, IsFallback: false, Error: null));
        }

        logs.AddServer($"GET /api/poi?lang=vi -> {(int)response.StatusCode} {response.ReasonPhrase}; using local fallback");
        return Results.Json(new PoiListResponse(FallbackPois(), IsFallback: true, Error: body));
    }
    catch (Exception ex)
    {
        logs.AddServer($"GET /api/poi?lang=vi failed: {ex.Message}");
        return Results.Json(new PoiListResponse(FallbackPois(), IsFallback: true, Error: ex.Message));
    }
});

app.MapPost("/api/visit/{poiId:int}", async (
    int poiId,
    VisitRequest visit,
    IHttpClientFactory httpClientFactory,
    RunnerLogStore logs,
    CancellationToken cancellationToken) =>
{
    var deviceId = string.IsNullOrWhiteSpace(visit.DeviceId)
        ? "SIM-DEVICE"
        : visit.DeviceId.Trim();

    try
    {
        var client = httpClientFactory.CreateClient("backend");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/poi/{poiId}/visit");
        request.Headers.Add("X-Device-Id", deviceId);

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var queued = ReadBool(body, "queued");
        var success = ReadBool(body, "success");
        var line = $"POST /api/poi/{poiId}/visit device={deviceId} -> {(int)response.StatusCode}; success={success}; queued={queued}";

        logs.AddServer(line);
        return Results.Json(new VisitResponse((int)response.StatusCode, success, queued, body));
    }
    catch (Exception ex)
    {
        logs.AddServer($"POST /api/poi/{poiId}/visit device={deviceId} failed: {ex.Message}");
        return Results.Json(new VisitResponse(0, false, false, ex.Message), statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapGet("/api/tours", async (
    IHttpClientFactory httpClientFactory,
    RunnerLogStore logs,
    CancellationToken cancellationToken) =>
{
    try
    {
        var client = httpClientFactory.CreateClient("backend");
        using var response = await client.GetAsync("/api/tour?lang=1", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logs.AddServer($"GET /api/tour?lang=1 -> {(int)response.StatusCode}");
        return Results.Content(body, "application/json", statusCode: (int)response.StatusCode);
    }
    catch (Exception ex)
    {
        logs.AddServer($"GET /api/tour?lang=1 failed: {ex.Message}");
        return Results.Json(Array.Empty<object>(), statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapGet("/api/tour/{tourId:int}", async (
    int tourId,
    IHttpClientFactory httpClientFactory,
    RunnerLogStore logs,
    CancellationToken cancellationToken) =>
{
    try
    {
        var client = httpClientFactory.CreateClient("backend");
        using var response = await client.GetAsync($"/api/tour/{tourId}", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logs.AddServer($"GET /api/tour/{tourId} -> {(int)response.StatusCode}");
        return Results.Content(body, "application/json", statusCode: (int)response.StatusCode);
    }
    catch (Exception ex)
    {
        logs.AddServer($"GET /api/tour/{tourId} failed: {ex.Message}");
        return Results.Json(new { success = false, message = ex.Message }, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapGet("/api/tour/{tourId:int}/progress", async (
    int tourId,
    string deviceId,
    IHttpClientFactory httpClientFactory,
    RunnerLogStore logs,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(deviceId))
        return Results.BadRequest(new { success = false, message = "Thieu deviceId." });

    try
    {
        var client = httpClientFactory.CreateClient("backend");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/tour/{tourId}/progress");
        request.Headers.Add("X-Device-Id", deviceId.Trim());

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logs.AddServer($"GET /api/tour/{tourId}/progress device={deviceId.Trim()} -> {(int)response.StatusCode}");
        return Results.Content(body, "application/json", statusCode: (int)response.StatusCode);
    }
    catch (Exception ex)
    {
        logs.AddServer($"GET /api/tour/{tourId}/progress device={deviceId.Trim()} failed: {ex.Message}");
        return Results.Json(new { success = false, message = ex.Message }, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapPost("/api/tour/{tourId:int}/advance", async (
    int tourId,
    TourAdvanceRequest advance,
    IHttpClientFactory httpClientFactory,
    RunnerLogStore logs,
    CancellationToken cancellationToken) =>
{
    var deviceId = string.IsNullOrWhiteSpace(advance.DeviceId)
        ? "SIM-DEVICE"
        : advance.DeviceId.Trim();

    if (advance.IdGianHangVuaDen <= 0)
        return Results.BadRequest(new { success = false, message = "Thieu idGianHangVuaDen." });

    try
    {
        var client = httpClientFactory.CreateClient("backend");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/tour/{tourId}/advance");
        request.Headers.Add("X-Device-Id", deviceId);
        request.Content = JsonContent.Create(new { idTour = tourId, idGianHangVuaDen = advance.IdGianHangVuaDen });

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var success = ReadBool(body, "success");
        logs.AddServer($"POST /api/tour/{tourId}/advance device={deviceId} poi={advance.IdGianHangVuaDen} -> {(int)response.StatusCode}; success={success}");
        return Results.Content(body, "application/json", statusCode: (int)response.StatusCode);
    }
    catch (Exception ex)
    {
        logs.AddServer($"POST /api/tour/{tourId}/advance device={deviceId} poi={advance.IdGianHangVuaDen} failed: {ex.Message}");
        return Results.Json(new { success = false, message = ex.Message }, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapGet("/api/server-log", (RunnerLogStore logs) => logs.Get());

app.MapPost("/api/server-log/clear", (RunnerLogStore logs) =>
{
    logs.Clear();
    logs.AddServer("server log cleared");
    return Results.Ok(new { ok = true });
});

app.MapPost("/api/save-log", async (
    SaveLogRequest request,
    RunnerLogStore logs,
    CancellationToken cancellationToken) =>
{
    var logDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".codex-runlogs"));
    Directory.CreateDirectory(logDir);

    var fileName = $"geofence-ui-{DateTime.Now:yyyyMMdd-HHmmss}.log";
    var path = Path.Combine(logDir, fileName);
    var text = new StringBuilder();
    text.AppendLine("=== Geofence Simulator Client Log ===");
    text.AppendLine(request.ClientLog ?? string.Empty);
    text.AppendLine();
    text.AppendLine("=== Backend Server Log ===");
    foreach (var line in logs.Get())
        text.AppendLine(line.Message);

    await File.WriteAllTextAsync(path, text.ToString(), Encoding.UTF8, cancellationToken);
    logs.AddServer($"saved combined log -> {path}");

    return Results.Ok(new { path });
});

static IReadOnlyList<PoiDto> ParsePois(string json)
{
    using var document = JsonDocument.Parse(json);
    if (document.RootElement.ValueKind != JsonValueKind.Array)
        return [];

    var pois = new List<PoiDto>();
    foreach (var element in document.RootElement.EnumerateArray())
    {
        var id = ReadInt(element, "id");
        var name = ReadString(element, "ten") ?? $"POI {id}";
        var lat = ReadNullableDouble(element, "lat");
        var lon = ReadNullableDouble(element, "lon");

        if (id > 0 && lat.HasValue && lon.HasValue)
            pois.Add(new PoiDto(id, name, lat.Value, lon.Value, RadiusMeters: 28, Visits: 0, Status: null, MonthlyFee: 0, IsSynthetic: false));
    }

    return pois;
}

static IReadOnlyList<PoiDto> ParseAdminPois(string json)
{
    using var document = JsonDocument.Parse(json);
    if (document.RootElement.ValueKind != JsonValueKind.Array)
        return [];

    var pois = new List<PoiDto>();
    foreach (var element in document.RootElement.EnumerateArray())
    {
        var id = ReadInt(element, "idGianHang");
        var name = ReadString(element, "ten") ?? $"Gian hang {id}";
        var lat = ReadNullableDouble(element, "lat");
        var lon = ReadNullableDouble(element, "lon");
        var radius = ReadNullableDouble(element, "vongBo") ?? 10d;
        var visits = ReadInt(element, "luotTruyCap");
        var status = ReadString(element, "tinhTrang");
        var monthlyFee = ReadDecimal(element, "phiHangThang");

        if (id > 0 && lat.HasValue && lon.HasValue)
        {
            pois.Add(new PoiDto(
                id,
                name,
                lat.Value,
                lon.Value,
                RadiusMeters: Math.Clamp(radius, 6d, 80d),
                visits,
                status,
                monthlyFee,
                IsSynthetic: false));
        }
    }

    return pois;
}

static IReadOnlyList<PoiDto> FallbackPois() =>
[
    new(1, "Bo nuong Cambodia", 10.762622, 106.660172, 28, 0, null, 0, IsSynthetic: false),
    new(2, "DAU HU THUI & TRA SUA CO UT", 10.762850, 106.660620, 28, 0, null, 0, IsSynthetic: false),
    new(3, "Tra sua mr tea", 10.763150, 106.661020, 28, 0, null, 0, IsSynthetic: false)
];

static bool ReadBool(string json, string propertyName)
{
    try
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.True;
    }
    catch
    {
        return false;
    }
}

static int ReadInt(JsonElement element, string propertyName)
{
    return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
        ? value.GetInt32()
        : 0;
}

static double? ReadNullableDouble(JsonElement element, string propertyName)
{
    return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
        ? value.GetDouble()
        : null;
}

static decimal ReadDecimal(JsonElement element, string propertyName)
{
    return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
        ? value.GetDecimal()
        : 0m;
}

static string? ReadString(JsonElement element, string propertyName)
{
    return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
        ? value.GetString()
        : null;
}

app.Run();

public sealed class RunnerLogStore
{
    private readonly ConcurrentQueue<RunnerLogLine> _lines = new();

    public void AddServer(string message)
    {
        _lines.Enqueue(new RunnerLogLine(DateTimeOffset.Now, message));
        while (_lines.Count > 400 && _lines.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<RunnerLogLine> Get() => _lines.ToArray();

    public void Clear()
    {
        while (_lines.TryDequeue(out _))
        {
        }
    }
}

public sealed record RunnerLogLine(DateTimeOffset Time, string Message);

public sealed record PoiDto(
    int Id,
    string Name,
    double Lat,
    double Lon,
    double RadiusMeters,
    int Visits,
    string? Status,
    decimal MonthlyFee,
    bool IsSynthetic);

public sealed record PoiListResponse(IReadOnlyList<PoiDto> Pois, bool IsFallback, string? Error);

public sealed record VisitRequest(string DeviceId);

public sealed record VisitResponse(int StatusCode, bool Success, bool Queued, string Body);

public sealed record TourAdvanceRequest(string DeviceId, int IdGianHangVuaDen);

public sealed record SaveLogRequest(string? ClientLog);
