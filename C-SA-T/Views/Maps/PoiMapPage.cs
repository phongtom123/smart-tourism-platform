using MauiApp1.Models;
using MauiApp1.Services;
using MauiApp1.Views;
using MauiApp1.Controls;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Networking;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
#if ANDROID
using MauiApp1.Platforms.Android.Maps;
using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Microsoft.Maui.Maps.Handlers;
using AndroidColor = Android.Graphics.Color;
using AndroidBitmap = Android.Graphics.Bitmap;
using AndroidBitmapConfig = Android.Graphics.Bitmap.Config;
using AndroidCanvas = Android.Graphics.Canvas;
using AndroidPaint = Android.Graphics.Paint;
using AndroidPaintFlags = Android.Graphics.PaintFlags;
using AndroidPaintStyle = Android.Graphics.Paint.Style;
using AndroidPath = Android.Graphics.Path;
#endif
using Map = Microsoft.Maui.Controls.Maps.Map;
using MauiPolyline = Microsoft.Maui.Controls.Maps.Polyline;
using Path = System.IO.Path;

namespace MauiApp1.Views.Maps;

public partial class PoiMapPage : ContentPage
{
    private enum TourSegmentState
    {
        Past,
        Active,
        Future
    }

    private readonly GianHangService _gianHangService;
    private readonly PoiService _poiService;
    private readonly MonAnService _monAnService;
    private readonly TourService _tourService;
    private readonly GeofenceEngineService _geofenceEngine;
    private readonly SQLiteService _sqliteService;
    private readonly AudioCacheService _audioCacheService;
    // DEMO_RESET_CACHE_BUTTON — xóa field này khi gỡ nút.
    private readonly AppDataCacheService _appDataCacheService;
    private readonly SemaphoreSlim _tourAdvanceSync = new(1, 1);
    private List<GianHang> _gianHangsForPrefetch = new();
    private DateTime _lastLazyPrefetchAtUtc = DateTime.MinValue;
    private double? _lastLazyPrefetchLat;
    private double? _lastLazyPrefetchLon;
    private readonly Map _map;
    private readonly View _footer;
    private readonly Grid _topBar;
    private readonly MapActionButton _currentLocationButton;
    private readonly MapActionButton _refreshButton;
    private readonly MapActionButton _mapModeButton;
    private readonly MapActionButton _visualTestButton;
    private readonly Border _visualTestStatusBanner;
    // DEMO_RESET_CACHE_BUTTON — xóa field này khi gỡ nút.
    private readonly MapActionButton _resetCacheButton;
    private readonly Label _mapModeLabel;
    private readonly Border _tourProgressBanner;
    private readonly Label _tourProgressStatusLabel;
    private readonly Label _tourProgressTitleLabel;
    private readonly Label _tourProgressSubtitleLabel;
    private readonly ProgressBar _tourProgressBar;

    private const string DefaultLanguageCode = "vi";
    private const double TourProgressBannerBottomMargin = 100;
    private const double TourProgressBannerEstimatedHeight = 76;
#if ANDROID
    private const float AndroidMap3DDefaultMinZoom = 17.5f;
#endif

    private Entry _searchEntry = null!;

    // Sheet khám phá
    private readonly Grid _bottomSheet;
    private readonly ScrollView _poiScroll;
    private readonly AppRefreshView _poiRefreshView;
    private readonly VerticalStackLayout _poiList;

    private readonly Label _titleLabel;
    private readonly Label _subtitleLabel;

    // Sheet chi tiết
    private readonly Grid _detailSheet;
    private Border _detailPanel = null!;
    private Image _detailImage = null!;
    private Label _detailTitle = null!;
    private Label _detailAddress = null!;
    private Label _detailDescription = null!;
    private Label _detailAudioLabel = null!;
    private HorizontalStackLayout _languageRow = null!;
    private Label _languageSectionLabel = null!;
    private Label _audioSectionLabel = null!;
    private Label _foodImagesSectionLabel = null!;
    private HorizontalStackLayout _foodImagesRow = null!;
    private Button _closeButton = null!;

    private readonly LocalizationService _loc;

    // Audio controls
    private Button _playButton = null!;
    private Slider _progressSlider = null!;
    private Label _currentTimeLabel = null!;
    private Label _durationLabel = null!;

    private static HttpClient? _imageProbeHttpClient;
    private static HttpClient? _imageRenderHttpClient;
    private static readonly ConcurrentDictionary<string, byte[]> _imageBytesCache = new();
    private static readonly ConcurrentDictionary<string, byte[]> _audioBytesCache = new();
    private bool _isDraggingSlider;
    private UserLocationPin? _userLocationPin;

    private readonly List<PoiItem> _pois = new();
    private readonly List<PoiItem> _allPois = new();
    private readonly Dictionary<int, StyledPin> _pinsByPoiId = new();
    private CancellationTokenSource? _pinRefreshCts;

    // Vị trí sheet khám phá
    private double _sheetHiddenY;
    private double _sheetMiniY;
    private double _sheetHalfY;
    private double _sheetFullY;

    private double _currentSheetY;
    private double _panStartSheetY;
    private double _lastPanTotalY;
    private double _lastPanDeltaY;

    private bool _isLayoutReady;
    private bool _isAnimating;
    private bool _isExploreVisible;
    private bool _shouldAutoOpenExplore;
    private bool _isInitialLoadStarted;
    private bool _isInitialLoadCompleted;
    private bool _hasPrimedExploreInfrastructure;
    private bool _isLoadingPois;
    private DateTime _lastPoiLoadAtUtc;
    private string _activeSearchQuery = string.Empty;
    private int? _selectedPoiId;
    private static readonly TimeSpan PoiReloadInterval = TimeSpan.FromMinutes(5);

    // Vị trí sheet chi tiết
    private double _detailHiddenY;
    private double _detailMiniY;
    private double _detailHalfY;
    private double _detailFullY;

    private double _detailCurrentY;
    private double _detailPanStartY;
    private double _detailLastPanTotalY;
    private double _detailLastPanDeltaY;

    private bool _isDetailVisible;
    private bool _isDetailAnimating;
    private bool _isOpeningFoodGallery;
    private bool _isPlaybackStateSubscribed;
    private GianHang? _currentDetailGianHang;
    private TourDetail? _activeTourDetail;
    private TourProgress? _activeTourProgress;
    private int? _pendingTourCompletionExitStoreId;
    private bool _isTourCompletionNotificationShowing;
    private readonly List<MauiPolyline> _mauiTourLines = new();
    private readonly List<NgonNgu> _languages = new();
    private string _selectedLanguageCode = DefaultLanguageCode; // overridden in constructor
    private bool _isLiveLocationSubscribed;
    private bool _isMap3DEnabled;
    private CancellationTokenSource? _liveLocationPollingCts;
    private bool _shouldFollowLiveLocation = false;
    private Location? _lastAutoCenteredLocation;
    private DateTime _lastAutoCenterAtUtc = DateTime.MinValue;
    private static readonly TimeSpan LiveLocationAutoCenterCooldown = TimeSpan.FromSeconds(2);
    private const double LiveLocationAutoCenterMinDistanceMeters = 8;

#if ANDROID
    private GoogleMap? _androidGoogleMap;
    private bool _hasPendingTourRender;
    private int _mapModeRevision;
    private readonly List<Android.Gms.Maps.Model.Polyline> _androidTourLines = new();
    private readonly List<Marker> _androidTourArrows = new();
    private static BitmapDescriptor? _tourArrowIcon;
#endif

    public PoiMapPage(
        PoiService poiService,
        GianHangService gianHangService,
        MonAnService monAnService,
        TourService tourService,
        GeofenceEngineService geofenceEngine,
        SQLiteService sqliteService,
        LocalizationService localizationService,
        AudioCacheService audioCacheService,
        // DEMO_RESET_CACHE_BUTTON — xóa parameter này khi gỡ nút.
        AppDataCacheService appDataCacheService)
    {
        _poiService = poiService;
        _gianHangService = gianHangService;
        _monAnService = monAnService;
        _tourService = tourService;
        _geofenceEngine = geofenceEngine;
        _sqliteService = sqliteService;
        _loc = localizationService;
        _audioCacheService = audioCacheService;
        // DEMO_RESET_CACHE_BUTTON — xóa dòng này khi gỡ nút.
        _appDataCacheService = appDataCacheService;
        _selectedLanguageCode = localizationService.CurrentLanguage;

        Title = "";
        BackgroundColor = Colors.White;
        SafeAreaEdges = SafeAreaEdges.None;

        Microsoft.Maui.Controls.NavigationPage.SetHasNavigationBar(this, false);

        _map = CreateMap();
        _map.PropertyChanged += OnMapPropertyChanged;

        _titleLabel = new Label
        {
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#111111")
        };

        _subtitleLabel = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#6B7280")
        };

        _poiList = new VerticalStackLayout
        {
            Spacing = 12,
            Padding = new Thickness(16, 0, 16, 24)
        };

        _poiScroll = new ScrollView
        {
            Content = _poiList
        };
        _poiRefreshView = new AppRefreshView(
            _poiScroll,
            async () => await LoadRealPoisAsync(forceRefresh: true));

        _bottomSheet = CreateBottomSheet();
        _detailSheet = CreateDetailSheet();
        _currentLocationButton = CreateCurrentLocationButton();
        _refreshButton = CreateRefreshButton();
        // DEMO_RESET_CACHE_BUTTON — xóa dòng này khi gỡ nút.
        _resetCacheButton = CreateResetCacheButton();
        _mapModeLabel = CreateMapModeLabel();
        _mapModeButton = CreateMapModeButton();
        _visualTestButton = CreateVisualTestButton();
        _visualTestStatusBanner = CreateVisualTestStatusBanner();
        _tourProgressStatusLabel = new Label
        {
            Text = _loc.Get("tour_progress_running"),
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#C2410C")
        };
        _tourProgressTitleLabel = new Label
        {
            Text = _loc.Get("tour_page_title"),
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#0F172A"),
            MaxLines = 1,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        _tourProgressSubtitleLabel = new Label
        {
            Text = string.Empty,
            FontSize = 10,
            TextColor = Color.FromArgb("#64748B"),
            MaxLines = 1,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        _tourProgressBar = new ProgressBar
        {
            Progress = 0,
            ProgressColor = Color.FromArgb("#DC2626"),
            BackgroundColor = Color.FromArgb("#FEE2E2"),
            HeightRequest = 3
        };
        _tourProgressBanner = CreateTourProgressBanner();
        _footer = new AppBottomBar(
            BottomBarTab.Explore,
            localizationService,
            onHomeTap: async () =>
            {
                if (Application.Current is App app)
                    await app.ShowMainPageAsync();
            },
            onExploreTap: ToggleSuggestionSheetAsync,
            onTourTap: async () =>
            {
                if (Application.Current is App app)
                    await app.ShowTourPageAsync();
            },
            onSettingsTap: async () =>
            {
                if (Application.Current is App app)
                    await app.ShowSettingsPageAsync();
            });
        _topBar = CreateTopBar();

        localizationService.LanguageChanged += () => MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateLocalizedText();
            UpdateMapModeButtonVisual();
            _selectedLanguageCode = _loc.CurrentLanguage;
            RenderLanguageOptions();
            _ = ApplySelectedLanguageToCurrentDetailAsync();
            if (_activeTourDetail is not null)
                _ = RefreshActiveTourTextAsync();
            if (_isInitialLoadCompleted)
                _ = LoadRealPoisAsync(forceRefresh: true);
        });
        UpdateLocalizedText();

