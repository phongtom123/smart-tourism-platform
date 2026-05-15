using System.Collections.Concurrent;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace MauiApp1.Utils;

public static class RemoteImageSourceFactory
{
    private static readonly ConcurrentDictionary<string, byte[]> ImageBytesCache = new(StringComparer.Ordinal);
    private static readonly object HttpClientLock = new();
    private static HttpClient? _httpClient;

    public static ImageSource Build(string? dbPath, string fallbackFile = "dotnet_bot.png")
    {
        var normalized = Normalize(dbPath, fallbackFile);
        if (!IsRemoteImageUrl(normalized))
            return ImageSource.FromFile(normalized);

        return ImageSource.FromStream(async cancellationToken =>
        {
            try
            {
                if (ImageBytesCache.TryGetValue(normalized, out var cachedBytes))
                    return new MemoryStream(cachedBytes);

                var httpClient = GetOrCreateHttpClient();
                using var response = await httpClient.GetAsync(normalized, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    return await FileSystem.OpenAppPackageFileAsync(fallbackFile).ConfigureAwait(false);

                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                ImageBytesCache[normalized] = bytes;
                return new MemoryStream(bytes);
            }
            catch
            {
                return await FileSystem.OpenAppPackageFileAsync(fallbackFile).ConfigureAwait(false);
            }
        });
    }

    public static string Normalize(string? dbPath, string fallbackFile = "dotnet_bot.png")
    {
        if (string.IsNullOrWhiteSpace(dbPath))
            return fallbackFile;

        var trimmedPath = dbPath.Trim();
        if (Uri.TryCreate(trimmedPath, UriKind.Absolute, out var absoluteUri))
        {
            if (absoluteUri.IsFile)
                return absoluteUri.LocalPath;

            if (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps)
                return BackendUrlResolver.BuildUrl(trimmedPath);
        }

        if (Path.IsPathRooted(trimmedPath))
            return trimmedPath;

        var normalizedPath = trimmedPath.Replace("\\", "/");
        if (!normalizedPath.Contains('/'))
            return normalizedPath;

        return BackendUrlResolver.BuildUrl(normalizedPath);
    }

    private static bool IsRemoteImageUrl(string value)
    {
        return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient GetOrCreateHttpClient()
    {
        if (_httpClient is not null)
            return _httpClient;

        lock (HttpClientLock)
        {
            _httpClient ??= CreateHttpClient();
            return _httpClient;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler();

#if DEBUG
        handler.ServerCertificateCustomValidationCallback =
            (_, _, _, _) => true;
#endif

        var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        BackendUrlResolver.ConfigureHttpClient(httpClient);
        return httpClient;
    }
}
