using MauiApp1.Models;
using Microsoft.Maui.Controls.Maps;
using System.Security.Cryptography;

namespace MauiApp1.Views.Maps;

public partial class PoiMapPage
{
    private static readonly TimeSpan BinaryCacheMaxAge = TimeSpan.FromDays(7);
    private const int MarkerLoadParallelism = 3;

    private static string NormalizeImagePath(string? dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
            return "mypham.jpg";

        dbPath = dbPath.Trim();

        if (dbPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            dbPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return BuildFullUrl(dbPath) ?? "mypham.jpg";
        }

        var normalizedPath = dbPath.Replace("\\", "/");

        // Nếu chỉ là filename thì ưu tiên ảnh local đã package trong Resources.
        if (!normalizedPath.Contains('/'))
            return normalizedPath;

        // Nếu là relative path từ backend thì ghép base URL để tải đúng từ API server.
        return BuildFullUrl(normalizedPath) ?? "mypham.jpg";
    }

    private static bool IsRemoteImageUrl(string value)
    {
        return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private ImageSource BuildImageSource(string? dbPath)
    {
        var normalized = NormalizeImagePath(dbPath);

        if (!IsRemoteImageUrl(normalized))
            return normalized;

        return ImageSource.FromStream(async cancellationToken =>
        {
            try
            {
                var bytes = await GetImageBytesAsync(normalized, cancellationToken).ConfigureAwait(false);
                if (bytes is not null)
                    return new MemoryStream(bytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ImageRender][ERROR] {normalized} -> {ex.Message}");
            }

            return await FileSystem.OpenAppPackageFileAsync("dotnet_bot.png");
        });
    }

    private async Task<byte[]?> GetImageBytesAsync(string imageUrl, CancellationToken cancellationToken)
    {
        var cacheKey = BuildBinaryCacheKey("img", imageUrl);

        if (_imageBytesCache.TryGetValue(imageUrl, out var memoryBytes))
            return memoryBytes;

        if (HasInternet())
        {
            try
            {
                _imageRenderHttpClient ??= CreateImageRenderHttpClient();
                using var response = await _imageRenderHttpClient.GetAsync(imageUrl, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                    _imageBytesCache[imageUrl] = bytes;
                    await SaveBinaryCacheAsync(cacheKey, bytes).ConfigureAwait(false);
                    return bytes;
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[ImageRender][WARN] Network image failed {imageUrl} -> {(int)response.StatusCode} {response.StatusCode}. Try sqlite cache.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ImageRender][WARN] Network image exception {imageUrl}: {ex.Message}. Try sqlite cache.");
            }
        }

        var sqliteBytes = await GetBinaryCacheAsync(cacheKey).ConfigureAwait(false);
        if (sqliteBytes is not null)
        {
            _imageBytesCache[imageUrl] = sqliteBytes;
            return sqliteBytes;
        }

        return null;
    }

    private async Task<string?> PrepareMarkerImagePathAsync(string? dbPath, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeImagePath(dbPath);
        if (!IsRemoteImageUrl(normalized))
            return normalized;

        try
        {
            var bytes = await GetImageBytesAsync(normalized, cancellationToken).ConfigureAwait(false);
            if (bytes is null || bytes.Length == 0)
                return null;

            var fileName = BuildBinaryCacheKey("marker", normalized) + ".img";
            var markerDir = Path.Combine(FileSystem.CacheDirectory, "marker-images");
            Directory.CreateDirectory(markerDir);

            var filePath = Path.Combine(markerDir, fileName);
            if (!File.Exists(filePath))
                await File.WriteAllBytesAsync(filePath, bytes, cancellationToken).ConfigureAwait(false);

            return filePath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MarkerImage][WARN] {normalized} -> {ex.Message}");
            return null;
        }
    }