        Content = BuildLayout();
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;

#if ANDROID
        _map.HandlerChanged += (_, __) => TryInitializeAndroidMap();
#endif

        Loaded += (_, __) =>
        {
            InitializeSheetPositions();

#if ANDROID
            TryInitializeAndroidMap();
            UpdateAndroidMapPadding();
#endif

            if (!_hasPrimedExploreInfrastructure)
            {
                _hasPrimedExploreInfrastructure = true;
                _ = PrimeExploreInfrastructureAsync();
            }

            if (_isInitialLoadStarted)
                return;

            _isInitialLoadStarted = true;
            _ = InitializePageAsync();
        };

        Appearing += async (_, __) =>
        {
            if (!_isDetailVisible)
                ClearSelectedPoiFocus();

            _ = ShowCurrentLocationMarkerAsync(centerOnUser: false);
            StartLiveLocationPolling();

            if (!_isPlaybackStateSubscribed)
            {
                _geofenceEngine.PlaybackStateChanged += OnPlaybackStateChanged;
                _isPlaybackStateSubscribed = true;
            }

            SyncDetailAudioUi(_geofenceEngine.PlaybackState);

            if (_isInitialLoadCompleted)
                await LoadRealPoisAsync();

            if (_isMap3DEnabled && !HasInternet())
                await SetMap3DModeAsync(false, animate: true);

            UpdateMapModeButtonVisual();

            await EnsureExploreSheetVisibleAsync();
        };
        SizeChanged += (_, __) => InitializeSheetPositions();
    }

    private async Task PrimeExploreInfrastructureAsync()
    {
        try
        {
            await Task.Yield();

            await InitializeGeofenceAsync();
            await _geofenceEngine.StartAsync();

            if (!_isInitialLoadCompleted)
                await ShowCurrentLocationMarkerAsync(centerOnUser: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PoiMapPage] PrimeExploreInfrastructureAsync error: {ex.Message}");
        }
    }

    private View BuildLayout()
    {
        var root = new Grid();

        root.Children.Add(_map);
        root.Children.Add(_topBar);
        root.Children.Add(_refreshButton);
        root.Children.Add(_visualTestButton);
        root.Children.Add(_visualTestStatusBanner);
        // DEMO_RESET_CACHE_BUTTON — xóa dòng này khi gỡ nút.
        root.Children.Add(_resetCacheButton);
        root.Children.Add(_bottomSheet);
        root.Children.Add(_detailSheet);
        root.Children.Add(_tourProgressBanner);
        root.Children.Add(_footer);
        root.Children.Add(new AudioPlaybackBanner(_geofenceEngine, _loc));

        return root;
    }

    private Border CreateTourProgressBanner()
    {
        var stopButton = new Border
        {
            StrokeThickness = 0,
            HeightRequest = 32,
            WidthRequest = 32,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            BackgroundColor = Colors.White,
            VerticalOptions = LayoutOptions.Center,
            Content = new Border
            {
                StrokeThickness = 0,
                BackgroundColor = Color.FromArgb("#EF4444"),
                StrokeShape = new RoundRectangle { CornerRadius = 4 },
                HeightRequest = 11,
                WidthRequest = 11,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            },
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.05f,
                Radius = 8,
                Offset = new Point(0, 2)
            }
        };

        var stopTap = new TapGestureRecognizer();
        stopTap.Tapped += async (_, __) => await StopActiveTourAsync();
        stopButton.GestureRecognizers.Add(stopTap);

        var textWrap = new VerticalStackLayout
        {
            Spacing = 3,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                _tourProgressStatusLabel,
                _tourProgressTitleLabel,
                _tourProgressSubtitleLabel,
                _tourProgressBar
            }
        };

        var content = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12,
            Children =
            {
                textWrap,
                stopButton
            }
        };
        Grid.SetColumn(stopButton, 1);

        var banner = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            BackgroundColor = Color.FromArgb("#FFF6EF"),
            Padding = new Thickness(12, 7),
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.08f,
                Radius = 14,
                Offset = new Point(0, 5)
            },
            Content = content,
            IsVisible = false,
            Opacity = 0,
            VerticalOptions = LayoutOptions.End,
            HorizontalOptions = LayoutOptions.Fill,
            Margin = new Thickness(16, 0, 16, TourProgressBannerBottomMargin),
            ZIndex = 39
        };

        return banner;
    }

    public void RequestStartTour(TourDetail tourDetail)
    {
        _activeTourDetail = tourDetail;
        _pendingTourCompletionExitStoreId = null;
        _isTourCompletionNotificationShowing = false;
#if ANDROID
        _hasPendingTourRender = true;
#endif

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await BeginActiveTourSafelyAsync();
        });
    }

    private async Task BeginActiveTourSafelyAsync()
    {
        try
        {
            await BeginActiveTourAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Tour] Start tour error: {ex}");
#if ANDROID
            _hasPendingTourRender = false;
#endif
            await DisplayAlertAsync(_loc.Get("tour_page_title"), _loc.Get("tour_detail_load_error"), _loc.Get("alert_ok"));
        }
    }

    private async Task BeginActiveTourAsync()
    {
        if (_activeTourDetail is null)
            return;

        _activeTourProgress = await _tourService.GetProgressAsync(_activeTourDetail.Tour.IdTour);
        await ApplyActiveTourGeofencePriorityAsync(resetInsideState: true);
        await _geofenceEngine.EvaluateNowAsync();

#if ANDROID
        if (_androidGoogleMap is null)
        {
            _hasPendingTourRender = true;
            return;
        }
#endif

#if ANDROID
        _hasPendingTourRender = false;
#endif
        var currentStop = TourService.ResolveCurrentStop(_activeTourDetail, _activeTourProgress);
        var nextStop = ResolveNextAvailableStop(_activeTourDetail, currentStop);

        _titleLabel.Text = _loc.Get("tour_page_title");
        _subtitleLabel.Text = nextStop is null
            ? _activeTourDetail.Tour.Ten
            : $"{currentStop?.TenGianHang ?? _loc.Get("tour_current_stop_fallback")} -> {nextStop.TenGianHang ?? _loc.Get("tour_next_stop_fallback")}";
        await ShowOrUpdateTourProgressBannerAsync(_activeTourDetail, currentStop, nextStop);

        ClearSelectedPoiFocus();
        await RenderActiveTourRouteAsync();
    }

    private async Task RefreshActiveTourTextAsync()
    {
        if (_activeTourDetail is null)
            return;

        var currentStop = TourService.ResolveCurrentStop(_activeTourDetail, _activeTourProgress);
        var nextStop = ResolveNextAvailableStop(_activeTourDetail, currentStop);

        _titleLabel.Text = _loc.Get("tour_page_title");
        _subtitleLabel.Text = nextStop is null
            ? _activeTourDetail.Tour.Ten
            : $"{currentStop?.TenGianHang ?? _loc.Get("tour_current_stop_fallback")} -> {nextStop.TenGianHang ?? _loc.Get("tour_next_stop_fallback")}";
        await ShowOrUpdateTourProgressBannerAsync(_activeTourDetail, currentStop, nextStop);
    }

    private async Task RenderActiveTourRouteAsync()
    {
        ClearTourRouteVisuals();

        if (_activeTourDetail is null)
            return;

        var stops = TourService.GetUsableStops(_activeTourDetail).ToList();
        if (stops.Count == 0)
            return;

        FitTourStops(stops);

        if (stops.Count == 1)
            return;

        var currentStop = TourService.ResolveCurrentStop(_activeTourDetail, _activeTourProgress) ?? stops[0];
        var currentIndex = Math.Max(0, stops.FindIndex(s => s.IdGianHang == currentStop.IdGianHang));

        for (var i = 0; i < stops.Count - 1; i++)
        {
            var from = stops[i];
            var to = stops[i + 1];
            var route = await _tourService.GetRouteAsync(from, to);
            var points = route.Points.Count > 1
                ? route.Points
                : new List<RoutePoint>
                {
                    new() { Lat = from.Lat!.Value, Lon = from.Lon!.Value },
                    new() { Lat = to.Lat!.Value, Lon = to.Lon!.Value }
                };

            var state = i < currentIndex
                ? TourSegmentState.Past
                : i == currentIndex
                    ? TourSegmentState.Active
                    : TourSegmentState.Future;

            DrawTourSegment(points, state);
        }
    }

    private void DrawTourSegment(IReadOnlyList<RoutePoint> points, TourSegmentState state)
    {
        if (points.Count < 2)
            return;

#if ANDROID
        if (_androidGoogleMap is not null)
        {
            DrawAndroidTourSegment(points, state);
            return;
        }
#endif

        DrawMauiTourSegment(points, state);
    }

    private void DrawMauiTourSegment(IReadOnlyList<RoutePoint> points, TourSegmentState state)
    {
        var line = new MauiPolyline
        {
            StrokeColor = state switch
            {
                TourSegmentState.Active => Color.FromArgb("#DC2626"),
                TourSegmentState.Future => Color.FromArgb("#F97316"),
                _ => Color.FromArgb("#94A3B8")
            },
            StrokeWidth = state == TourSegmentState.Active ? 8 : 4
        };

        foreach (var point in points)
            line.Geopath.Add(new Location(point.Lat, point.Lon));

        _map.MapElements.Add(line);
        _mauiTourLines.Add(line);
    }

    private void ClearTourRouteVisuals()
    {
        foreach (var line in _mauiTourLines)
            _map.MapElements.Remove(line);
        _mauiTourLines.Clear();

#if ANDROID
        foreach (var line in _androidTourLines)
            line.Remove();
        _androidTourLines.Clear();

        foreach (var marker in _androidTourArrows)
            marker.Remove();
        _androidTourArrows.Clear();
#endif
    }

    private async Task ShowOrUpdateTourProgressBannerAsync(TourDetail detail, TourStop? currentStop, TourStop? nextStop)
    {
        var stops = TourService.GetUsableStops(detail).ToList();
        var totalStops = Math.Max(1, stops.Count);
        var currentIndex = currentStop is null
            ? 0
            : Math.Max(0, stops.FindIndex(s => s.IdGianHang == currentStop.IdGianHang));
        if (currentIndex < 0)
            currentIndex = 0;

        var completedStops = nextStop is null ? totalStops : currentIndex + 1;
        var progress = Math.Clamp((double)completedStops / totalStops, 0, 1);

        _tourProgressStatusLabel.Text = string.Format(_loc.Get("tour_progress_status"), completedStops, totalStops);
        _tourProgressTitleLabel.Text = string.IsNullOrWhiteSpace(detail.Tour.Ten)
            ? $"Tour #{detail.Tour.IdTour}"
            : detail.Tour.Ten;
        _tourProgressSubtitleLabel.Text = nextStop is null
            ? _loc.Get("tour_completed")
            : $"{currentStop?.TenGianHang ?? _loc.Get("tour_start_fallback")} -> {nextStop.TenGianHang ?? _loc.Get("tour_next_stop_fallback")}";
        _tourProgressBar.Progress = progress;

        if (_tourProgressBanner.IsVisible)
        {
#if ANDROID
            UpdateAndroidMapPadding();
#endif
            return;
        }

        _tourProgressBanner.IsVisible = true;
        _tourProgressBanner.TranslationY = 16;
        _tourProgressBanner.Opacity = 0;
#if ANDROID
        UpdateAndroidMapPadding();
#endif

        await Task.WhenAll(
            _tourProgressBanner.TranslateToAsync(0, 0, 180, Easing.CubicOut),
            _tourProgressBanner.FadeToAsync(1, 180, Easing.CubicOut));
    }

    private async Task StopActiveTourAsync()
    {
        if (_activeTourDetail is null && !_tourProgressBanner.IsVisible)
            return;

        var shouldStop = await DisplayAlertAsync(
            _loc.Get("tour_stop_confirm_title"),
            _loc.Get("tour_stop_confirm_message"),
            _loc.Get("tour_stop_confirm_accept"),
            _loc.Get("tour_stop_confirm_cancel"));

        if (!shouldStop)
            return;

        await ClearActiveTourStateAsync(resetHeader: true);
    }

    private async Task MarkActiveTourCompletedPendingExitAsync(TourDetail completedTour, int finalStoreId)
    {
        if (_activeTourDetail?.Tour.IdTour != completedTour.Tour.IdTour)
            return;

        _pendingTourCompletionExitStoreId = finalStoreId;
        await ApplyActiveTourGeofencePriorityAsync();
        await RefreshActiveTourTextAsync();
        await RenderActiveTourRouteAsync();
    }

    private async Task FinishActiveTourAfterExitAsync(int exitedStoreId)
    {
        if (_pendingTourCompletionExitStoreId != exitedStoreId || _activeTourDetail is null || _isTourCompletionNotificationShowing)
            return;

        _isTourCompletionNotificationShowing = true;
        try
        {
            await DisplayAlertAsync(_loc.Get("tour_page_title"), _loc.Get("tour_completed"), _loc.Get("alert_ok"));
            await ClearActiveTourStateAsync(resetHeader: true);
        }
        finally
        {
            _isTourCompletionNotificationShowing = false;
        }
    }

    private async Task ClearActiveTourStateAsync(bool resetHeader)
    {
        _activeTourDetail = null;
        _activeTourProgress = null;
        _pendingTourCompletionExitStoreId = null;
        await _geofenceEngine.ClearPriorityBoostsAsync();
#if ANDROID
        _hasPendingTourRender = false;
#endif
        ClearTourRouteVisuals();
        if (resetHeader)
        {
            _titleLabel.Text = _loc.Get("map_title");
            _subtitleLabel.Text = _loc.Get("map_subtitle_default");
        }
        await HideTourProgressBannerAsync();
    }

    private async Task HideTourProgressBannerAsync()
    {
        if (!_tourProgressBanner.IsVisible)
            return;

        await Task.WhenAll(
            _tourProgressBanner.TranslateToAsync(0, 16, 140, Easing.CubicIn),
            _tourProgressBanner.FadeToAsync(0, 140, Easing.CubicIn));

        _tourProgressBanner.IsVisible = false;
        _tourProgressBanner.TranslationY = 0;
        _tourProgressBar.Progress = 0;
#if ANDROID
        UpdateAndroidMapPadding();
#endif
    }

    private static TourStop? ResolveNextAvailableStop(TourDetail detail, TourStop? currentStop)
    {
        return TourRules.ResolveNextAvailableStop(detail, currentStop);
    }

    private async Task ApplyActiveTourGeofencePriorityAsync(bool resetInsideState = false)
    {
        if (_activeTourDetail is null)
        {
            await _geofenceEngine.ClearPriorityBoostsAsync(resetInsideState);
            return;
        }

        var boosts = TourRules.BuildGeofencePriorityBoosts(_activeTourDetail, _activeTourProgress);
        await _geofenceEngine.SetPriorityBoostsAsync(boosts, resetInsideState);
    }

    private void FitTourStops(IReadOnlyList<TourStop> stops)
    {
        var validStops = stops
            .Where(s => s.Lat.HasValue && s.Lon.HasValue)
            .ToList();

        if (validStops.Count == 0)
            return;

        var minLat = validStops.Min(s => s.Lat!.Value);
        var maxLat = validStops.Max(s => s.Lat!.Value);
        var minLon = validStops.Min(s => s.Lon!.Value);
        var maxLon = validStops.Max(s => s.Lon!.Value);
        var center = new Location((minLat + maxLat) / 2, (minLon + maxLon) / 2);
        var radiusMeters = Math.Max(
            250,
            CalculateDistanceMeters(minLat, minLon, maxLat, maxLon) * 0.65);

        _map.MoveToRegion(MapSpan.FromCenterAndRadius(center, Distance.FromMeters(radiusMeters)));
        RestoreMapModeAfterRegionMove();
    }

    private static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6371000.0;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusMeters * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    private MapActionButton CreateCurrentLocationButton()
    {
        var button = new MapActionButton(BuildCurrentLocationIcon())
        {
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Center,
            ZIndex = 21
        };

        button.Clicked += async (_, __) => await CenterOnCurrentLocationAsync();

        return button;
    }

    private MapActionButton CreateRefreshButton()
    {
        var button = new MapActionButton(BuildRefreshIcon())
        {
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 72, 16, 0),
            ZIndex = 21
        };

        SemanticProperties.SetDescription(button, _loc.Get("map_refresh"));
        button.Clicked += async (_, __) => await RefreshMapAsync();

        return button;
    }

    private Label CreateMapModeLabel()
    {
        return new Label
        {
            Text = "3D",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#DC2626"),
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
    }

    private MapActionButton CreateMapModeButton()
    {
        var button = new MapActionButton(_mapModeLabel)
        {
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Center,
            ZIndex = 21
        };

        button.Clicked += async (_, __) => await ToggleMapModeAsync();
        return button;
    }

    private async Task CenterOnCurrentLocationAsync()
    {
        _currentLocationButton.SetBusy(true);

        try
        {
            // Center one-shot theo thao tác người dùng, không bật follow liên tục.
            await ShowCurrentLocationMarkerAsync(centerOnUser: true);
        }
        finally
        {
            _currentLocationButton.SetBusy(false);
        }
    }

    private async Task RefreshMapAsync()
    {
        _refreshButton.SetBusy(true);

        try
        {
            await LoadRealPoisAsync(forceRefresh: true);
        }
        finally
        {
            _refreshButton.SetBusy(false);
        }
    }

    #region DEMO_RESET_CACHE_BUTTON
    // Nút làm mới dữ liệu phục vụ demo: clear toàn bộ memory + SQLite cache rồi reload.
    // Khi không cần demo, xóa region này + 5 chỗ đánh dấu "DEMO_RESET_CACHE_BUTTON" ở phần khai báo/wire-up phía trên.

    private MapActionButton CreateResetCacheButton()
    {
        var button = new MapActionButton(BuildRefreshIcon())
        {
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(16, 72, 0, 0),
            ZIndex = 21
        };

        SemanticProperties.SetDescription(button, "Làm mới toàn bộ dữ liệu (demo)");
        button.Clicked += async (_, __) => await ResetAllCacheAsync();
        return button;
    }

    private async Task ResetAllCacheAsync()
    {
        _resetCacheButton.SetBusy(true);

        try
        {
            await _appDataCacheService.ClearAsync();
            AudioCacheService.ClearMemoryCache();
            // Các tầng cache static khác mà AppDataCacheService không quản lý:
            ClearAllMediaCaches();             // _imageBytesCache, _audioBytesCache, marker-images dir
            HomePage.ClearImageCache();        // cache ảnh ở HomePage
            _pinsByPoiId.Clear();              // pin đã build từ marker file cũ

            await LoadRealPoisAsync(forceRefresh: true);

            await DisplayAlertAsync(
                _loc.Get("alert_notice"),
                "Đã xoá cache và tải lại dữ liệu mới nhất.",
                _loc.Get("alert_ok"));
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(_loc.Get("alert_error"), ex.Message, _loc.Get("alert_ok"));
        }
        finally
        {
            _resetCacheButton.SetBusy(false);
        }
    }
    #endregion

    private Grid CreateTopBar()
    {
        _searchEntry = new Entry
        {
            Placeholder = _loc.Get("search_placeholder"),
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#111827"),
            PlaceholderColor = Color.FromArgb("#A16207"),
            FontSize = 15,
            ClearButtonVisibility = ClearButtonVisibility.WhileEditing,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center
        };
        _searchEntry.TextChanged += OnSearchTextChanged;
        _searchEntry.Completed += OnSearchCompleted;

        var searchIcon = BuildSearchIcon();

        var searchGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 10,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Fill
        };

        searchGrid.Children.Add(searchIcon);
        Grid.SetColumn(searchIcon, 0);

        searchGrid.Children.Add(_searchEntry);
        Grid.SetColumn(_searchEntry, 1);

        var searchBox = new Border
        {
            StrokeThickness = 1,
            Stroke = new SolidColorBrush(Color.FromArgb("#FED7AA")),
            BackgroundColor = Color.FromArgb("#FFFFFB"),
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Padding = new Thickness(15, 5),
            HeightRequest = 54,
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.11f,
                Radius = 18,
                Offset = new Point(0, 7)
            },
            Content = searchGrid,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center
        };

        var topBar = new Grid
        {
            Padding = new Thickness(16, 10, 16, 0),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8,
            VerticalOptions = LayoutOptions.Start,
            HorizontalOptions = LayoutOptions.Fill,
            ZIndex = 20
        };

        topBar.Children.Add(searchBox);
        Grid.SetColumn(searchBox, 0);

        topBar.Children.Add(_mapModeButton);
        Grid.SetColumn(_mapModeButton, 1);

        topBar.Children.Add(_currentLocationButton);
        Grid.SetColumn(_currentLocationButton, 2);

        return topBar;
    }

    private Map CreateMap()
    {
        var center = new Location(10.762622, 106.660172);

        return new Map(MapSpan.FromCenterAndRadius(center, Distance.FromKilometers(1)))
        {
            MapType = MapType.Street,
            IsShowingUser = false,
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill,
            BackgroundColor = Colors.Transparent
        };
    }

    private void OnMapPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Map.VisibleRegion))
            ScheduleVisiblePinRefresh();
    }

    private void ScheduleVisiblePinRefresh()
    {
        _pinRefreshCts?.Cancel();
        _pinRefreshCts?.Dispose();
        _pinRefreshCts = new CancellationTokenSource();
        var ct = _pinRefreshCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(120, ct);
                if (ct.IsCancellationRequested)
                    return;

                await MainThread.InvokeOnMainThreadAsync(RefreshVisiblePins);
            }
            catch (OperationCanceledException)
            {
            }
        }, ct);
    }

    private void RefreshVisiblePins()
    {
        if (_pinsByPoiId.Count == 0)
            return;

        var visiblePoiIds = GetVisiblePoiIds();
        var desiredPins = visiblePoiIds
            .Select(id => _pinsByPoiId.TryGetValue(id, out var pin) ? pin : null)
            .Where(pin => pin is not null)
            .Cast<StyledPin>()
            .ToHashSet();

        var currentStyledPins = _map.Pins.OfType<StyledPin>().ToList();
        foreach (var pin in currentStyledPins)
        {
            if (!desiredPins.Contains(pin))
                _map.Pins.Remove(pin);
        }

        foreach (var pin in desiredPins)
        {
            if (!_map.Pins.Contains(pin))
                _map.Pins.Add(pin);
        }
    }

    private HashSet<int> GetVisiblePoiIds()
    {
        if (_selectedPoiId is int selectedPoiId)
        {
            if (_pinsByPoiId.ContainsKey(selectedPoiId))
                return new HashSet<int> { selectedPoiId };

            _selectedPoiId = null;
        }

        var result = new HashSet<int>();
        var sourcePois = _allPois.Count > 0 ? _allPois : _pois;
        var region = _map.VisibleRegion;

        if (region is null)
        {
            foreach (var poi in sourcePois.Take(40))
                result.Add(poi.IDChiNhanh);

            return result;
        }

        var centerLat = region.Center.Latitude;
        var centerLon = region.Center.Longitude;
        var latRadius = region.LatitudeDegrees / 2d;
        var lonRadius = region.LongitudeDegrees / 2d;
        const double overscanFactor = 1.35;

        foreach (var poi in sourcePois)
        {
            if (Math.Abs(poi.Latitude - centerLat) <= latRadius * overscanFactor &&
                Math.Abs(poi.Longitude - centerLon) <= lonRadius * overscanFactor)
            {
                result.Add(poi.IDChiNhanh);
            }
        }

        if (result.Count == 0)
        {
            foreach (var poi in sourcePois.Take(25))
                result.Add(poi.IDChiNhanh);
        }

        return result;
    }

    private bool HasInternet()
    {
        return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
    }

    private async Task ToggleMapModeAsync()
    {
        if (_isMap3DEnabled)
        {
            await SetMap3DModeAsync(false, animate: true);
            return;
        }

        if (!HasInternet())
        {
            UpdateMapModeButtonVisual();
            await DisplayAlertAsync(_loc.Get("alert_notice"), _loc.Get("map_3d_requires_internet"), _loc.Get("alert_ok"));
            return;
        }

#if !ANDROID
        await DisplayAlertAsync(_loc.Get("alert_notice"), _loc.Get("map_3d_android_only"), _loc.Get("alert_ok"));
        return;
#else
        await SetMap3DModeAsync(true, animate: true);
#endif
    }

    private async Task SetMap3DModeAsync(bool enabled, bool animate)
    {
        if (enabled && !HasInternet())
        {
            UpdateMapModeButtonVisual();
            await DisplayAlertAsync(_loc.Get("alert_notice"), _loc.Get("map_3d_requires_internet"), _loc.Get("alert_ok"));
            return;
        }

        _isMap3DEnabled = enabled;
#if ANDROID
        _mapModeRevision++;
#endif
        UpdateMapModeButtonVisual();

#if ANDROID
        ApplyAndroidMapMode(enabled, animate);
        if (enabled && _activeTourDetail is not null)
        {
            var activeStops = TourService.GetUsableStops(_activeTourDetail).ToList();
            if (activeStops.Count > 0)
                FitTourStops(activeStops);
        }
#endif

        await Task.CompletedTask;
    }

    private void UpdateMapModeButtonVisual()
    {
        if (_mapModeLabel is null)
            return;

        _mapModeLabel.Text = _isMap3DEnabled ? "2D" : "3D";
        _mapModeLabel.TextColor = _isMap3DEnabled
            ? Color.FromArgb("#9A3412")
            : Color.FromArgb("#DC2626");
        _mapModeLabel.Opacity = !_isMap3DEnabled && !HasInternet() ? 0.48 : 1;

        SemanticProperties.SetDescription(
            _mapModeButton,
            _isMap3DEnabled ? _loc.Get("map_switch_to_2d") : _loc.Get("map_switch_to_3d"));
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (_isMap3DEnabled && e.NetworkAccess != NetworkAccess.Internet)
            {
                await SetMap3DModeAsync(false, animate: true);
                return;
            }

            UpdateMapModeButtonVisual();
        });
    }

    private void RestoreMapModeAfterRegionMove()
    {
#if ANDROID
        if (!_isMap3DEnabled)
            return;

        var revision = _mapModeRevision;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(260);
            if (_isMap3DEnabled && revision == _mapModeRevision)
                ApplyAndroidMapMode(enabled: true, animate: true);
        });
