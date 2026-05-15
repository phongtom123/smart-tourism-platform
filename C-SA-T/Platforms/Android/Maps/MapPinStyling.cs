using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using MauiApp1.Services;
using Microsoft.Maui.Maps.Handlers;
using MauiApp1.Models;
using System.Collections.Generic;
using System.Security.Cryptography;
using AndroidBitmap = global::Android.Graphics.Bitmap;
using AndroidBitmapFactory = global::Android.Graphics.BitmapFactory;

namespace MauiApp1.Platforms.Android.Maps;

internal static class MapPinStyling
{
    private static bool _isConfigured;
    private static readonly object _styledMapsLock = new();
    private static readonly HashSet<nint> _styledMapHandles = [];
    private static readonly object _iconCacheLock = new();
    private static readonly Dictionary<double, BitmapDescriptor> _iconCache = [];
    private static Dictionary<string, BitmapDescriptor>? _imageIconCache;
    private static BitmapDescriptor? _userLocationIconCache;

    // Custom theme - sáng, sạch, nhấn màu cam theo giao diện app
    private static readonly string CustomMapStyle = "[" +
        "{\"elementType\":\"geometry\",\"stylers\":[{\"color\":\"#f6f1ea\"}]}" +
        ",{\"elementType\":\"labels.icon\",\"stylers\":[{\"visibility\":\"off\"}]}" +
        ",{\"elementType\":\"labels.text.fill\",\"stylers\":[{\"color\":\"#6b4f3a\"}]}" +
        ",{\"elementType\":\"labels.text.stroke\",\"stylers\":[{\"color\":\"#fffaf5\"}]}" +
        ",{\"featureType\":\"administrative\",\"elementType\":\"geometry.stroke\",\"stylers\":[{\"color\":\"#d7c3b2\"}]}" +
        ",{\"featureType\":\"administrative.land_parcel\",\"elementType\":\"labels.text.fill\",\"stylers\":[{\"color\":\"#9d7b61\"}]}" +
        ",{\"featureType\":\"poi\",\"elementType\":\"geometry\",\"stylers\":[{\"color\":\"#f1e4d8\"}]}" +
        ",{\"featureType\":\"poi\",\"elementType\":\"labels.text.fill\",\"stylers\":[{\"color\":\"#7d5b45\"}]}" +
        ",{\"featureType\":\"poi.park\",\"elementType\":\"geometry\",\"stylers\":[{\"color\":\"#dce8d6\"}]}" +
        ",{\"featureType\":\"poi.park\",\"elementType\":\"labels.text.fill\",\"stylers\":[{\"color\":\"#5e7a56\"}]}" +
        ",{\"featureType\":\"road\",\"elementType\":\"geometry\",\"stylers\":[{\"color\":\"#fffaf7\"}]}" +
        ",{\"featureType\":\"road.arterial\",\"elementType\":\"labels.text.fill\",\"stylers\":[{\"color\":\"#8f6a52\"}]}" +
        ",{\"featureType\":\"road.highway\",\"elementType\":\"geometry\",\"stylers\":[{\"color\":\"#f0d6c1\"}]}" +
        ",{\"featureType\":\"road.highway\",\"elementType\":\"labels.text.fill\",\"stylers\":[{\"color\":\"#7d4e2e\"}]}" +
        ",{\"featureType\":\"road.local\",\"elementType\":\"labels.text.fill\",\"stylers\":[{\"color\":\"#9e846e\"}]}" +
        ",{\"featureType\":\"transit.line\",\"elementType\":\"geometry\",\"stylers\":[{\"color\":\"#eddccf\"}]}" +
        ",{\"featureType\":\"transit.station\",\"elementType\":\"geometry\",\"stylers\":[{\"color\":\"#f0e2d7\"}]}" +
        ",{\"featureType\":\"water\",\"elementType\":\"geometry\",\"stylers\":[{\"color\":\"#c8dff0\"}]}" +
        ",{\"featureType\":\"water\",\"elementType\":\"labels.text.fill\",\"stylers\":[{\"color\":\"#5b7d9d\"}]}" +
        "]";

    public static void Configure()
    {
        if (_isConfigured)
            return;

        _isConfigured = true;
        _imageIconCache = new Dictionary<string, BitmapDescriptor>();

        MapHandler.Mapper.AppendToMapping("CustomMapTheme", (handler, _) =>
        {
            ApplyCustomTheme(handler);
        });

        MapPinHandler.Mapper.AppendToMapping("StyledPin", (handler, pin) =>
        {
            if (handler.PlatformView is null)
                return;

            if (pin is UserLocationPin)
            {
                var userIcon = GetOrCreateUserLocationIcon();
                handler.PlatformView.SetIcon(userIcon);
                handler.PlatformView.Anchor(0.5f, 0.5f);
                return;
            }

            if (pin is not StyledPin styledPin)
                return;

            var icon = GetOrCreateIcon(styledPin.Rating, styledPin.ImagePath);
            handler.PlatformView.SetIcon(icon);
            handler.PlatformView.Anchor(0.5f, 1f);
        });
    }