    private async Task BuildStyledPinsAsync(IEnumerable<PoiItem> pois, CancellationToken cancellationToken = default)
    {
        using var semaphore = new SemaphoreSlim(MarkerLoadParallelism);

        var markerTasks = pois.Select(async poi =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var markerImagePath = await PrepareMarkerImagePathAsync(poi.ImagePath, cancellationToken).ConfigureAwait(false);
                return (poi, markerImagePath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MarkerLoad][WARN] {poi.Title}: {ex.Message}");
                return (poi, markerImagePath: (string?)null);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var preparedPins = await Task.WhenAll(markerTasks).ConfigureAwait(false);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            foreach (var (poi, markerImagePath) in preparedPins)
            {
                var pin = new StyledPin
                {
                    Label = poi.Title,
                    Address = poi.Subtitle,
                    Type = PinType.Place,
                    Location = new Location(poi.Latitude, poi.Longitude),
                    Rating = 4.9,
                    ImagePath = markerImagePath
                };

                pin.MarkerClicked += async (_, e) =>
                {
                    e.HideInfoWindow = true;
                    await OpenDetailAsync(poi);
                };

                _pinsByPoiId[poi.IDChiNhanh] = pin;
            }
        });
    }

    private async Task<byte[]?> GetAudioBytesAsync(string audioUrl)
    {
        var cacheKey = BuildBinaryCacheKey("aud", audioUrl);

        if (_audioBytesCache.TryGetValue(audioUrl, out var memoryBytes))
            return memoryBytes;

        if (HasInternet())
        {
            try
            {
                using var http = CreateHttpClientForAudio();
                var bytes = await http.GetByteArrayAsync(audioUrl);
                _audioBytesCache[audioUrl] = bytes;
                await SaveBinaryCacheAsync(cacheKey, bytes);
                return bytes;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioCache][WARN] Network audio exception {audioUrl}: {ex.Message}. Try sqlite cache.");
            }
        }

        var sqliteBytes = await GetBinaryCacheAsync(cacheKey);
        if (sqliteBytes is not null)
        {
            _audioBytesCache[audioUrl] = sqliteBytes;
            return sqliteBytes;
        }

        return null;
    }

    private async Task SaveBinaryCacheAsync(string cacheKey, byte[] bytes)
    {
        try
        {
            await _sqliteService.UpsertCacheAsync(new AppCacheEntry
            {
                CacheKey = cacheKey,
                JsonData = Convert.ToBase64String(bytes),
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BinaryCache][WARN] Save failed for key {cacheKey}: {ex.Message}");
        }
    }

    private async Task<byte[]?> GetBinaryCacheAsync(string cacheKey)
    {
        try
        {
            var entry = await _sqliteService.GetCacheIfFreshAsync(cacheKey, BinaryCacheMaxAge);
            if (entry is null || string.IsNullOrWhiteSpace(entry.JsonData))
                return null;

            return Convert.FromBase64String(entry.JsonData);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BinaryCache][WARN] Read failed for key {cacheKey}: {ex.Message}");
            return null;
        }
    }

    private static string BuildBinaryCacheKey(string kind, string url)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url));
        return $"{kind}_bin_{Convert.ToHexString(bytes)}";
    }

    // Demo helper: clear toàn bộ in-memory image/audio cache + marker file đã ghi xuống disk.
    public static void ClearAllMediaCaches()
    {
        _imageBytesCache.Clear();
        _audioBytesCache.Clear();

        try
        {
            var markerDir = Path.Combine(FileSystem.CacheDirectory, "marker-images");
            if (Directory.Exists(markerDir))
                Directory.Delete(markerDir, recursive: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MediaCache] Marker dir cleanup failed: {ex.Message}");
        }
    }

    private static HttpClient CreateImageRenderHttpClient()
    {
        var handler = new HttpClientHandler();

#if DEBUG
        handler.ServerCertificateCustomValidationCallback =
            (message, cert, chain, errors) => true;
#endif

        var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        global::MauiApp1.Utils.BackendUrlResolver.ConfigureHttpClient(httpClient);
        return httpClient;
    }

}