#endif
    }

    private async Task InitializePageAsync()
    {
        try
        {
            await EnsureExploreSheetVisibleAsync();
            await LoadLanguagesAsync();
            await LoadRealPoisAsync();
            _ = DiagnosePoiImagesAsync();
            _isInitialLoadCompleted = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PoiMapPage] Initial load error: {ex.Message}");
        }
    }

    private async Task LoadRealPoisAsync(bool forceRefresh = false)
    {
        if (_isLoadingPois)
            return;

        if (!forceRefresh &&
            _allPois.Count > 0 &&
            DateTime.UtcNow - _lastPoiLoadAtUtc < PoiReloadInterval)
        {
            ApplySmartSearch(_searchEntry.Text, revealResults: !string.IsNullOrWhiteSpace(_searchEntry.Text), preserveSelectedPoi: true);
            RefreshVisiblePins();
            return;
        }

        _isLoadingPois = true;

        try
        {
            _pois.Clear();
            _allPois.Clear();
            _poiList.Children.Clear();
            _map.Pins.Clear();
            _pinsByPoiId.Clear();

            var data = await _poiService.GetAllPoisAsync(forceRefresh);

            System.Diagnostics.Debug.WriteLine($"[PoiMapPage] Loaded {data.Count} POIs from database");

            foreach (var poi in data)
            {
                System.Diagnostics.Debug.WriteLine($"[PoiMapPage] POI: {poi.Title}, ImagePath: '{poi.ImagePath}'");

                _allPois.Add(poi);
            }

            ApplySmartSearch(
                _searchEntry.Text,
                revealResults: !string.IsNullOrWhiteSpace(_searchEntry.Text));

            if (_pois.Count > 0)
            {
                var first = _pois[0];
                _map.MoveToRegion(
                    MapSpan.FromCenterAndRadius(
                        new Location(first.Latitude, first.Longitude),
                        Distance.FromKilometers(1)));
                RestoreMapModeAfterRegionMove();
            }

            RefreshVisiblePins();
            await BuildStyledPinsAsync(data);
            RefreshVisiblePins();
            _lastPoiLoadAtUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(_loc.Get("alert_error"), string.Format(_loc.Get("poi_load_error"), ex.Message), _loc.Get("alert_ok"));
        }
        finally
        {
            _isLoadingPois = false;

            if (_isInitialLoadCompleted)
                await ShowCurrentLocationMarkerAsync(centerOnUser: false);
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplySmartSearch(
            e.NewTextValue,
            revealResults: !string.IsNullOrWhiteSpace(e.NewTextValue));
    }

    private void OnSearchCompleted(object? sender, EventArgs e)
    {
        ApplySmartSearch(_searchEntry.Text, revealResults: true);
    }

    private void ApplySmartSearch(string? rawQuery, bool revealResults, bool preserveSelectedPoi = false)
    {
        var normalizedQuery = NormalizeSearchText(rawQuery);
        _activeSearchQuery = rawQuery?.Trim() ?? string.Empty;

        if (!preserveSelectedPoi)
        {
            _selectedPoiId = null;
        }
        else if (_selectedPoiId.HasValue && !_allPois.Any(x => x.IDChiNhanh == _selectedPoiId.Value))
        {
            _selectedPoiId = null;
        }

        _poiList.Children.Clear();
        _pois.Clear();

        IEnumerable<PoiItem> results;

        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            results = _allPois
                .OrderBy(GetDistanceToUserMeters)
                .ThenBy(x => x.Title)
                .ToList();
            ResetExploreHeader();
            UpdateHeaderByState();
        }
        else
        {
            var terms = normalizedQuery
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            results = _allPois
                .Select(poi => new
                {
                    Poi = poi,
                    Score = ScorePoiSearchMatch(poi, normalizedQuery, terms),
                    Distance = GetDistanceToUserMeters(poi)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Distance)
                .ThenBy(x => x.Poi.Title)
                .Select(x => x.Poi)
                .ToList();

            _titleLabel.Text = _loc.Get("search_title");
            _subtitleLabel.Text = results.Any()
                ? string.Format(_loc.Get("search_results"), results.Count(), _activeSearchQuery)
                : string.Format(_loc.Get("search_results_empty"), _activeSearchQuery);
        }

        foreach (var poi in results)
        {
            _pois.Add(poi);
            _poiList.Children.Add(CreatePoiCard(poi));
        }

        if (!string.IsNullOrWhiteSpace(normalizedQuery) && _pois.Count == 0)
        {
            _poiList.Children.Add(BuildEmptySearchState(_activeSearchQuery));
        }

        RefreshVisiblePins();

        if (!string.IsNullOrWhiteSpace(normalizedQuery) && revealResults)
        {
            _ = EnsureSearchResultsVisibleAsync();
        }
    }

    private View BuildEmptySearchState(string query)
    {
        return new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#FFF6EF"),
            StrokeShape = new RoundRectangle { CornerRadius = 22 },
            Padding = new Thickness(16),
            Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Label
                    {
                        Text = _loc.Get("search_no_results"),
                        FontSize = 16,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#0F172A")
                    },
                    new Label
                    {
                        Text = string.Format(_loc.Get("search_try_other"), query),
                        FontSize = 13,
                        TextColor = Color.FromArgb("#64748B")
                    }
                }
            }
        };
    }

    private int ScorePoiSearchMatch(PoiItem poi, string normalizedQuery, string[] terms)
    {
        var title = NormalizeSearchText(poi.Title);
        var address = NormalizeSearchText(poi.Address);
        var description = NormalizeSearchText(poi.Description);
        var searchText = NormalizeSearchText(poi.SearchText);
        var menuNames = poi.MenuNames
            .Select(NormalizeSearchText)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        var score = 0;

        if (title.StartsWith(normalizedQuery, StringComparison.Ordinal))
            score += 220;
        else if (title.Contains(normalizedQuery, StringComparison.Ordinal))
            score += 170;

        if (menuNames.Any(x => x.StartsWith(normalizedQuery, StringComparison.Ordinal)))
            score += 180;
        else if (menuNames.Any(x => x.Contains(normalizedQuery, StringComparison.Ordinal)))
            score += 140;

        if (description.Contains(normalizedQuery, StringComparison.Ordinal))
            score += 70;

        if (address.Contains(normalizedQuery, StringComparison.Ordinal))
            score += 55;

        foreach (var term in terms)
        {
            if (title.StartsWith(term, StringComparison.Ordinal))
                score += 45;
            else if (title.Contains(term, StringComparison.Ordinal))
                score += 28;

            if (menuNames.Any(x => x.StartsWith(term, StringComparison.Ordinal)))
                score += 34;
            else if (menuNames.Any(x => x.Contains(term, StringComparison.Ordinal)))
                score += 20;

            if (description.Contains(term, StringComparison.Ordinal))
                score += 10;

            if (address.Contains(term, StringComparison.Ordinal))
                score += 8;
        }

        if (terms.Length > 1 && terms.All(term => searchText.Contains(term, StringComparison.Ordinal)))
            score += 42;

        if (terms.All(term => title.Contains(term, StringComparison.Ordinal) || menuNames.Any(x => x.Contains(term, StringComparison.Ordinal))))
            score += 25;

        return score;
    }

    private double GetDistanceToUserMeters(PoiItem poi)
    {
        if (_userLocationPin?.Location is null)
            return double.MaxValue;

        return Location.CalculateDistance(
            _userLocationPin.Location,
            new Location(poi.Latitude, poi.Longitude),
            DistanceUnits.Kilometers) * 1000d;
    }

    private static string NormalizeSearchText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            builder.Append(c switch
            {
                'đ' => 'd',
                _ => c
            });
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }

    private Grid CreateBottomSheet()
    {
        var dragBar = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#D4D4D8"),
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
            HeightRequest = 5,
            WidthRequest = 48,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 10, 0, 10)
        };

        var dragArea = new Grid
        {
            HeightRequest = 34,
            Children = { dragBar }
        };

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnSheetPanUpdated;
        dragArea.GestureRecognizers.Add(pan);

        var nearBadge = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#FFE8D6"),
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
            Padding = new Thickness(12, 6),
            HorizontalOptions = LayoutOptions.Start
        };

        var header = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(18, 0, 18, 12),
            Children =
            {
                _titleLabel,
                _subtitleLabel,
                nearBadge
            }
        };

        var body = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            }
        };

        body.Children.Add(dragArea);

        body.Children.Add(header);
        Grid.SetRow(header, 1);

        body.Children.Add(_poiRefreshView);
        Grid.SetRow(_poiRefreshView, 2);

        var panel = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#FFF8F1"),
            StrokeShape = new RoundRectangle
            {
                CornerRadius = new CornerRadius(28, 28, 0, 0)
            },
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.12f,
                Radius = 18,
                Offset = new Point(0, -4)
            },
            Content = body,
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill
        };

        return new Grid
        {
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill,
            Children = { panel },
            IsVisible = false
        };
    }

    private Grid CreateDetailSheet()
    {
        _detailImage = new Image
        {
            Source = "dotnet_bot.png",
            Aspect = Aspect.AspectFill,
            HeightRequest = 250
        };

        var imageHeader = new Border
        {
            StrokeThickness = 0,
            HeightRequest = 250,
            StrokeShape = new RoundRectangle
            {
                CornerRadius = new CornerRadius(28, 28, 0, 0)
            },
            Content = _detailImage
        };

        _detailTitle = new Label
        {
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Black
        };

        _detailAddress = new Label
        {
            FontSize = 14,
            TextColor = Colors.Gray
        };

        _detailDescription = new Label
        {
            FontSize = 15,
            TextColor = Colors.Black,
            LineBreakMode = LineBreakMode.WordWrap
        };

        _detailAudioLabel = new Label
        {
            FontSize = 13,
            TextColor = Colors.Gray
        };

        _closeButton = new Button
        {
            BackgroundColor = Color.FromArgb("#E85D04"),
            TextColor = Colors.White,
            CornerRadius = 12,
            Padding = new Thickness(16, 10)
        };
        _closeButton.Clicked += async (_, __) => await HideDetailSheetAsync();

        var dragBar = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#D4D4D8"),
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
            HeightRequest = 5,
            WidthRequest = 48,
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 10, 0, 10)
        };

        var dragArea = new Grid
        {
            HeightRequest = 34,
            Children = { dragBar }
        };

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnDetailPanUpdated;
        dragArea.GestureRecognizers.Add(pan);

        var infoCard = new Border
        {
            Stroke = Color.FromArgb("#EEEEEE"),
            StrokeShape = new RoundRectangle { CornerRadius = 20 },
            BackgroundColor = Colors.White,
            Padding = 16,
            Margin = new Thickness(16, -20, 16, 0),
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    _detailTitle,
                    _detailAddress,
                    _detailDescription,
                    _detailAudioLabel
                }
            }
        };

        _languageRow = new HorizontalStackLayout
        {
            Spacing = 8
        };

        _languageSectionLabel = new Label
        {
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Black
        };

        var languageCard = new Border
        {
            Stroke = Color.FromArgb("#EEEEEE"),
            StrokeShape = new RoundRectangle { CornerRadius = 20 },
            BackgroundColor = Colors.White,
            Padding = 16,
            Margin = new Thickness(16, 0, 16, 0),
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    _languageSectionLabel,
                    new ScrollView
                    {
                        Orientation = ScrollOrientation.Horizontal,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
                        Content = _languageRow
                    }
                }
            }
        };

        _audioSectionLabel = new Label
        {
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Black
        };

        _playButton = new Button
        {
            BackgroundColor = Color.FromArgb("#E85D04"),
            TextColor = Colors.White,
            CornerRadius = 12,
            Padding = new Thickness(16, 10)
        };
        _playButton.Clicked += OnPlayButtonClicked;

        _progressSlider = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            Value = 0
        };
        _progressSlider.DragStarted += (_, __) => _isDraggingSlider = true;
        _progressSlider.DragCompleted += OnProgressDragCompleted;

        _currentTimeLabel = new Label
        {
            Text = "00:00",
            FontSize = 12,
            TextColor = Colors.Gray
        };

        _durationLabel = new Label
        {
            Text = "00:00",
            FontSize = 12,
            TextColor = Colors.Gray,
            HorizontalOptions = LayoutOptions.End
        };

        var timeGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };

        timeGrid.Children.Add(_currentTimeLabel);
        Grid.SetColumn(_currentTimeLabel, 0);

        timeGrid.Children.Add(_durationLabel);
        Grid.SetColumn(_durationLabel, 1);

        var audioCard = new Border
        {
            Stroke = Color.FromArgb("#EEEEEE"),
            StrokeShape = new RoundRectangle { CornerRadius = 20 },
            BackgroundColor = Colors.White,
            Padding = 16,
            Margin = new Thickness(16, 0, 16, 0),
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    _audioSectionLabel,
                    _playButton,
                    _progressSlider,
                    timeGrid
                }
            }
        };

        _foodImagesSectionLabel = new Label
        {
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Black,
            Margin = new Thickness(16, 0, 16, 0)
        };

        _foodImagesRow = new HorizontalStackLayout
        {
            Spacing = 12,
            Padding = new Thickness(16, 0, 16, 0),
            Children =
            {
                CreateFoodImageCard(null)
            }
        };

        var content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 14,
                Padding = new Thickness(0, 0, 0, 300),
                Children =
                {
                    imageHeader,
                    infoCard,
                    languageCard,
                    audioCard,
                    _foodImagesSectionLabel,
                    _foodImagesRow,
                    new VerticalStackLayout
                    {
                        Padding = new Thickness(16, 0, 16, 0),
                        Children = { _closeButton }
                    }
                }
            }
        };

        var body = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            }
        };

        body.Children.Add(dragArea);
        body.Children.Add(content);
        Grid.SetRow(content, 1);

        _detailPanel = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#FFF8F1"),
            StrokeShape = new RoundRectangle
            {
                CornerRadius = new CornerRadius(28, 28, 0, 0)
            },
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.14f,
                Radius = 18,
                Offset = new Point(0, -4)
            },
            Content = body,
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill
        };

        return new Grid
        {
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill,
            Children = { _detailPanel },
            IsVisible = false,
            InputTransparent = false
        };
    }

    private View CreateFoodImageCard(string? imagePath)
    {
        return new Border
        {
            Stroke = Color.FromArgb("#EEEEEE"),
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            HeightRequest = 100,
            WidthRequest = 140,
            Content = new Image
            {
                Source = BuildImageSource(imagePath),
                Aspect = Aspect.AspectFill
            }
        };
    }

    private View BuildSearchIcon()
    {
        var stroke = new SolidColorBrush(Color.FromArgb("#DC2626"));

        return new Grid
        {
            WidthRequest = 22,
            HeightRequest = 22,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Ellipse
                {
                    WidthRequest = 11,
                    HeightRequest = 11,
                    Stroke = stroke,
                    StrokeThickness = 1.8,
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Start,
                    TranslationX = 4,
                    TranslationY = 4
                },
                new Line
                {
                    X1 = 13.5,
                    Y1 = 13.5,
                    X2 = 18,
                    Y2 = 18,
                    Stroke = stroke,
                    StrokeThickness = 1.8
                }
            }
        };
    }

    private View BuildCurrentLocationIcon()
    {
        var stroke = new SolidColorBrush(Color.FromArgb("#DC2626"));
        var accent = new SolidColorBrush(Color.FromArgb("#FB7185"));

        return new Grid
        {
            WidthRequest = 24,
            HeightRequest = 24,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Ellipse
                {
                    WidthRequest = 14,
                    HeightRequest = 14,
                    Stroke = stroke,
                    StrokeThickness = 1.8,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                },
                new Ellipse
                {
                    WidthRequest = 5,
                    HeightRequest = 5,
                    Fill = accent,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                },
                new Line
                {
                    X1 = 12,
                    Y1 = 0,
                    X2 = 12,
                    Y2 = 5,
                    Stroke = stroke,
                    StrokeThickness = 1.8
                },
                new Line
                {
                    X1 = 12,
                    Y1 = 19,
                    X2 = 12,
                    Y2 = 24,
                    Stroke = stroke,
                    StrokeThickness = 1.8
                },
                new Line
                {
                    X1 = 0,
                    Y1 = 12,
                    X2 = 5,
                    Y2 = 12,
                    Stroke = stroke,
                    StrokeThickness = 1.8
                },
                new Line
                {
                    X1 = 19,
                    Y1 = 12,
                    X2 = 24,
                    Y2 = 12,
                    Stroke = stroke,
                    StrokeThickness = 1.8
                }
            }
        };
    }

    private View BuildRefreshIcon()
    {
        var stroke = new SolidColorBrush(Color.FromArgb("#DC2626"));

        return new Grid
        {
            WidthRequest = 24,
            HeightRequest = 24,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Ellipse
                {
                    WidthRequest = 16,
                    HeightRequest = 16,
                    Stroke = stroke,
                    StrokeThickness = 1.8,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                },
                new Line
                {
                    X1 = 17,
                    Y1 = 4,
                    X2 = 21,
                    Y2 = 4,
                    Stroke = stroke,
                    StrokeThickness = 1.8
                },
                new Line
                {
                    X1 = 21,
                    Y1 = 4,
                    X2 = 21,
                    Y2 = 8,
                    Stroke = stroke,
                    StrokeThickness = 1.8
                }
            }
        };
    }


    private async Task ToggleSuggestionSheetAsync()
    {
        if (!_isLayoutReady || _isAnimating)
            return;

        if (_isDetailVisible)
        {
            await HideDetailSheetAsync();
        }

        if (!_isExploreVisible)
        {
            ResetExploreHeader();
            _bottomSheet.IsVisible = true;
            _bottomSheet.TranslationY = _sheetHiddenY;
            _currentSheetY = _sheetHiddenY;
            _isExploreVisible = true;

            await SnapSheetToAsync(_sheetHalfY);
            return;
        }

        await HideSheetAsync();
    }

    private async Task HideSheetAsync()
    {
        if (!_isExploreVisible || _isAnimating)
            return;

        _isAnimating = true;

        try
        {
            await _bottomSheet.TranslateToAsync(0, _sheetHiddenY, 180, Easing.CubicIn);

            _currentSheetY = _sheetHiddenY;
            _bottomSheet.TranslationY = _sheetHiddenY;
            _bottomSheet.IsVisible = false;
            _isExploreVisible = false;
            ResetExploreHeader();

            UpdateScrollAvailability();
            UpdateHeaderByState();
#if ANDROID
            UpdateAndroidMapPadding();
#endif
        }
        finally
        {
            _isAnimating = false;
        }
    }

    private async Task ShowDetailSheetAsync(GianHang gianHang)
    {
        if (_isExploreVisible)
        {
            await HideSheetAsync();
        }

        _currentDetailGianHang = gianHang;
        SetDetailInfo(gianHang);

        ResetAudioState();

        _detailAudioLabel.Text = string.IsNullOrWhiteSpace(gianHang.AudioURL)
            ? _loc.Get("fallback_audio_none")
            : _loc.Get("fallback_audio_ready");

        _detailImage.Source = BuildImageSource(
            !string.IsNullOrWhiteSpace(gianHang.HinhAnhChinh)
                ? gianHang.HinhAnhChinh
                : gianHang.HinhAnh);

        await LoadLanguagesAsync(forceReload: true);
        RenderLanguageOptions();
        await ApplySelectedLanguageToCurrentDetailAsync();
        SyncDetailAudioUi(_geofenceEngine.PlaybackState);

        _detailSheet.IsVisible = true;
        _detailSheet.TranslationY = _detailHiddenY;
        _detailCurrentY = _detailHiddenY;
        _isDetailVisible = true;

        await SnapDetailSheetToAsync(_detailHalfY);
#if ANDROID
        UpdateAndroidMapPadding();
#endif
    }

    private async Task HideDetailSheetAsync()
    {
        if (!_isDetailVisible || _isDetailAnimating)
            return;

        _isDetailAnimating = true;

        try
        {
            ResetAudioState();

            await _detailSheet.TranslateToAsync(0, _detailHiddenY, 180, Easing.CubicIn);

            _detailCurrentY = _detailHiddenY;
            _detailSheet.TranslationY = _detailHiddenY;
            _detailSheet.IsVisible = false;
            _isDetailVisible = false;
            ClearSelectedPoiFocus();
#if ANDROID
            UpdateAndroidMapPadding();
#endif
        }
        finally
        {
            _isDetailAnimating = false;
        }
    }

    private void InitializeSheetPositions()
    {
        if (Width <= 0 || Height <= 0)
            return;

        _sheetFullY = Height * 0.12;
        _sheetHalfY = Height * 0.46;

        const double miniPeekHeight = 180;
        _sheetMiniY = Math.Max(_sheetHalfY + 40, Height - miniPeekHeight);

        _sheetHiddenY = Height + 20;

        _detailFullY = 0;
        _detailHalfY = Height * 0.22;
        _detailMiniY = Height * 0.58;
        _detailHiddenY = Height + 20;

        if (!_isLayoutReady)
        {
            _currentSheetY = _sheetHiddenY;
            _bottomSheet.TranslationY = _sheetHiddenY;
            _bottomSheet.IsVisible = false;
            _isExploreVisible = false;

            _detailCurrentY = _detailHiddenY;
            _detailSheet.TranslationY = _detailHiddenY;
            _detailSheet.IsVisible = false;
            _isDetailVisible = false;

            UpdateScrollAvailability();
            UpdateHeaderByState();
            _isLayoutReady = true;
        }
#if ANDROID
        UpdateAndroidMapPadding();
#endif
    }

    private void OnSheetPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (!_isLayoutReady || _isAnimating || !_isExploreVisible)
            return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panStartSheetY = _currentSheetY;
                _lastPanTotalY = 0;
                _lastPanDeltaY = 0;
                break;

            case GestureStatus.Running:
                _lastPanDeltaY = e.TotalY - _lastPanTotalY;
                _lastPanTotalY = e.TotalY;

                var nextY = _panStartSheetY + e.TotalY;
                nextY = Math.Max(_sheetFullY, Math.Min(_sheetHiddenY, nextY));

                _currentSheetY = nextY;
                _bottomSheet.TranslationY = _currentSheetY;

                UpdateScrollAvailability();
