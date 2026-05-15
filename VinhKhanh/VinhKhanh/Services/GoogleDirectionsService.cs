using System.Globalization;
using System.Text.Json;
using VinhKhanh.Dtos;

namespace VinhKhanh.Services
{
    public class GoogleDirectionsService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private string? _cachedApiKey;

        public GoogleDirectionsService(
            HttpClient httpClient,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _environment = environment;
        }

        public async Task<TourRouteDto> GetWalkingRouteAsync(
            double fromLat,
            double fromLon,
            double toLat,
            double toLon,
            CancellationToken ct = default)
        {
            var fallback = BuildFallback(fromLat, fromLon, toLat, toLon);
            var apiKey = ResolveApiKey();

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                fallback.Message = "Missing Google Maps API key.";
                return fallback;
            }

            try
            {
                var origin = FormatLatLon(fromLat, fromLon);
                var destination = FormatLatLon(toLat, toLon);
                var url =
                    "https://maps.googleapis.com/maps/api/directions/json" +
                    $"?origin={Uri.EscapeDataString(origin)}" +
                    $"&destination={Uri.EscapeDataString(destination)}" +
                    "&mode=walking" +
                    $"&key={Uri.EscapeDataString(apiKey)}";

                using var response = await _httpClient.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    fallback.Status = response.StatusCode.ToString();
                    fallback.Message = "Google Directions request failed.";
                    return fallback;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                var root = doc.RootElement;
                var status = root.TryGetProperty("status", out var statusEl)
                    ? statusEl.GetString()
                    : null;

                if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
                {
                    fallback.Status = status;
                    fallback.Message = TryReadErrorMessage(root) ?? "Google Directions returned no route.";
                    return fallback;
                }

                if (!root.TryGetProperty("routes", out var routes) ||
                    routes.ValueKind != JsonValueKind.Array ||
                    routes.GetArrayLength() == 0)
                {
                    fallback.Status = status;
                    fallback.Message = "Google Directions returned an empty route.";
                    return fallback;
                }

                var route = routes[0];
                if (!route.TryGetProperty("overview_polyline", out var polyline) ||
                    !polyline.TryGetProperty("points", out var pointsEl))
                {
                    fallback.Status = status;
                    fallback.Message = "Google Directions route has no overview polyline.";
                    return fallback;
                }

                var encoded = pointsEl.GetString();
                var points = DecodePolyline(encoded);
                if (points.Count < 2)
                {
                    fallback.Status = status;
                    fallback.Message = "Google Directions polyline is empty.";
                    return fallback;
                }

                return new TourRouteDto
                {
                    Success = true,
                    IsFallback = false,
                    Provider = "google",
                    Status = status,
                    Points = points
                };
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                fallback.Message = ex.Message;
                return fallback;
            }
        }

        private static TourRouteDto BuildFallback(double fromLat, double fromLon, double toLat, double toLon) =>
            new()
            {
                Success = true,
                IsFallback = true,
                Provider = "fallback",
                Points =
                {
                    new RoutePointDto { Lat = fromLat, Lon = fromLon },
                    new RoutePointDto { Lat = toLat, Lon = toLon }
                }
            };

        private string? ResolveApiKey()
        {
            if (!string.IsNullOrWhiteSpace(_cachedApiKey))
                return _cachedApiKey;

            _cachedApiKey =
                _configuration["GoogleMaps:ApiKey"] ??
                _configuration["GoogleMapsApiKey"] ??
                Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY") ??
                TryReadSharedBrowserKeyFile();

            return _cachedApiKey;
        }

        private string? TryReadSharedBrowserKeyFile()
        {
            try
            {
                var dir = new DirectoryInfo(_environment.ContentRootPath);
                while (dir is not null)
                {
                    var keyPath = Path.Combine(dir.FullName, "CS_admin", "Secret", "google-maps-browser-key.txt");
                    if (File.Exists(keyPath))
                    {
                        var value = File.ReadAllText(keyPath).Trim();
                        if (!string.IsNullOrWhiteSpace(value))
                            return value;
                    }

                    dir = dir.Parent;
                }
            }
            catch
            {
            }

            return null;
        }

        private static string FormatLatLon(double lat, double lon) =>
            string.Join(
                ",",
                lat.ToString(CultureInfo.InvariantCulture),
                lon.ToString(CultureInfo.InvariantCulture));

        private static string? TryReadErrorMessage(JsonElement root)
        {
            return root.TryGetProperty("error_message", out var errorEl)
                ? errorEl.GetString()
                : null;
        }

        private static List<RoutePointDto> DecodePolyline(string? encoded)
        {
            var poly = new List<RoutePointDto>();
            if (string.IsNullOrWhiteSpace(encoded))
                return poly;

            var index = 0;
            var lat = 0;
            var lng = 0;

            while (index < encoded.Length)
            {
                lat += DecodeNextValue(encoded, ref index);
                lng += DecodeNextValue(encoded, ref index);

                poly.Add(new RoutePointDto
                {
                    Lat = lat / 100000.0,
                    Lon = lng / 100000.0
                });
            }

            return poly;
        }

        private static int DecodeNextValue(string encoded, ref int index)
        {
            var result = 0;
            var shift = 0;
            int b;

            do
            {
                b = encoded[index++] - 63;
                result |= (b & 0x1f) << shift;
                shift += 5;
            }
            while (b >= 0x20 && index < encoded.Length);

            return (result & 1) != 0 ? ~(result >> 1) : result >> 1;
        }
    }
}
