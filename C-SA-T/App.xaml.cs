using MauiApp1.Services;
using MauiApp1.Models;
using MauiApp1.Utils;
using MauiApp1.Views;
using MauiApp1.Views.Maps;

namespace MauiApp1;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly DeviceHeartbeatService _heartbeatService;
    private HomePage? _homePage;
    private PoiMapPage? _explorePage;
    private TourPage? _tourPage;
    private SettingsPage? _settingsPage;

    public App(
        IServiceProvider services,
        SQLiteService sqliteService,
        LocalizationService localizationService,
        DeviceHeartbeatService heartbeatService)
    {
        InitializeComponent();

        _services = services;
        _heartbeatService = heartbeatService;

        var savedBackendUrl = Preferences.Get(BackendUrlResolver.PreferenceKey, string.Empty);
        BackendUrlResolver.Configure(savedBackendUrl);

        var startupLanguage = LocalizationService.ResolveStartupLanguage();
        localizationService.SetLanguage(startupLanguage);

        _ = InitializeDatabaseAsync(sqliteService);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new NavigationPage(_services.GetRequiredService<AccessEntryPage>()));
        window.Activated += (_, _) => _heartbeatService.Start();
        window.Deactivated += (_, _) => _heartbeatService.Stop();
        window.Resumed += (_, _) => _heartbeatService.Start();
        window.Stopped += (_, _) => _heartbeatService.Stop();
        return window;
    }

    public Task ShowAccessEntryAsync()
    {
        _homePage = null;
        _explorePage = null;
        _tourPage = null;
        _settingsPage = null;
        return SetRootAsync(_services.GetRequiredService<AccessEntryPage>());
    }

    public Task ShowMainPageAsync()
    {
        return ShowHomePageInternalAsync();
    }

    public Task ShowExplorePageAsync(bool autoOpenExplore = false)
    {
        return ShowTabPageInternalAsync(GetOrCreateExplorePage, autoOpenExplore);
    }

    public Task ShowSettingsPageAsync()
    {
        return ShowTabPageInternalAsync(GetOrCreateSettingsPage);
    }

    public Task ShowTourPageAsync()
    {
        return ShowTabPageInternalAsync(GetOrCreateTourPage);
    }

    public async Task StartTourAsync(TourDetail tourDetail)
    {
        var explorePage = GetOrCreateExplorePage();
        explorePage.RequestStartTour(tourDetail);
        await ShowTabPageInternalAsync(() => explorePage);
    }

    public Task PreloadExplorePageAsync()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                _ = GetOrCreateExplorePage();
                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    private Task SetRootAsync(Page page)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var window = Windows.FirstOrDefault();
                if (window is not null)
                    window.Page = new NavigationPage(page);

                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    private Task ShowHomePageInternalAsync()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var homePage = GetOrCreateHomePage();
                var window = Windows.FirstOrDefault();

                if (window?.Page is NavigationPage nav && nav.Navigation.NavigationStack.Contains(homePage))
                {
                    await nav.PopToRootAsync(false);
                }
                else
                {
                    if (homePage.Parent is not null)
                    {
                        _homePage = _services.GetRequiredService<HomePage>();
                        homePage = _homePage;
                    }

                    window!.Page = new NavigationPage(homePage);
                }

                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    private Task ShowTabPageInternalAsync<TPage>(Func<TPage> pageFactory, bool autoOpenExplore = false)
        where TPage : Page
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var page = pageFactory();
                if (autoOpenExplore && page is PoiMapPage explorePage)
                    explorePage.RequestAutoOpenExplore();

                var window = Windows.FirstOrDefault();
                if (window?.Page is not NavigationPage nav)
                {
                    window!.Page = new NavigationPage(GetOrCreateHomePage());
                    nav = (NavigationPage)window.Page;
                }

                var stack = nav.Navigation.NavigationStack;
                if (nav.CurrentPage == page)
                {
                    tcs.TrySetResult(true);
                    return;
                }

                if (stack.Contains(page))
                {
                    while (nav.CurrentPage != page && nav.Navigation.NavigationStack.Count > 1)
                        await nav.PopAsync(false);
                }
                else if (page.Parent is null)
                {
                    await nav.PushAsync(page, false);
                }
                else
                {
                    var freshPage = _services.GetRequiredService<TPage>();
                    if (freshPage is PoiMapPage freshExplore)
                        _explorePage = freshExplore;
                    else if (freshPage is TourPage freshTour)
                        _tourPage = freshTour;
                    else if (freshPage is SettingsPage freshSettings)
                        _settingsPage = freshSettings;

                    if (autoOpenExplore && freshPage is PoiMapPage freshExplorePage)
                        freshExplorePage.RequestAutoOpenExplore();

                    await nav.PushAsync(freshPage, false);
                }

                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    private HomePage GetOrCreateHomePage()
    {
        return _homePage ??= _services.GetRequiredService<HomePage>();
    }

    private PoiMapPage GetOrCreateExplorePage()
    {
        return _explorePage ??= _services.GetRequiredService<PoiMapPage>();
    }

    private TourPage GetOrCreateTourPage()
    {
        return _tourPage ??= _services.GetRequiredService<TourPage>();
    }

    private SettingsPage GetOrCreateSettingsPage()
    {
        return _settingsPage ??= _services.GetRequiredService<SettingsPage>();
    }

    private static async Task InitializeDatabaseAsync(SQLiteService sqliteService)
    {
        try
        {
            await sqliteService.InitAsync();
            await sqliteService.CleanupOldCacheAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] SQLite init error: {ex.Message}");
        }
    }
}