#if ANDROID
                UpdateAndroidMapPadding();
#endif
                break;

            case GestureStatus.Canceled:
            case GestureStatus.Completed:
                var target = ResolveSnapTargetWithDirection(_currentSheetY, _lastPanDeltaY);
                if (target >= _sheetHiddenY - 1)
                {
                    _ = HideSheetAsync();
                }
                else
                {
                    _ = SnapSheetToAsync(target);
                }
                break;
        }
    }

    private double ResolveSnapTargetWithDirection(double y, double deltaY)
    {
        var points = new[] { _sheetFullY, _sheetHalfY, _sheetMiniY }.OrderBy(p => p).ToArray();

        if (deltaY > 0 && y >= _sheetMiniY + 24)
            return _sheetHiddenY;

        if (Math.Abs(deltaY) < 1.2)
            return ResolveSnapTarget(y);

        if (deltaY < 0)
        {
            for (var i = points.Length - 1; i >= 0; i--)
            {
                if (points[i] < y)
                    return points[i];
            }

            return points[0];
        }

        for (var i = 0; i < points.Length; i++)
        {
            if (points[i] > y)
                return points[i];
        }

        return points[^1];
    }

    private double ResolveDetailSnapTargetWithDirection(double y, double deltaY)
    {
        var points = new[] { _detailFullY, _detailHalfY, _detailMiniY }.OrderBy(p => p).ToArray();

        if (y >= _detailMiniY + 8)
            return _detailHiddenY;

        if (deltaY > 0 && y >= _detailMiniY - 4)
            return _detailHiddenY;

        if (Math.Abs(deltaY) < 1.2)
            return ResolveDetailSnapTarget(y);

        if (deltaY < 0)
        {
            for (var i = points.Length - 1; i >= 0; i--)
            {
                if (points[i] < y)
                    return points[i];
            }

            return points[0];
        }

        for (var i = 0; i < points.Length; i++)
        {
            if (points[i] > y)
                return points[i];
        }

        return points[^1];
    }

    private double ResolveSnapTarget(double y)
    {
        var points = new[] { _sheetFullY, _sheetHalfY, _sheetMiniY };
        return points.OrderBy(p => Math.Abs(p - y)).First();
    }

    private double ResolveDetailSnapTarget(double y)
    {
        var points = new[] { _detailFullY, _detailHalfY, _detailMiniY };
        return points.OrderBy(p => Math.Abs(p - y)).First();
    }

    private void OnDetailPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (!_isLayoutReady || _isDetailAnimating || !_isDetailVisible)
            return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _detailPanStartY = _detailCurrentY;
                _detailLastPanTotalY = 0;
                _detailLastPanDeltaY = 0;
                break;

            case GestureStatus.Running:
                _detailLastPanDeltaY = e.TotalY - _detailLastPanTotalY;
                _detailLastPanTotalY = e.TotalY;

                var nextY = _detailPanStartY + e.TotalY;
                nextY = Math.Max(_detailFullY, Math.Min(_detailHiddenY, nextY));

                _detailCurrentY = nextY;
                _detailSheet.TranslationY = _detailCurrentY;
                UpdateDetailPanelShape(_detailLastPanDeltaY < -0.05);
