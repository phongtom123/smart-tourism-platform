using System.Net.Sockets;
using Microsoft.Maui.Devices;

namespace MauiApp1.Utils;

public static class BackendUrlResolver
{
    public const string PreferenceKey = "backend_base_url";

    private const string NgrokSkipBrowserWarningHeaderName = "ngrok-skip-browser-warning";
    private const string NgrokSkipBrowserWarningHeaderValue = "true";
    private const string EmulatorHttpsBaseUrl = "https://10.0.2.2:7123/";
    private const string EmulatorHttpBaseUrl = "http://10.0.2.2:5114/";
    private const string AndroidDeviceLocalHttpBaseUrl = "http://192.168.31.235:5114/";
    private const string AndroidDeviceLocalHttpsBaseUrl = "https://192.168.31.235:7123/";
    private const string AndroidDeviceReverseHttpBaseUrl = "http://localhost:5114/";
    private const string AndroidDeviceReverseHttpsBaseUrl = "https://localhost:7123/";
    private const string AndroidDeviceFallbackBaseUrl = "https://rudder-lake-yelp.ngrok-free.dev/";
    private const string DesktopHttpsBaseUrl = "https://localhost:7123/";
    private const string DesktopHttpBaseUrl = "http://localhost:5114/";
    private const int LocalProbeTimeoutMs = 700;

    private static readonly object SyncRoot = new();
    private static string? _configuredBaseUrl;
    private static string? _autoDetectedBaseUrl;

    public static void Configure(string? configuredBaseUrl)
    {
        lock (SyncRoot)
        {
            _configuredBaseUrl = NormalizeOverrideBaseUrl(configuredBaseUrl);
            _autoDetectedBaseUrl = null;
        }
    }

    public static string GetBaseUrl()
    {
        var configured = GetConfiguredBaseUrl();
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        lock (SyncRoot)
        {
            if (!string.IsNullOrWhiteSpace(_autoDetectedBaseUrl))
                return _autoDetectedBaseUrl;
        }

        var detected = ResolveDefaultBaseUrl();
        lock (SyncRoot)
        {
            _autoDetectedBaseUrl = detected;
        }

        return detected;
    }

    public static void ResetAutoDetectedBaseUrl()
    {
        lock (SyncRoot)
        {
            _autoDetectedBaseUrl = null;
        }
    }

    public static IReadOnlyList<string> GetCandidateBaseUrls()
    {
        var configured = GetConfiguredBaseUrl();
        if (!string.IsNullOrWhiteSpace(configured))
            return new[] { configured };

#if ANDROID
        return DeviceInfo.DeviceType == DeviceType.Virtual
            ? new[]
            {
                EmulatorHttpBaseUrl,
                EmulatorHttpsBaseUrl,
                AndroidDeviceFallbackBaseUrl
            }
            : new[]
            {
                // Ngrok đứng đầu để phone không phụ thuộc LAN IP máy host.
                // Nếu LAN IP máy đổi (Wi-Fi mới, DHCP renew), app vẫn reach được backend qua internet.
                AndroidDeviceFallbackBaseUrl,
                AndroidDeviceLocalHttpBaseUrl,
                AndroidDeviceLocalHttpsBaseUrl,
                AndroidDeviceReverseHttpBaseUrl,
                AndroidDeviceReverseHttpsBaseUrl
            };
#else
        return new[]
        {
            DesktopHttpBaseUrl,
            DesktopHttpsBaseUrl
        };
#endif
    }

    public static string BuildUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return GetBaseUrl();

        var trimmedPath = path.Trim();
        if (Uri.TryCreate(trimmedPath, UriKind.Absolute, out var absoluteUri) &&
            (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
        {
            return RewriteLoopbackUrl(absoluteUri);
        }

        return new Uri(new Uri(GetBaseUrl()), trimmedPath.TrimStart('/')).ToString();
    }

    public static void ConfigureHttpClient(HttpClient httpClient)
    {
        if (!httpClient.DefaultRequestHeaders.Contains(NgrokSkipBrowserWarningHeaderName))
            httpClient.DefaultRequestHeaders.Add(NgrokSkipBrowserWarningHeaderName, NgrokSkipBrowserWarningHeaderValue);
    }

    public static string? NormalizeOverrideBaseUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return null;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return null;

        return trimmed.EndsWith("/", StringComparison.Ordinal) ? trimmed : trimmed + "/";
    }

    private static string? GetConfiguredBaseUrl()
    {
        lock (SyncRoot)
        {
            if (!string.IsNullOrWhiteSpace(_configuredBaseUrl))
                return _configuredBaseUrl;
        }

        return NormalizeOverrideBaseUrl(Environment.GetEnvironmentVariable("MAUI_BACKEND_URL"));
    }

    private static string ResolveDefaultBaseUrl()
    {
        var candidates = GetCandidateBaseUrls();

        for (var i = 0; i < candidates.Count - 1; i++)
        {
            if (CanReachTcpPort(candidates[i]))
                return candidates[i];
        }

        return candidates[^1];
    }

    private static bool CanReachTcpPort(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        try
        {
            using var client = new TcpClient();
            var port = uri.IsDefaultPort ? (uri.Scheme == Uri.UriSchemeHttps ? 443 : 80) : uri.Port;
            var connectTask = client.ConnectAsync(uri.Host, port);
            var timeoutTask = Task.Delay(LocalProbeTimeoutMs);
            var completedTask = Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false).GetAwaiter().GetResult();

            if (completedTask != connectTask)
                return false;

            connectTask.ConfigureAwait(false).GetAwaiter().GetResult();
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static string RewriteLoopbackUrl(Uri absoluteUri)
    {
        if (!IsLoopbackHost(absoluteUri.Host))
            return absoluteUri.ToString();

        var baseUri = new Uri(GetBaseUrl());
        var builder = new UriBuilder(absoluteUri)
        {
            Scheme = baseUri.Scheme,
            Host = baseUri.Host,
            Port = baseUri.IsDefaultPort ? -1 : baseUri.Port
        };

        return builder.Uri.ToString();
    }

    private static bool IsLoopbackHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("0.0.0.0", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("::1", StringComparison.OrdinalIgnoreCase);
    }
}