    public static void ApplyThemeToMap(Microsoft.Maui.Controls.Maps.Map map)
    {
        if (map.Handler is not IMapHandler handler || handler.PlatformView is null)
            return;

        ApplyCustomTheme(handler);
    }

    private static void ApplyCustomTheme(IMapHandler handler)
    {
        if (handler.PlatformView is null)
            return;

        var mapHandle = handler.PlatformView.Handle;
        if (mapHandle != nint.Zero)
        {
            lock (_styledMapsLock)
            {
                if (!_styledMapHandles.Add(mapHandle))
                    return;
            }
        }

        handler.PlatformView.GetMapAsync(new MapReadyCallback(googleMap =>
        {
            googleMap.MapType = GoogleMap.MapTypeNormal;
            var style = new MapStyleOptions(CustomMapStyle);
            var applied = googleMap.SetMapStyle(style);
            System.Diagnostics.Debug.WriteLine(
                applied
                    ? $"Map custom style applied ({CustomMapStyle.Length} chars)."
                    : "Map custom style reported a parsing issue; Google Maps kept its default render.");
        }));
    }

    private static BitmapDescriptor GetOrCreateUserLocationIcon()
    {
        lock (_iconCacheLock)
        {
            if (_userLocationIconCache != null)
                return _userLocationIconCache;

            var bitmap = UserLocationMarkerFactory.Create(global::Android.App.Application.Context);
            _userLocationIconCache = BitmapDescriptorFactory.FromBitmap(bitmap);
            return _userLocationIconCache;
        }
    }

    private static BitmapDescriptor GetOrCreateIcon(double rating, string? imagePath)
    {
        lock (_iconCacheLock)
        {
            _imageIconCache ??= new Dictionary<string, BitmapDescriptor>();

            // Never do network or sqlite fetches synchronously while rendering map markers.
            // Remote images fall back to the lightweight rating marker to keep gestures smooth.
            if (!string.IsNullOrWhiteSpace(imagePath) &&
                !imagePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !imagePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (_imageIconCache.TryGetValue(imagePath, out var cachedImageIcon))
                    return cachedImageIcon;

                AndroidBitmap? imageBitmap = null;
                try
                {
                    imageBitmap = LoadImageBitmap(imagePath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MapPinStyling] Failed to load local image {imagePath}: {ex.Message}");
                }

                var markerBitmap = CustomMarkerFactory.Create(global::Android.App.Application.Context, rating, imageBitmap);
                var icon = BitmapDescriptorFactory.FromBitmap(markerBitmap);
                _imageIconCache[imagePath] = icon;
                imageBitmap?.Dispose();
                return icon;
            }

            var normalizedRating = Math.Round(rating, 1);
            if (_iconCache.TryGetValue(normalizedRating, out var cachedIcon))
                return cachedIcon;

            var defaultMarkerBitmap = CustomMarkerFactory.Create(global::Android.App.Application.Context, normalizedRating);
            var defaultIcon = BitmapDescriptorFactory.FromBitmap(defaultMarkerBitmap);
            _iconCache[normalizedRating] = defaultIcon;
            return defaultIcon;
        }
    }

    private static AndroidBitmap? LoadImageBitmap(string imagePath)
    {
        System.Diagnostics.Debug.WriteLine($"[LoadImageBitmap] Starting to load from: {imagePath}");
        
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            System.Diagnostics.Debug.WriteLine($"[LoadImageBitmap] Image path is empty or whitespace");
            return null;
        }