#if ANDROID
                UpdateAndroidMapPadding();
#endif
                break;

            case GestureStatus.Canceled:
            case GestureStatus.Completed:
                var target = ResolveDetailSnapTargetWithDirection(_detailCurrentY, _detailLastPanDeltaY);
                if (target >= _detailHiddenY - 1)
                {
                    _ = HideDetailSheetAsync();
                }
                else if (target <= _detailFullY + 2)
                {
                    _ = OpenFoodGalleryFromDetailAsync();
                }
                else
                {
                    _ = SnapDetailSheetToAsync(target);
                }
                break;
        }
    }

    private async Task OpenFoodGalleryFromDetailAsync()
    {
        if (_isOpeningFoodGallery || _currentDetailGianHang is null)
        {
            await SnapDetailSheetToAsync(_detailFullY);
            return;
        }

        _isOpeningFoodGallery = true;

        try
        {
            await SnapDetailSheetToAsync(_detailFullY);
            UpdateDetailPanelShape(true);

            var page = new GianHangFoodGalleryPage(
                _currentDetailGianHang,
                _monAnService,
                _loc,
                NormalizeImagePath(_currentDetailGianHang.HinhAnhFullUrl),
                NormalizeLanguageCode(_selectedLanguageCode));

            var isMenuLayerOpened = false;
            var menuOpenedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnMenuLayerLoaded(object? sender, EventArgs args)
            {
                isMenuLayerOpened = true;
                menuOpenedTcs.TrySetResult(true);
            }

            page.Loaded += OnMenuLayerLoaded;

            try
            {
                await Navigation.PushAsync(page);

                if (!isMenuLayerOpened)
                    await menuOpenedTcs.Task;
            }
            finally
            {
                page.Loaded -= OnMenuLayerLoaded;
            }

            if (!isMenuLayerOpened)
                return;

            ResetAudioState();

            _detailCurrentY = _detailHiddenY;
            _detailSheet.TranslationY = _detailHiddenY;
            _detailSheet.Opacity = 1;
            _detailSheet.IsVisible = false;
            _detailSheet.InputTransparent = false;
            _isDetailVisible = false;
            ClearSelectedPoiFocus();
            UpdateDetailPanelShape();
        }
        finally
        {
            _isOpeningFoodGallery = false;
        }
    }

    private async Task SnapSheetToAsync(double targetY)
    {
        if (_isAnimating)
            return;

        _isAnimating = true;

        try
        {
            targetY = Math.Max(_sheetFullY, Math.Min(_sheetMiniY, targetY));

            await _bottomSheet.TranslateToAsync(0, targetY, 180, Easing.CubicOut);

            _currentSheetY = targetY;
            _bottomSheet.TranslationY = targetY;

            UpdateScrollAvailability();
            UpdateHeaderByState();
#if ANDROID
            UpdateAndroidMapPadding();
#endif
        }
        finally
        {
            _isAnimating = false;
        }
    }

    private async Task SnapDetailSheetToAsync(double targetY)
    {
        if (_isDetailAnimating)
            return;

        _isDetailAnimating = true;

        try
        {
            targetY = Math.Max(_detailFullY, Math.Min(_detailMiniY, targetY));

            await _detailSheet.TranslateToAsync(0, targetY, 180, Easing.CubicOut);

            _detailCurrentY = targetY;
            _detailSheet.TranslationY = targetY;
            UpdateDetailPanelShape();
#if ANDROID
            UpdateAndroidMapPadding();
#endif
        }
        finally
        {
            _isDetailAnimating = false;
        }
    }

