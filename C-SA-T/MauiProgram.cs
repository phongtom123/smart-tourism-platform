using MauiApp1.Services;
using MauiApp1.Utils;
using MauiApp1.Views;
using MauiApp1.Views.Auth;
using MauiApp1.Views.Maps;
using Microsoft.Extensions.Logging;
using Plugin.Maui.Audio;
using ZXing.Net.Maui.Controls;
#if ANDROID
using MauiApp1.Platforms.Android.Maps;
using MauiApp1.Platforms.Android;
#elif WINDOWS
using MauiApp1.Platforms.Windows;
#endif

namespace MauiApp1;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiMaps()
            .UseBarcodeReader()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if ANDROID
        MapPinStyling.Configure();
#endif

        var baseUrl = BackendUrlResolver.GetBaseUrl();

        builder.Services.AddSingleton<HttpClient>(sp =>
        {
            var handler = new HttpClientHandler();

#if DEBUG
            handler.ServerCertificateCustomValidationCallback =
                (message, cert, chain, errors) => true;
#endif

            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(baseUrl)
            };

            BackendUrlResolver.ConfigureHttpClient(httpClient);
            return httpClient;
        });

        builder.Services.AddSingleton(AudioManager.Current);

        builder.Services.AddSingleton<ClientDeviceIdentityService>();
        builder.Services.AddSingleton<SQLiteService>();
        builder.Services.AddSingleton<AudioCacheService>();
        builder.Services.AddSingleton<ApiService>();
        builder.Services.AddSingleton<AccessFlowService>();
        builder.Services.AddSingleton<DeviceHeartbeatService>();
        builder.Services.AddSingleton<AppDataCacheService>();
        builder.Services.AddSingleton<GeofenceEngineService>();

        builder.Services.AddSingleton<GianHangService>();
        builder.Services.AddSingleton<MonAnService>();
        builder.Services.AddSingleton<PoiService>();
        builder.Services.AddSingleton<TourService>();

        builder.Services.AddSingleton<LocalizationService>();

        builder.Services.AddSingleton<GoogleServiceAccountJsonProvider>();
        builder.Services.AddSingleton<ImagePathHelper>();

        builder.Services.AddTransient<GianHangPage>();
        builder.Services.AddTransient<MonAnPage>();
        builder.Services.AddTransient<PoiMapPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<TourPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<QrScanPage>();
        builder.Services.AddTransient<AccessEntryPage>();
        builder.Services.AddTransient<PackageRegistrationPage>();
        builder.Services.AddTransient<PackageQrPaymentPage>();

#if ANDROID
        builder.Services.AddSingleton<IImageGallerySaver, ImageGallerySaver>();
#elif WINDOWS
        builder.Services.AddSingleton<IImageGallerySaver, ImageGallerySaver>();
#endif

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var imageHelper = app.Services.GetRequiredService<ImagePathHelper>();
                await imageHelper.CopyImagesToResourcesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MauiProgram] Error initializing images: {ex.Message}");
            }
        });

        return app;
    }
}