        try
        {
            // If it's a URL, download the image
            if (imagePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                imagePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                System.Diagnostics.Debug.WriteLine($"[LoadImageBitmap] Detected URL, downloading...");
                var bitmapFromUrl = LoadBitmapFromUrl(imagePath);
                if (bitmapFromUrl != null)
                    return bitmapFromUrl;

                System.Diagnostics.Debug.WriteLine($"[LoadImageBitmap] URL download failed, trying SQLite cache...");
                return LoadBitmapFromSqliteCache(imagePath);
            }

            // If it's a local file path (absolute)
            if (System.IO.Path.IsPathRooted(imagePath) && System.IO.File.Exists(imagePath))
            {
                System.Diagnostics.Debug.WriteLine($"[LoadImageBitmap] Absolute path exists, decoding from file: {imagePath}");
                var bitmap = AndroidBitmapFactory.DecodeFile(imagePath);
                System.Diagnostics.Debug.WriteLine($"[LoadImageBitmap] Decoded absolute path file, result: {(bitmap != null ? "Success" : "Null")}");
                return bitmap;
            }

            // Try to find in app files directory (AppDataDirectory)
            var appDataPath = FileSystem.AppDataDirectory;
            var resourcePath = System.IO.Path.Combine(appDataPath, imagePath);
            System.Diagnostics.Debug.WriteLine($"[LoadImageBitmap] Trying app data path: {resourcePath}");
            
            if (System.IO.File.Exists(resourcePath))
            {
                System.Diagnostics.Debug.WriteLine($"[LoadImageBitmap] Found in app data, decoding from: {resourcePath}");
                var bitmap = AndroidBitmapFactory.DecodeFile(resourcePath);
                System.Diagnostics.Debug.WriteLine($"[LoadImageBitmap] Decoded app data file, result: {(bitmap != null ? "Success" : "Null")}");
                return bitmap;
            }

            // Load from app package Raw assets (Resources/Raw/)
            System.Diagnostics.Debug.WriteLine($"[LoadImageBitmap] Trying to load from app package assets...");
            return LoadBitmapFromAssets(imagePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoadImageBitmap] Error loading bitmap from path '{imagePath}': {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    private static AndroidBitmap? LoadBitmapFromUrl(string url)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[LoadBitmapFromUrl] Downloading from {url}...");
            var bytes = Task.Run(async () =>
            {
                var handler = new System.Net.Http.HttpClientHandler();
#if DEBUG
                handler.ServerCertificateCustomValidationCallback =
                    (message, cert, chain, errors) => true;
#endif
                using var httpClient = new System.Net.Http.HttpClient(handler)
                {
                    Timeout = TimeSpan.FromSeconds(8)
                };
                global::MauiApp1.Utils.BackendUrlResolver.ConfigureHttpClient(httpClient);

                return await httpClient.GetByteArrayAsync(url).ConfigureAwait(false);
            }).GetAwaiter().GetResult();

            var bitmap = AndroidBitmapFactory.DecodeByteArray(bytes, 0, bytes.Length);
            System.Diagnostics.Debug.WriteLine($"[LoadBitmapFromUrl] Decoded bytes, result: {(bitmap != null ? "Success" : "Null")}");
            return bitmap;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoadBitmapFromUrl] Error downloading bitmap from URL '{url}': {ex.Message}\n{ex.StackTrace}");
        }
        return null;
    }

    private static AndroidBitmap? LoadBitmapFromSqliteCache(string url)
    {
        try
        {
            var sqlite = App.Current?.Handler?.MauiContext?.Services?.GetService(typeof(SQLiteService)) as SQLiteService;
            if (sqlite is null)
            {
                System.Diagnostics.Debug.WriteLine("[LoadBitmapFromSqliteCache] SQLiteService is null");
                return null;
            }

            var cacheKey = BuildBinaryCacheKey("img", url);
            var entry = sqlite.GetCacheAsync(cacheKey).GetAwaiter().GetResult();
            if (entry is null || string.IsNullOrWhiteSpace(entry.JsonData))
            {
                System.Diagnostics.Debug.WriteLine($"[LoadBitmapFromSqliteCache] Cache miss for key: {cacheKey}");
                return null;
            }

            var bytes = Convert.FromBase64String(entry.JsonData);
            var bitmap = AndroidBitmapFactory.DecodeByteArray(bytes, 0, bytes.Length);
            System.Diagnostics.Debug.WriteLine($"[LoadBitmapFromSqliteCache] Cache hit for url: {url}, bitmap: {(bitmap != null ? "ok" : "null")}");
            return bitmap;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoadBitmapFromSqliteCache] Error: {ex.Message}");
            return null;
        }
    }

    private static string BuildBinaryCacheKey(string kind, string url)
    {
        var hashBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url));
        return $"{kind}_bin_{Convert.ToHexString(hashBytes)}";
    }

    private static AndroidBitmap? LoadBitmapFromAssets(string assetPath)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[LoadBitmapFromAssets] Loading from app package assets: {assetPath}");
            var context = global::Android.App.Application.Context;
            
            if (context?.Assets == null)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadBitmapFromAssets] Context or Assets is null");
                return null;
            }

            try
            {
                // Try loading directly from assets
                using var stream = context.Assets.Open(assetPath);
                if (stream != null)
                {
                    var bitmap = AndroidBitmapFactory.DecodeStream(stream);
                    System.Diagnostics.Debug.WriteLine($"[LoadBitmapFromAssets] Successfully decoded asset '{assetPath}'");
                    return bitmap;
                }
            }
            catch (Java.IO.IOException)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadBitmapFromAssets] Asset not found directly, trying with 'Resources/Raw/' prefix");
                
                try
                {
                    // Try with Resources/Raw prefix
                    var rawPath = $"Resources/Raw/{assetPath}";
                    using var rawStream = context.Assets.Open(rawPath);
                    if (rawStream != null)
                    {
                        var bitmap = AndroidBitmapFactory.DecodeStream(rawStream);
                        System.Diagnostics.Debug.WriteLine($"[LoadBitmapFromAssets] Successfully decoded from Resources/Raw/{assetPath}");
                        return bitmap;
                    }
                }
                catch (Java.IO.IOException rawIoEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[LoadBitmapFromAssets] Also failed with Resources/Raw prefix: {rawIoEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoadBitmapFromAssets] Error: {ex.Message}");
        }
        
        System.Diagnostics.Debug.WriteLine($"[LoadBitmapFromAssets] Failed to load from app package: {assetPath}");
        return null;
    }

    private sealed class MapReadyCallback(Action<GoogleMap> onMapReady) : global::Java.Lang.Object, IOnMapReadyCallback
    {
        public void OnMapReady(GoogleMap googleMap) => onMapReady(googleMap);
    }
}