#if ANDROID
    private void DrawAndroidTourSegment(IReadOnlyList<RoutePoint> points, TourSegmentState state)
    {
        if (_androidGoogleMap is null)
            return;

        var options = new PolylineOptions();
        foreach (var point in points)
            options.Add(new LatLng(point.Lat, point.Lon));

        var colorHex = state switch
        {
            TourSegmentState.Active => "#DC2626",
            TourSegmentState.Future => "#F97316",
            _ => "#94A3B8"
        };

        options.InvokeColor(AndroidColor.ParseColor(colorHex).ToArgb());
        options.InvokeWidth(state == TourSegmentState.Active ? 12f : 7f);
        options.InvokeZIndex(state == TourSegmentState.Active ? 50f : state == TourSegmentState.Future ? 30f : 10f);

        if (state == TourSegmentState.Future)
            options.InvokePattern(new List<PatternItem> { new Dash(28f), new Gap(18f) });

        var line = _androidGoogleMap.AddPolyline(options);
        _androidTourLines.Add(line);

        if (state == TourSegmentState.Active)
            AddAndroidTourArrow(points);
    }

    private void AddAndroidTourArrow(IReadOnlyList<RoutePoint> points)
    {
        if (_androidGoogleMap is null || points.Count < 2)
            return;

        try
        {
            var index = Math.Clamp(points.Count / 2, 1, points.Count - 1);
            var from = points[index - 1];
            var to = points[index];
            var icon = GetTourArrowIcon();
            if (icon is null)
                return;

            var markerOptions = new MarkerOptions()
                .SetPosition(new LatLng(to.Lat, to.Lon))
                .Anchor(0.5f, 0.5f)
                .Flat(true)
                .SetRotation(CalculateBearing(from, to))
                .SetIcon(icon);
            markerOptions.InvokeZIndex(60f);

            var marker = _androidGoogleMap.AddMarker(markerOptions);
            if (marker is not null)
                _androidTourArrows.Add(marker);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Tour] AddAndroidTourArrow error: {ex}");
        }
    }

    private static AndroidBitmap? _tourArrowBitmap;

    private static BitmapDescriptor? GetTourArrowIcon()
    {
        if (_tourArrowIcon is not null)
            return _tourArrowIcon;

        try
        {
            var bitmap = AndroidBitmap.CreateBitmap(72, 72, AndroidBitmapConfig.Argb8888!);
            using var canvas = new AndroidCanvas(bitmap);
            using var shadow = new AndroidPaint(AndroidPaintFlags.AntiAlias)
            {
                Color = AndroidColor.Argb(90, 15, 23, 42)
            };
            shadow.SetStyle(AndroidPaintStyle.Fill);
            shadow.SetShadowLayer(6f, 0f, 3f, AndroidColor.Argb(120, 15, 23, 42));

            using var paint = new AndroidPaint(AndroidPaintFlags.AntiAlias)
            {
                Color = AndroidColor.ParseColor("#DC2626")
            };
            paint.SetStyle(AndroidPaintStyle.Fill);

            using var path = new AndroidPath();
            path.MoveTo(36, 5);
            path.LineTo(59, 64);
            path.LineTo(36, 51);
            path.LineTo(13, 64);
            path.Close();
            canvas.DrawPath(path, shadow);
            canvas.DrawPath(path, paint);

            using var stroke = new AndroidPaint(AndroidPaintFlags.AntiAlias)
            {
                Color = AndroidColor.White,
                StrokeWidth = 5
            };
            stroke.SetStyle(AndroidPaintStyle.Stroke);
            canvas.DrawPath(path, stroke);

            _tourArrowBitmap = bitmap;
            _tourArrowIcon = BitmapDescriptorFactory.FromBitmap(bitmap);
            return _tourArrowIcon;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Tour] GetTourArrowIcon error: {ex}");
            return null;
        }
    }

    private static float CalculateBearing(RoutePoint from, RoutePoint to)
    {
        var lat1 = DegreesToRadians(from.Lat);
        var lat2 = DegreesToRadians(to.Lat);
        var dLon = DegreesToRadians(to.Lon - from.Lon);
        var y = Math.Sin(dLon) * Math.Cos(lat2);
        var x = Math.Cos(lat1) * Math.Sin(lat2) -
                Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
        return (float)((Math.Atan2(y, x) * 180.0 / Math.PI + 360.0) % 360.0);
    }

    private void TryInitializeAndroidMap()
    {
        if (_androidGoogleMap is not null)
            return;

        if (_map.Handler is not IMapHandler mapHandler || mapHandler.PlatformView is null)
            return;

        mapHandler.PlatformView.GetMapAsync(new MapReadyCallback(map =>
        {
            _androidGoogleMap = map;
            _androidGoogleMap.UiSettings.ZoomControlsEnabled = true;
            ApplyAndroidMapMode(_isMap3DEnabled, animate: false);
            UpdateAndroidMapPadding();
            if (_hasPendingTourRender)
                _ = BeginActiveTourSafelyAsync();
        }));
    }

    private void ApplyAndroidMapMode(bool enabled, bool animate)
    {
        if (_androidGoogleMap is null)
            return;

        var currentCamera = _androidGoogleMap.CameraPosition;
        var target = currentCamera.Target;
        var targetZoom = enabled
            ? GetAndroid3DTargetZoom(currentCamera.Zoom)
            : currentCamera.Zoom;
        var targetTilt = enabled ? 58f : 0f;
        var targetBearing = enabled
            ? Math.Abs(currentCamera.Bearing) < 1f ? 32f : currentCamera.Bearing
            : 0f;

        _androidGoogleMap.StopAnimation();
        _androidGoogleMap.BuildingsEnabled = enabled;
        _androidGoogleMap.UiSettings.TiltGesturesEnabled = enabled;
        _androidGoogleMap.UiSettings.RotateGesturesEnabled = enabled;

        var camera = new CameraPosition.Builder()
            .Target(target)
            .Zoom(targetZoom)
            .Tilt(targetTilt)
            .Bearing(targetBearing)
            .Build();

        var update = CameraUpdateFactory.NewCameraPosition(camera);
        if (animate && enabled)
            _androidGoogleMap.AnimateCamera(update);
        else
            _androidGoogleMap.MoveCamera(update);
    }

    private float GetAndroid3DTargetZoom(float currentZoom)
    {
        return _activeTourDetail is not null
            ? currentZoom
            : Math.Max(currentZoom, AndroidMap3DDefaultMinZoom);
    }

    private void UpdateAndroidMapPadding()
    {
        if (_androidGoogleMap is null || Height <= 0)
            return;

        var overlayTop = Height;

        if (_footer.IsVisible)
            overlayTop = Math.Min(overlayTop, Height - _footer.Height);

        if (_bottomSheet.IsVisible)
        {
            // Giữ vị trí zoom controls ổn định như trước: không trôi xuống đáy khi sheet hạ thấp.
            var fixedExploreTop = _sheetHalfY;
            overlayTop = Math.Min(overlayTop, fixedExploreTop);
        }

        if (_detailSheet.IsVisible)
            overlayTop = Math.Min(overlayTop, _detailCurrentY);

        if (_tourProgressBanner.IsVisible)
        {
            var bannerHeight = _tourProgressBanner.Height > 0
                ? _tourProgressBanner.Height
                : TourProgressBannerEstimatedHeight;
            var bannerTop = Height - TourProgressBannerBottomMargin - bannerHeight;
            overlayTop = Math.Min(overlayTop, bannerTop);
        }

        var bottomInsetDip = Math.Max(0, Height - overlayTop + 8);
        var bottomInsetPx = (int)Math.Ceiling(bottomInsetDip * DeviceDisplay.MainDisplayInfo.Density);

        _androidGoogleMap.SetPadding(0, 0, 0, bottomInsetPx);
    }

    private sealed class MapReadyCallback(Action<GoogleMap> onMapReady) : Java.Lang.Object, IOnMapReadyCallback
    {
        public void OnMapReady(GoogleMap googleMap) => onMapReady(googleMap);
    }
