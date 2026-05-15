using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using MauiApp1.Controls;
using MauiApp1.Models;
using MauiApp1.Services;
using MauiApp1.Utils;
using MauiApp1.Views.Maps;
using System.Globalization;
using System.Text;

namespace MauiApp1.Views;

public class HomePage : ContentPage
{
    private readonly GianHangService _gianHangService;
    private readonly GeofenceEngineService _geofenceEngine;
    private readonly LocalizationService _loc;
    private static HttpClient? _imageRenderHttpClient;
    private static readonly ConcurrentDictionary<string, byte[]> _imageBytesCache = new();

    // Demo helper: dùng cho nút Reset cache trên PoiMapPage.
    public static void ClearImageCache() => _imageBytesCache.Clear();
    private Location? _userLocation;
    private readonly VerticalStackLayout _nearbySection;
    private readonly Label _heroFollowLabel;
    private Label _headerTitleLabel = null!;
    private Label _headerLocationLabel = null!;
    private Label _heroBadgeLabel = null!;
    private Label _heroStreetLabel = null!;
    private Label _heroAccentLabel = null!;
    private Label _sectionNearbyLabel = null!;
    private Entry _homeSearchEntry = null!;
    private Border _heroBorder = null!;
    private readonly List<(GianHang restaurant, double distance, string imagePath)> _nearbyRestaurants = new();
    private int _followCount;
    private bool _hasLoadedNearby;
    private bool _isLoadingNearby;
    private bool _hasRequestedExplorePreload;
    private DateTime _lastNearbyLoadAtUtc;
    private readonly AppRefreshView _refreshView;
    private static readonly TimeSpan HomeRefreshInterval = TimeSpan.FromMinutes(2);
    private CancellationTokenSource? _locationPollingCts;
    private static readonly TimeSpan LocationPollInterval = TimeSpan.FromSeconds(5);
    private const double LocationChangeThresholdMeters = 10; // meters
    private DateTime _lastLocationTriggeredRefreshAtUtc = DateTime.MinValue;
    private static readonly TimeSpan LocationTriggeredRefreshCooldown = TimeSpan.FromSeconds(12);

    private enum WeatherCondition { Sunny, Dark, Rainy }
    private WeatherCondition _currentWeatherCondition = WeatherCondition.Sunny;

    public HomePage(GianHangService gianHangService, GeofenceEngineService geofenceEngine, LocalizationService localizationService)
    {
        _gianHangService = gianHangService;
        _geofenceEngine = geofenceEngine;
        _loc = localizationService;

        Title = string.Empty;
        BackgroundColor = Color.FromArgb("#FFF7F1");

        NavigationPage.SetHasNavigationBar(this, false);

        _nearbySection = new VerticalStackLayout { Spacing = 12 };
        _heroFollowLabel = new Label
        {
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#D6E4FF")
        };

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };

