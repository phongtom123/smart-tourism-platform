using System.Security.Cryptography;
using System.Text;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

#if ANDROID
using Android.Provider;
#endif

namespace MauiApp1.Services;

public sealed class ClientDeviceIdentityService
{
    private const string ClientDeviceIdKey = "client_device_id";

    public string GetOrCreateClientDeviceId()
    {
        var existing = Preferences.Get(ClientDeviceIdKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(existing))
            return existing;

#if ANDROID
        var stableAndroidId = GetStableAndroidDeviceId();
        if (!string.IsNullOrWhiteSpace(stableAndroidId))
        {
            Preferences.Set(ClientDeviceIdKey, stableAndroidId);
            return stableAndroidId;
        }
#endif

        var generated = $"APP-CLIENT-{Guid.NewGuid():N}".ToUpperInvariant();
        Preferences.Set(ClientDeviceIdKey, generated);
        return generated;
    }

    public DeviceMetadata GetDeviceMetadata()
    {
        try
        {
            return new DeviceMetadata
            {
                Platform = DeviceInfo.Current.Platform.ToString(),
                Model = Truncate(DeviceInfo.Current.Model, 128),
                Manufacturer = Truncate(DeviceInfo.Current.Manufacturer, 128),
                AppVersion = Truncate(AppInfo.Current.VersionString, 32)
            };
        }
        catch
        {
            return new DeviceMetadata();
        }
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value.Length <= max ? value : value[..max];
    }

#if ANDROID
    private static string? GetStableAndroidDeviceId()
    {
        try
        {
            var context = Android.App.Application.Context;
            var androidId = Settings.Secure.GetString(context.ContentResolver, Settings.Secure.AndroidId);
            if (string.IsNullOrWhiteSpace(androidId))
                return null;

            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(androidId.Trim().ToUpperInvariant());
            var hash = Convert.ToHexString(sha256.ComputeHash(bytes));
            return $"APP-CLIENT-{hash[..24]}";
        }
        catch
        {
            return null;
        }
    }
#endif
}

public sealed class DeviceMetadata
{
    public string? Platform { get; set; }
    public string? Model { get; set; }
    public string? Manufacturer { get; set; }
    public string? AppVersion { get; set; }
}