#endif

    public void RequestAutoOpenExplore()
    {
        _shouldAutoOpenExplore = true;
    }

    private async Task EnsureExploreSheetVisibleAsync()
    {
        if (!_shouldAutoOpenExplore || !_isLayoutReady || _isAnimating)
            return;

        if (_isDetailVisible)
        {
            await HideDetailSheetAsync();
        }

        if (!_isExploreVisible)
        {
            ResetExploreHeader();
            _bottomSheet.IsVisible = true;
            _bottomSheet.TranslationY = _sheetHiddenY;
            _currentSheetY = _sheetHiddenY;
            _isExploreVisible = true;
        }

        await SnapSheetToAsync(_sheetHalfY);
        _shouldAutoOpenExplore = false;
    }

    private async Task EnsureSearchResultsVisibleAsync()
    {
        if (!_isLayoutReady || _isAnimating)
            return;

        if (_isDetailVisible)
        {
            await HideDetailSheetAsync();
        }

        if (!_isExploreVisible)
        {
            _bottomSheet.IsVisible = true;
            _bottomSheet.TranslationY = _sheetHiddenY;
            _currentSheetY = _sheetHiddenY;
            _isExploreVisible = true;
        }

        var targetY = _currentSheetY <= _sheetHalfY + 10
            ? _currentSheetY
            : _sheetHalfY;

        await SnapSheetToAsync(targetY);
    }

    private void SetDetailInfo(GianHang gianHang)
    {
        _detailTitle.Text = string.IsNullOrWhiteSpace(gianHang.Ten) ? _loc.Get("fallback_name") : gianHang.Ten;
        _detailAddress.Text = string.IsNullOrWhiteSpace(gianHang.DiaChi) ? _loc.Get("fallback_address") : gianHang.DiaChi;
        _detailDescription.Text = string.IsNullOrWhiteSpace(gianHang.MoTa) ? _loc.Get("fallback_description") : gianHang.MoTa;
    }

    private async Task PopulateDetailFoodImagesAsync(GianHang gianHang, string? lang = null)
    {
        if (_foodImagesRow is null)
            return;

        var requestedLang = string.IsNullOrWhiteSpace(lang)
            ? NormalizeLanguageCode(_selectedLanguageCode)
            : NormalizeLanguageCode(lang);

        List<MonAn> items;
        try
        {
            items = await _monAnService.GetByGianHangAsync(gianHang.IdGianHang, requestedLang);
        }
        catch
        {
            items = new List<MonAn>();
        }

        if (items.Count == 0 && gianHang.MonAns.Count > 0)
            items = gianHang.MonAns;

        var visibleItems = items
            .Where(item => item is not null)
            .Where(item =>
                string.IsNullOrWhiteSpace(item.TinhTrang) ||
                string.Equals(item.TinhTrang, "con_ban", StringComparison.OrdinalIgnoreCase))
            .Take(6)
            .ToList();

        _foodImagesRow.Children.Clear();

        if (visibleItems.Count == 0)
        {
            _foodImagesRow.Children.Add(CreateFoodImageCard(null));
            return;
        }

        foreach (var item in visibleItems)
            _foodImagesRow.Children.Add(CreateFoodImageCard(item.HinhAnhFullUrl));
    }

    private void UpdateLocalizedText()
    {
        _titleLabel.Text = _loc.Get("map_title");
        _subtitleLabel.Text = _loc.Get("map_subtitle_default");
        _searchEntry.Placeholder = _loc.Get("search_placeholder");
        _closeButton.Text = _loc.Get("detail_close");
        _languageSectionLabel.Text = _loc.Get("detail_language");
        _audioSectionLabel.Text = _loc.Get("detail_audio_title");
        _foodImagesSectionLabel.Text = _loc.Get("detail_food_images");
        _playButton.Text = _loc.Get("btn_play");
        if (_currentDetailGianHang is null)
        {
            _detailAudioLabel.Text = _loc.Get("fallback_audio_none");
        }
        if (_userLocationPin is not null)
        {
            _userLocationPin.Label = _loc.Get("my_location");
        }
    }

    private void UpdateDetailPanelShape(bool forceSquare = false)
    {
        var topRadius = forceSquare || _detailCurrentY <= _detailFullY + 2 ? 0 : 28;
        _detailPanel.StrokeShape = new RoundRectangle
        {
            CornerRadius = new CornerRadius(topRadius, topRadius, 0, 0)
        };
    }

    private void UpdateScrollAvailability()
    {
        if (!_isExploreVisible)
        {
            _poiScroll.InputTransparent = true;
            return;
        }

        var enableScroll = _currentSheetY <= _sheetHalfY + 4;
        _poiScroll.InputTransparent = !enableScroll;
    }

    private void ResetExploreHeader()
    {
        if (!string.IsNullOrWhiteSpace(_activeSearchQuery))
            return;

        _titleLabel.Text = _loc.Get("map_title");
        _subtitleLabel.Text = _loc.Get("map_subtitle_default");
    }

    private void UpdateHeaderByState()
    {
        if (!string.IsNullOrWhiteSpace(_activeSearchQuery))
            return;

        if (!_isExploreVisible)
        {
            _subtitleLabel.Text = _loc.Get("map_subtitle_default");
            return;
        }

        if (_currentSheetY <= _sheetFullY + 10)
        {
            _subtitleLabel.Text = _loc.Get("map_subtitle_full");
        }
        else if (_currentSheetY <= _sheetHalfY + 10)
        {
            _subtitleLabel.Text = _loc.Get("map_subtitle_half");
        }
        else
        {
            _subtitleLabel.Text = _loc.Get("map_subtitle_default");
        }
    }

    private View CreatePoiCard(PoiItem poi)
    {
        var searchHint = GetSearchHintForPoi(poi);

        var image = new Image
        {
            Source = BuildImageSource(poi.ImagePath),
            HeightRequest = 76,
            WidthRequest = 76,
            Aspect = Aspect.AspectFill
        };

        var imageWrap = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            HeightRequest = 76,
            WidthRequest = 76,
            Content = image
        };

        var title = new Label
        {
            Text = poi.Title,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#111111"),
            LineBreakMode = LineBreakMode.TailTruncation
        };

        var subtitle = new Label
        {
            Text = poi.Subtitle,
            FontSize = 13,
            TextColor = Color.FromArgb("#6B7280"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        var tag = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#FFF1E6"),
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
            Padding = new Thickness(10, 4),
            HorizontalOptions = LayoutOptions.Start,
            Content = new Label
            {
                Text = searchHint,
                FontSize = 11,
                TextColor = Color.FromArgb("#9A3412")
            }
        };

        var textArea = new VerticalStackLayout
        {
            Spacing = 6,
            VerticalOptions = LayoutOptions.Center,
            Children = { title, subtitle, tag }
        };

        var content = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 12
        };

        content.Children.Add(imageWrap);
        content.Children.Add(textArea);
        Grid.SetColumn(textArea, 1);

        var card = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#F8EDE3"),
            StrokeShape = new RoundRectangle { CornerRadius = 24 },
            Padding = new Thickness(12),
            Content = content
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, __) => await OpenDetailAsync(poi);
        card.GestureRecognizers.Add(tap);

        return card;
    }

    private string GetSearchHintForPoi(PoiItem poi)
    {
        if (string.IsNullOrWhiteSpace(_activeSearchQuery))
            return _loc.Get("explore_view_detail");

        var normalizedQuery = NormalizeSearchText(_activeSearchQuery);
        var title = NormalizeSearchText(poi.Title);

        if (title.Contains(normalizedQuery, StringComparison.Ordinal))
            return _loc.Get("search_match_name");

        var matchedFoods = poi.MenuNames
            .Where(name => NormalizeSearchText(name).Contains(normalizedQuery, StringComparison.Ordinal))
            .Take(2)
            .ToArray();

        if (matchedFoods.Length > 0)
            return string.Format(_loc.Get("search_match_food"), string.Join(", ", matchedFoods));

        if (NormalizeSearchText(poi.Address).Contains(normalizedQuery, StringComparison.Ordinal))
            return _loc.Get("search_match_address");

        return _loc.Get("explore_view_detail");
    }

    private async Task OpenDetailAsync(PoiItem poi)
    {
        try
        {
            // forceRefresh -> bỏ qua appdata cache (12h) để mô tả/audio web vừa cập nhật xuất hiện ngay.
            // Offline thì AppDataCacheService tự fallback về memory/SQLite cache.
            var gianHang = await _gianHangService.GetByIdAsync(
                poi.IDChiNhanh, forceRefresh: true);

            if (gianHang == null)
            {
                await DisplayAlertAsync(_loc.Get("alert_notice"), _loc.Get("alert_poi_not_found"), _loc.Get("alert_ok"));
                return;
            }

            await FocusPoiAsync(poi);
            await ShowDetailSheetAsync(gianHang);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(_loc.Get("alert_error"), ex.Message, _loc.Get("alert_ok"));
        }
    }

    private async Task FocusPoiAsync(PoiItem poi)
    {
        // User chọn focus vào POI thì tạm ngừng follow vị trí hiện tại.
        _shouldFollowLiveLocation = false;

        _selectedPoiId = poi.IDChiNhanh;
        RefreshVisiblePins();

        var location = new Location(poi.Latitude, poi.Longitude);
        _map.MoveToRegion(MapSpan.FromCenterAndRadius(location, Distance.FromMeters(250)));
        RestoreMapModeAfterRegionMove();

        _titleLabel.Text = poi.Title;
        _subtitleLabel.Text = poi.Subtitle;

        await Task.CompletedTask;
    }

    private void ClearSelectedPoiFocus()
    {
        if (!_selectedPoiId.HasValue)
            return;

        _selectedPoiId = null;
        RefreshVisiblePins();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopLiveLocationPolling();
        _pinRefreshCts?.Cancel();
        _pinRefreshCts?.Dispose();
        _pinRefreshCts = null;
        _poiRefreshView.StopAutoRefresh();
        ResetAudioState();
        if (_isLiveLocationSubscribed)
        {
            _geofenceEngine.EnteredGeofence -= OnEnteredGeofence;
            _geofenceEngine.ExitedGeofence -= OnExitedGeofence;
            _geofenceEngine.LocationUpdated -= OnLiveLocationUpdated;
            _isLiveLocationSubscribed = false;
        }
        if (_isPlaybackStateSubscribed)
        {
            _geofenceEngine.PlaybackStateChanged -= OnPlaybackStateChanged;
            _isPlaybackStateSubscribed = false;
        }
    }
}