        var scroll = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 20,
                Padding = new Thickness(16, 18, 16, 28),
                Children =
                {
                    BuildMainHeader(),
                    BuildHeroCard(),
                    _nearbySection
                }
            }
        };

        _refreshView = new AppRefreshView(
            scroll,
            async () => await LoadNearbyRestaurants(forceRefresh: true));

        root.Children.Add(_refreshView);
        Grid.SetRow(_refreshView, 0);

        var footer = new AppBottomBar(
            BottomBarTab.Home,
            localizationService,
            onExploreTap: async () =>
            {
                if (Application.Current is App app)
                    await app.ShowExplorePageAsync(autoOpenExplore: true);
            },
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
        root.Children.Add(footer);
        Grid.SetRow(footer, 1);

        var audioBanner = new AudioPlaybackBanner(_geofenceEngine, localizationService);
        root.Children.Add(audioBanner);
        Grid.SetRowSpan(audioBanner, 2);

        Content = root;

        localizationService.LanguageChanged += OnLanguageChanged;
        UpdateLocalizedText();

        Appearing += async (_, __) =>
        {
            _refreshView.StartAutoRefresh(Dispatcher, HomeRefreshInterval);
            await LoadNearbyRestaurants();
            QueueExplorePreload();
            StartLocationPolling();
            DetectAndApplyWeatherTheme();
            UpdateHeroCardTheme();
        };

        Disappearing += (_, __) =>
        {
            _refreshView.StopAutoRefresh();
            StopLocationPolling();
        };
    }

    private void QueueExplorePreload()
    {
        if (_hasRequestedExplorePreload)
            return;

        _hasRequestedExplorePreload = true;

        _ = Task.Run(async () =>
        {
            await Task.Delay(350);
            if (Application.Current is App app)
                await app.PreloadExplorePageAsync();
        });
    }

    private void OnLanguageChanged()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            UpdateLocalizedText();
            await LoadNearbyRestaurants(forceRefresh: true);
        });
    }

    private void UpdateLocalizedText()
    {
        var theme = GetWeatherThemeColors();
        
        _headerTitleLabel.Text = _loc.Get("home_header_title");
        _headerLocationLabel.Text = _loc.Get("home_header_location");
        _heroBadgeLabel.Text = "● " + _loc.Get("hero_badge");
        _heroBadgeLabel.TextColor = theme.BadgeColor;
        _heroStreetLabel.Text = _loc.Get("hero_street");
        _heroStreetLabel.TextColor = theme.PrimaryTextColor;
        _heroAccentLabel.Text = _loc.Get("home_hero_accent");
        _heroAccentLabel.TextColor = theme.AccentColor;
        _heroFollowLabel.Text = string.Format(_loc.Get("hero_follow"), _followCount);
        _heroFollowLabel.TextColor = theme.BadgeColor;
        if (_sectionNearbyLabel is not null)
            _sectionNearbyLabel.Text = _loc.Get("section_nearby");
        if (_homeSearchEntry is not null)
            _homeSearchEntry.Placeholder = _loc.Get("search_placeholder");
    }

    private async Task LoadNearbyRestaurants(bool forceRefresh = false)
    {
        if (_isLoadingNearby)
            return;

        if (!forceRefresh &&
            _hasLoadedNearby &&
            DateTime.UtcNow - _lastNearbyLoadAtUtc < HomeRefreshInterval)
        {
            return;
        }

        _isLoadingNearby = true;

        try
        {
            var locationTask = GetUserLocation();

            var gianHangs = await _gianHangService.GetAllAsync(_loc.CurrentLanguage, forceRefresh);
            await locationTask;

            _nearbySection.Children.Clear();
            _sectionNearbyLabel = null!;
            _nearbySection.Children.Add(BuildSectionHeader(_loc.Get("section_nearby")));
            _nearbySection.Children.Add(BuildHomeSearchBox());

            if (gianHangs == null || gianHangs.Count == 0)
            {
                _nearbyRestaurants.Clear();
                _nearbySection.Children.Add(new Label
                {
                    Text = _loc.Get("no_data"),
                    FontSize = 14,
                    TextColor = Color.FromArgb("#64748B")
                });
                _hasLoadedNearby = true;
                _lastNearbyLoadAtUtc = DateTime.UtcNow;
                return;
            }

            var restaurantsWithDistance = new List<(GianHang restaurant, double distance, string imagePath)>();

            foreach (var gh in gianHangs)
            {
                if (gh.Lat == null || gh.Lon == null)
                    continue;

                var userLocation = _userLocation ?? new Location(10.762622, 106.660172);
                var distance = CalculateDistance(
                    userLocation.Latitude,
                    userLocation.Longitude,
                    gh.Lat.Value,
                    gh.Lon.Value);

                var imagePath = !string.IsNullOrWhiteSpace(gh.HinhAnhChinh)
                    ? gh.HinhAnhChinh
                    : gh.HinhAnh;

                restaurantsWithDistance.Add((gh, distance, imagePath ?? "dotnet_bot.png"));
            }

            var sorted = restaurantsWithDistance.OrderBy(r => r.distance).ToList();
            _nearbyRestaurants.Clear();
            _nearbyRestaurants.AddRange(sorted);

            _followCount = Math.Min(sorted.Count, 8);
            _heroFollowLabel.Text = string.Format(_loc.Get("hero_follow"), _followCount);
            await _geofenceEngine.UpdateTargetsAsync(gianHangs, radiusMeters: 10);
            await _geofenceEngine.StartAsync();

            RenderNearbyRestaurants();

            _hasLoadedNearby = true;
            _lastNearbyLoadAtUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomePage] Error loading nearby restaurants: {ex.Message}");
        }
        finally
        {
            _isLoadingNearby = false;
        }
    }

    private void RenderNearbyRestaurants()
    {
        if (_nearbySection.Children.Count > 2)
        {
            for (var i = _nearbySection.Children.Count - 1; i >= 2; i--)
                _nearbySection.Children.RemoveAt(i);
        }

        var normalizedQuery = NormalizeSearchText(_homeSearchEntry?.Text);
        var results = GetFilteredNearbyRestaurants(normalizedQuery).ToList();

        if (results.Count == 0)
        {
            _nearbySection.Children.Add(BuildEmptyHomeSearchState(_homeSearchEntry?.Text ?? string.Empty));
            return;
        }

        foreach (var (restaurant, distance, imagePath) in results.Take(string.IsNullOrWhiteSpace(normalizedQuery) ? 8 : 12))
        {
            var distanceText = distance < 1
                ? $"{distance * 1000:F0} m"
                : $"{distance:F1} km";

            _nearbySection.Children.Add(BuildNearbySpotRow(
                restaurant,
                string.IsNullOrWhiteSpace(restaurant.DiaChi)
                    ? $"{_loc.Get("home_nearby_prefix")} • {distanceText}"
                    : $"{restaurant.DiaChi} • {distanceText}",
                imagePath));
        }
    }

    private IEnumerable<(GianHang restaurant, double distance, string imagePath)> GetFilteredNearbyRestaurants(string normalizedQuery)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return _nearbyRestaurants.OrderBy(x => x.distance);

        var terms = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return _nearbyRestaurants
            .Select(item => new
            {
                Item = item,
                Score = ScoreRestaurantSearchMatch(item.restaurant, normalizedQuery, terms)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Item.distance)
            .ThenBy(x => x.Item.restaurant.Ten)
            .Select(x => x.Item);
    }

    private static int ScoreRestaurantSearchMatch(GianHang restaurant, string normalizedQuery, string[] terms)
    {
        var title = NormalizeSearchText(restaurant.Ten);
        var address = NormalizeSearchText(restaurant.DiaChi);
        var description = NormalizeSearchText(restaurant.MoTa);
        var menuNames = restaurant.MonAns
            .Where(item => !string.Equals(item.TinhTrang, "an", StringComparison.OrdinalIgnoreCase))
            .Select(item => NormalizeSearchText(item.Ten))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        var allText = NormalizeSearchText($"{restaurant.Ten} {restaurant.DiaChi} {restaurant.MoTa} {string.Join(" ", restaurant.MonAns.Select(x => $"{x.Ten} {x.MoTa}"))}");

        var score = 0;

        if (title.StartsWith(normalizedQuery, StringComparison.Ordinal))
            score += 220;
        else if (title.Contains(normalizedQuery, StringComparison.Ordinal))
            score += 170;

        if (menuNames.Any(name => name.StartsWith(normalizedQuery, StringComparison.Ordinal)))
            score += 180;
        else if (menuNames.Any(name => name.Contains(normalizedQuery, StringComparison.Ordinal)))
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

            if (menuNames.Any(name => name.StartsWith(term, StringComparison.Ordinal)))
                score += 34;
            else if (menuNames.Any(name => name.Contains(term, StringComparison.Ordinal)))
                score += 20;

            if (description.Contains(term, StringComparison.Ordinal))
                score += 10;

            if (address.Contains(term, StringComparison.Ordinal))
                score += 8;
        }

        if (terms.Length > 1 && terms.All(term => allText.Contains(term, StringComparison.Ordinal)))
            score += 42;

        if (terms.All(term => title.Contains(term, StringComparison.Ordinal) || menuNames.Any(name => name.Contains(term, StringComparison.Ordinal))))
            score += 25;

        return score;
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

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private async Task GetUserLocation()
    {
        try
        {
            var lastKnown = await Geolocation.Default.GetLastKnownLocationAsync();
            if (lastKnown is not null)
            {
                _userLocation = lastKnown;
                return;
            }

            var location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest
            {
                DesiredAccuracy = GeolocationAccuracy.Medium,
                Timeout = TimeSpan.FromSeconds(5)
            });

            _userLocation = location ?? new Location(10.762622, 106.660172);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomePage] Geolocation error: {ex.Message}");
            _userLocation = new Location(10.762622, 106.660172);
        }
    }

    private void UpdateNearbyDistancesAndRender(Location newLocation)
    {
        try
        {
            if (_nearbyRestaurants == null || _nearbyRestaurants.Count == 0)
                return;

            var updated = _nearbyRestaurants
                .Select(item => (
                    restaurant: item.restaurant,
                    distance: CalculateDistance(newLocation.Latitude, newLocation.Longitude, item.restaurant.Lat ?? newLocation.Latitude, item.restaurant.Lon ?? newLocation.Longitude),
                    imagePath: item.imagePath))
                .OrderBy(x => x.distance)
                .ToList();

            _nearbyRestaurants.Clear();
            _nearbyRestaurants.AddRange(updated);

            RenderNearbyRestaurants();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomePage] UpdateNearbyDistances error: {ex.Message}");
        }
    }

    private void StartLocationPolling()
    {
        if (_locationPollingCts != null)
            return;
        _locationPollingCts = new CancellationTokenSource();
        var ct = _locationPollingCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (permission != PermissionStatus.Granted)
                {
                    permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                    if (permission != PermissionStatus.Granted)
                    {
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            try
                            {
                                await DisplayAlertAsync(_loc.Get("alert_notice"), _loc.Get("alert_location_permission_required"), _loc.Get("alert_ok"));
                            }
                            catch { }
                        });
                        return;
                    }
                }

                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var loc = await Geolocation.Default.GetLocationAsync(new GeolocationRequest
                        {
                            DesiredAccuracy = GeolocationAccuracy.Medium,
                            Timeout = TimeSpan.FromSeconds(5)
                        });

                        if (loc is not null)
                        {
                            var prev = _userLocation;
                            var movedMeters = prev is null
                                ? double.MaxValue
                                : CalculateDistance(prev.Latitude, prev.Longitude, loc.Latitude, loc.Longitude) * 1000.0;

                            if (prev is null || movedMeters >= LocationChangeThresholdMeters)
                            {
                                _userLocation = loc;
                                MainThread.BeginInvokeOnMainThread(async () =>
                                {
                                    await RefreshNearbyOnLocationChangeAsync(loc);
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HomePage] location poll error: {ex.Message}");
                    }

                    try
                    {
                        await Task.Delay(LocationPollInterval, ct).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HomePage] StartLocationPolling error: {ex.Message}");
            }
        }, ct);
    }

    private void StopLocationPolling()
    {
        try
        {
            _locationPollingCts?.Cancel();
            _locationPollingCts?.Dispose();
        }
        catch { }
        finally
        {
            _locationPollingCts = null;
        }
    }

    private async Task RefreshNearbyOnLocationChangeAsync(Location location)
    {
        try
        {
            var now = DateTime.UtcNow;
            if (now - _lastLocationTriggeredRefreshAtUtc < LocationTriggeredRefreshCooldown)
            {
                UpdateNearbyDistancesAndRender(location);
                return;
            }

            _lastLocationTriggeredRefreshAtUtc = now;
            await LoadNearbyRestaurants(forceRefresh: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomePage] RefreshNearbyOnLocationChange error: {ex.Message}");
            UpdateNearbyDistancesAndRender(location);
        }
    }

    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return r * c;
    }

    private View BuildMainHeader()
    {
        _headerTitleLabel = new Label
        {
            Text = "Khu phố Vĩnh Khánh",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1F2937"),
            LineHeight = 1.1
        };

        _headerLocationLabel = new Label
        {
            Text = "Quận 4, TP HCM",
            FontSize = 13,
            TextColor = Color.FromArgb("#F97316"),
            VerticalTextAlignment = TextAlignment.Center
        };

        return new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                _headerTitleLabel,
                new Border
                {
                    StrokeThickness = 0,
                    BackgroundColor = Color.FromArgb("#FEE2E2"),
                    StrokeShape = new RoundRectangle { CornerRadius = 999 },
                    HorizontalOptions = LayoutOptions.Start,
                    Padding = new Thickness(12, 8),
                    Content = new HorizontalStackLayout
                    {
                        Spacing = 6,
                        VerticalOptions = LayoutOptions.Center,
                        Children =
                        {
                            new BoxView
                            {
                                WidthRequest = 8,
                                HeightRequest = 8,
                                CornerRadius = 4,
                                Color = Color.FromArgb("#EF4444"),
                                VerticalOptions = LayoutOptions.Center
                            },
                            _headerLocationLabel
                        }
                    }
                }
            }
        };
    }

    private View BuildHeroCard()
    {
        var theme = GetWeatherThemeColors();

        _heroBadgeLabel = new Label
        {
            Text = "● Bắt đầu khám phá",
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = theme.BadgeColor,
            VerticalTextAlignment = TextAlignment.Center
        };

        _heroStreetLabel = new Label
        {
            Text = "Phở Ấm Thực",
            FontSize = 32,
            FontAttributes = FontAttributes.Bold,
            TextColor = theme.PrimaryTextColor,
            LineHeight = 1.1
        };

        _heroAccentLabel = new Label
        {
            Text = "Vĩnh Khánh",
            FontSize = 32,
            FontAttributes = FontAttributes.Bold,
            TextColor = theme.AccentColor,
            LineHeight = 1.1
        };

        var content = new VerticalStackLayout
        {
            Spacing = 10,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Fill,
            Padding = new Thickness(20),
            Children =
            {
                _heroBadgeLabel,
                new VerticalStackLayout
                {
                    Spacing = 2,
                    Children =
                    {
                        _heroStreetLabel,
                        _heroAccentLabel
                    }
                },
                _heroFollowLabel
            }
        };

        // Decorative circles
        var bgGrid = new Grid { ColumnSpacing = 0, RowSpacing = 0 };
        bgGrid.Children.Add(new Border
        {
            StrokeThickness = 0,
            WidthRequest = 120,
            HeightRequest = 120,
            StrokeShape = new RoundRectangle { CornerRadius = 60 },
            BackgroundColor = theme.DecorColor1,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            TranslationX = 40,
            TranslationY = -30
        });

        bgGrid.Children.Add(new Border
        {
            StrokeThickness = 0,
            WidthRequest = 100,
            HeightRequest = 100,
            StrokeShape = new RoundRectangle { CornerRadius = 50 },
            BackgroundColor = theme.DecorColor2,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End,
            TranslationX = 50,
            TranslationY = 25
        });

        bgGrid.Children.Add(content);

        return _heroBorder = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 24 },
            HeightRequest = 220,
            Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(theme.GradientStart, 0f),
                    new GradientStop(theme.GradientEnd, 1f)
                },
                new Point(0, 0),
                new Point(1, 1)),
            Content = bgGrid,
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.15f,
                Radius = 20,
                Offset = new Point(0, 8)
            }
        };
    }

    private View BuildNearbySpotRow(GianHang restaurant, string subtitle, string imagePath)
    {
        var rowGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 14,
            VerticalOptions = LayoutOptions.Center,
            Padding = new Thickness(12)
        };

        // Left: Image thumbnail
        rowGrid.Children.Add(new Border
        {
            StrokeThickness = 0,
            HeightRequest = 64,
            WidthRequest = 64,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Content = new Image
            {
                Source = BuildImageSource(imagePath),
                Aspect = Aspect.AspectFill
            },
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.08f,
                Radius = 10,
                Offset = new Point(0, 3)
            }
        });

        // Middle: Text info
        var textWrap = new VerticalStackLayout
        {
            Spacing = 3,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = string.IsNullOrWhiteSpace(restaurant.Ten) ? _loc.Get("home_restaurant_fallback") : restaurant.Ten,
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#1F2937"),
                    MaxLines = 1,
                    LineBreakMode = LineBreakMode.TailTruncation
                },
                new Label
                {
                    Text = subtitle,
                    FontSize = 12,
                    TextColor = Color.FromArgb("#78716C"),
                    MaxLines = 1,
                    LineBreakMode = LineBreakMode.TailTruncation
                }
            }
        };
        rowGrid.Children.Add(textWrap);
        Grid.SetColumn(textWrap, 1);

        // Right: Play button
        var playButton = new Border
        {
            StrokeThickness = 0,
            HeightRequest = 44,
            WidthRequest = 44,
            StrokeShape = new RoundRectangle { CornerRadius = 22 },
            BackgroundColor = Color.FromArgb("#EF4444"),
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Content = BuildPlayIcon(),
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.1f,
                Radius = 12,
                Offset = new Point(0, 4)
            }
        };
        var playTap = new TapGestureRecognizer();
        playTap.Tapped += async (_, __) => await PlayStoreAudioAsync(restaurant);
        playButton.GestureRecognizers.Add(playTap);
        rowGrid.Children.Add(playButton);
        Grid.SetColumn(playButton, 2);

        return new Border
        {
            StrokeThickness = 1,
            Stroke = new SolidColorBrush(Color.FromArgb("#E8DCC4")),
            BackgroundColor = Colors.White,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Content = rowGrid,
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.04f,
                Radius = 8,
                Offset = new Point(0, 2)
            }
        };
    }

    private async Task PlayStoreAudioAsync(GianHang restaurant)
    {
        if (string.IsNullOrWhiteSpace(restaurant.AudioFullUrl))
        {
            await DisplayAlertAsync(_loc.Get("alert_notice"), _loc.Get("alert_no_audio"), _loc.Get("alert_ok"));
            return;
        }

        await _geofenceEngine.TogglePlaybackAsync(new AudioPlaybackRequest(
            restaurant.IdGianHang,
            string.IsNullOrWhiteSpace(restaurant.Ten) ? _loc.Get("home_restaurant_fallback") : restaurant.Ten,
            restaurant.AudioFullUrl,
            restaurant.HinhAnhFullUrl));
    }

    private static string NormalizeImagePath(string? dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
            return "dotnet_bot.png";

        dbPath = dbPath.Trim();

        if (dbPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            dbPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return BackendUrlResolver.BuildUrl(dbPath);
        }

        var normalizedPath = dbPath.Replace("\\", "/");
        if (!normalizedPath.Contains('/'))
            return normalizedPath;

        return BuildFullUrl(normalizedPath);
    }

    private static bool IsRemoteImageUrl(string value)
    {
        return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private static ImageSource BuildImageSource(string? dbPath)
    {
        var normalized = NormalizeImagePath(dbPath);

        if (!IsRemoteImageUrl(normalized))
            return normalized;

        return ImageSource.FromStream(async cancellationToken =>
        {
            try
            {
                if (_imageBytesCache.TryGetValue(normalized, out var cachedBytes))
                    return new MemoryStream(cachedBytes);

                _imageRenderHttpClient ??= CreateImageRenderHttpClient();

                using var response = await _imageRenderHttpClient.GetAsync(normalized, cancellationToken)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    return await FileSystem.OpenAppPackageFileAsync("dotnet_bot.png");

                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                _imageBytesCache[normalized] = bytes;
                return new MemoryStream(bytes);
            }
            catch
            {
                return await FileSystem.OpenAppPackageFileAsync("dotnet_bot.png");
            }
        });
    }

    private static string BuildFullUrl(string path)
    {
        return BackendUrlResolver.BuildUrl(path);
    }

    private static HttpClient CreateImageRenderHttpClient()
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

    private View BuildBottomPlayerStub()
    {
        var cover = new Border
        {
            StrokeThickness = 0,
            HeightRequest = 42,
            WidthRequest = 42,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = new Image
            {
                Source = "dotnet_bot.png",
                Aspect = Aspect.AspectFill
            }
        };

        var textWrap = new VerticalStackLayout
        {
            Spacing = 0,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "\u0110ang ph\u00E1t",
                    FontSize = 11,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#9A3412")
                },
                new Label
                {
                    Text = "X\u00F3m Chi\u1EBFu - A Century of Mat-Weaving",
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#0F172A"),
                    MaxLines = 1,
                    LineBreakMode = LineBreakMode.TailTruncation
                }
            }
        };

        var closeButton = new Border
        {
            StrokeThickness = 0,
            HeightRequest = 30,
            WidthRequest = 30,
            StrokeShape = new RoundRectangle { CornerRadius = 15 },
            BackgroundColor = Colors.White,
            VerticalOptions = LayoutOptions.Center,
            Content = BuildCloseIcon(),
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.05f,
                Radius = 8,
                Offset = new Point(0, 2)
            }
        };

        var content = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };
        content.Children.Add(cover);
        content.Children.Add(textWrap);
        content.Children.Add(closeButton);
        Grid.SetColumn(textWrap, 1);
        Grid.SetColumn(closeButton, 2);

        return new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            BackgroundColor = Color.FromArgb("#FFF6EF"),
            Padding = new Thickness(12, 10),
            Content = content,
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.04f,
                Radius = 12,
                Offset = new Point(0, 3)
            }
        };
    }

    private View BuildSectionHeader(string title)
    {
        _sectionNearbyLabel = new Label
        {
            Text = title,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1F2937"),
            Margin = new Thickness(0, 8, 0, 4)
        };

        return _sectionNearbyLabel;
    }

    private View BuildHomeSearchBox()
    {
        _homeSearchEntry = new Entry
        {
            Placeholder = _loc.Get("search_placeholder"),
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#1F2937"),
            PlaceholderColor = Color.FromArgb("#9CA3AF"),
            FontSize = 14,
            ClearButtonVisibility = ClearButtonVisibility.WhileEditing,
            HorizontalTextAlignment = TextAlignment.Start,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(0),
            HeightRequest = 28
        };
        _homeSearchEntry.TextChanged += (_, __) => RenderNearbyRestaurants();

        var searchGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                BuildHomeSearchIcon(),
                _homeSearchEntry
            }
        };
        Grid.SetColumn(_homeSearchEntry, 1);

        return new Border
        {
            StrokeThickness = 1,
            Stroke = new SolidColorBrush(Color.FromArgb("#E5D5C4")),
            BackgroundColor = Color.FromArgb("#FFFCF9"),
            StrokeShape = new RoundRectangle { CornerRadius = 28 },
            Padding = new Thickness(18, 10),
            HeightRequest = 56,
            Content = searchGrid,
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.05f,
                Radius = 10,
                Offset = new Point(0, 3)
            }
        };
    }

    private View BuildHomeSearchIcon()
    {
        var stroke = new SolidColorBrush(Color.FromArgb("#DC2626"));

        return new Grid
        {
            WidthRequest = 20,
            HeightRequest = 20,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Ellipse
                {
                    WidthRequest = 10,
                    HeightRequest = 10,
                    Stroke = stroke,
                    StrokeThickness = 1.8,
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Start,
                    TranslationX = 4,
                    TranslationY = 4
                },
                new Line
                {
                    X1 = 13,
                    Y1 = 13,
                    X2 = 17.5,
                    Y2 = 17.5,
                    Stroke = stroke,
                    StrokeThickness = 1.8
                }
            }
        };
    }

    private View BuildEmptyHomeSearchState(string query)
    {
        return new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#FFF6EF"),
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Padding = new Thickness(14),
            Content = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label
                    {
                        Text = "Khong tim thay ket qua",
                        FontSize = 15,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#0F172A")
                    },
                    new Label
                    {
                        Text = string.IsNullOrWhiteSpace(query)
                            ? "Thu tim theo ten quan, mon an hoac dia chi."
                            : $"Thu tu khoa khac thay cho \"{query}\".",
                        FontSize = 13,
                        TextColor = Color.FromArgb("#64748B")
                    }
                }
            }
        };
    }

    private View BuildBellIcon()
    {
        return new Grid
        {
            WidthRequest = 20,
            HeightRequest = 20,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Ellipse
                {
                    Stroke = new SolidColorBrush(Color.FromArgb("#0F172A")),
                    StrokeThickness = 1.6,
                    WidthRequest = 15,
                    HeightRequest = 15,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                },
                new Line
                {
                    X1 = 10,
                    X2 = 10,
                    Y1 = 4.6,
                    Y2 = 10.2,
                    Stroke = new SolidColorBrush(Color.FromArgb("#0F172A")),
                    StrokeThickness = 1.6,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                },
                new Ellipse
                {
                    WidthRequest = 3.4,
                    HeightRequest = 3.4,
                    Fill = new SolidColorBrush(Color.FromArgb("#0F172A")),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.End,
                    TranslationY = -1
                }
            }
        };
    }

    private View BuildPlayIcon()
    {
        return new Polygon
        {
            Points = new PointCollection
            {
                new Point(6, 4.5),
                new Point(6, 15.5),
                new Point(15, 10)
            },
            Fill = new SolidColorBrush(Colors.White),
            StrokeThickness = 0,
            WidthRequest = 18,
            HeightRequest = 18,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
    }

    private View BuildCloseIcon()
    {
        var stroke = new SolidColorBrush(Color.FromArgb("#64748B"));

        return new Grid
        {
            WidthRequest = 14,
            HeightRequest = 14,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Line
                {
                    X1 = 3,
                    Y1 = 3,
                    X2 = 11,
                    Y2 = 11,
                    Stroke = stroke,
                    StrokeThickness = 1.8
                },
                new Line
                {
                    X1 = 11,
                    Y1 = 3,
                    X2 = 3,
                    Y2 = 11,
                    Stroke = stroke,
                    StrokeThickness = 1.8
                }
            }
        };
    }

    private void DetectAndApplyWeatherTheme()
    {
        var hour = DateTime.Now.Hour;
        bool isRaining = Preferences.Get("weather_is_raining", false);

        if (isRaining)
        {
            _currentWeatherCondition = WeatherCondition.Rainy;
        }
        else if (hour >= 6 && hour < 18)
        {
            _currentWeatherCondition = WeatherCondition.Sunny;
        }
        else
        {
            _currentWeatherCondition = WeatherCondition.Dark;
        }
    }

    private void UpdateHeroCardTheme()
    {
        if (_heroBorder == null)
            return;

        var theme = GetWeatherThemeColors();
        _heroBorder.Background = new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop(theme.GradientStart, 0f),
                new GradientStop(theme.GradientEnd, 1f)
            },
            new Point(0, 0),
            new Point(1, 1));

        _heroBadgeLabel.TextColor = theme.BadgeColor;
        _heroStreetLabel.TextColor = theme.PrimaryTextColor;
        _heroAccentLabel.TextColor = theme.AccentColor;
        _heroFollowLabel.TextColor = theme.BadgeColor;
    }

    private (Color GradientStart, Color GradientEnd, Color PrimaryTextColor, Color AccentColor, Color BadgeColor, Color DecorColor1, Color DecorColor2) GetWeatherThemeColors()
    {
        return _currentWeatherCondition switch
        {
            WeatherCondition.Sunny => (
                GradientStart: Color.FromArgb("#FFF9E6"),
                GradientEnd: Color.FromArgb("#FFE4B3"),
                PrimaryTextColor: Color.FromArgb("#1F2937"),
                AccentColor: Color.FromArgb("#F97316"),
                BadgeColor: Color.FromArgb("#EA580C"),
                DecorColor1: Color.FromArgb("#FED7AA"),
                DecorColor2: Color.FromArgb("#FDBA74")
            ),
            WeatherCondition.Rainy => (
                GradientStart: Color.FromArgb("#1E293B"),
                GradientEnd: Color.FromArgb("#0F172A"),
                PrimaryTextColor: Color.FromArgb("#E2E8F0"),
                AccentColor: Color.FromArgb("#60A5FA"),
                BadgeColor: Color.FromArgb("#3B82F6"),
                DecorColor1: Color.FromArgb("#475569"),
                DecorColor2: Color.FromArgb("#334155")
            ),
            _ => (
                GradientStart: Color.FromArgb("#1F2937"),
                GradientEnd: Color.FromArgb("#111827"),
                PrimaryTextColor: Colors.White,
                AccentColor: Color.FromArgb("#FECACA"),
                BadgeColor: Color.FromArgb("#FECACA"),
                DecorColor1: Color.FromArgb("#1A2F5C"),
                DecorColor2: Color.FromArgb("#2D1F3A")
            )
        };
    }

}
