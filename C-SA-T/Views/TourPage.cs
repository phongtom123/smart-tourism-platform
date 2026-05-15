using MauiApp1.Controls;
using MauiApp1.Models;
using MauiApp1.Services;
using Microsoft.Maui.Controls.Shapes;

namespace MauiApp1.Views;

public class TourPage : ContentPage
{
    private readonly TourService _tourService;
    private readonly LocalizationService _loc;
    private readonly VerticalStackLayout _tourList;
    private readonly ActivityIndicator _loadingIndicator;
    private readonly Label _statusLabel;
    private readonly Label _pageTitleLabel;
    private readonly Label _pageSubtitleLabel;
    private readonly Button _refreshButton;
    private bool _isLoading;
    private bool _hasLoaded;

    public TourPage(TourService tourService, LocalizationService localizationService)
    {
        _tourService = tourService;
        _loc = localizationService;

        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Color.FromArgb("#FFF7F1");
        SafeAreaEdges = SafeAreaEdges.None;

        _tourList = new VerticalStackLayout
        {
            Spacing = 12
        };

        _loadingIndicator = new ActivityIndicator
        {
            Color = Color.FromArgb("#DC2626"),
            IsVisible = false
        };

        _statusLabel = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#6B7280"),
            HorizontalTextAlignment = TextAlignment.Center,
            IsVisible = false
        };

        _pageTitleLabel = new Label
        {
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#111111")
        };

        _pageSubtitleLabel = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#6B7280")
        };

        _refreshButton = new Button
        {
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#DC2626"),
            BackgroundColor = Color.FromArgb("#FFE8E2"),
            CornerRadius = 18,
            Padding = new Thickness(14, 8)
        };
        _refreshButton.Clicked += async (_, __) => await LoadToursAsync(forceRefresh: true);

        UpdateLocalizedText();
        localizationService.LanguageChanged += () => MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateLocalizedText();
            if (_hasLoaded)
                _ = LoadToursAsync(forceRefresh: true);
        });

        Content = BuildContent();

        Appearing += async (_, __) =>
        {
            if (!_hasLoaded)
                await LoadToursAsync();
        };
    }

    private View BuildContent()
    {
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
                Spacing = 18,
                Padding = new Thickness(16, 52, 16, 28),
                Children =
                {
                    new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Auto)
                        },
                        Children =
                        {
                            new VerticalStackLayout
                            {
                                Spacing = 4,
                                Children =
                                {
                                    _pageTitleLabel,
                                    _pageSubtitleLabel
                                }
                            },
                            _refreshButton
                        }
                    },
                    _loadingIndicator,
                    _statusLabel,
                    _tourList
                }
            }
        };

        Grid.SetColumn(_refreshButton, 1);

        root.Children.Add(scroll);

        var footer = new AppBottomBar(
            BottomBarTab.Tour,
            _loc,
            onHomeTap: async () =>
            {
                if (Application.Current is App app)
                    await app.ShowMainPageAsync();
            },
            onExploreTap: async () =>
            {
                if (Application.Current is App app)
                    await app.ShowExplorePageAsync(autoOpenExplore: true);
            },
            onSettingsTap: async () =>
            {
                if (Application.Current is App app)
                    await app.ShowSettingsPageAsync();
            });

        root.Children.Add(footer);
        Grid.SetRow(footer, 1);

        return root;
    }

    private void UpdateLocalizedText()
    {
        _pageTitleLabel.Text = _loc.Get("tour_page_title");
        _pageSubtitleLabel.Text = _loc.Get("tour_page_subtitle");
        _refreshButton.Text = _loc.Get("tour_refresh");
    }

    private async Task LoadToursAsync(bool forceRefresh = false)
    {
        if (_isLoading)
            return;

        _isLoading = true;
        _loadingIndicator.IsVisible = true;
        _loadingIndicator.IsRunning = true;
        _statusLabel.IsVisible = false;

        try
        {
            var tours = await _tourService.GetActiveToursAsync();
            _tourList.Children.Clear();

            if (tours.Count == 0)
            {
                ShowStatus(_loc.Get("tour_empty"));
                return;
            }

            foreach (var tour in tours)
                _tourList.Children.Add(BuildTourCard(tour));

            _hasLoaded = true;
        }
        catch (Exception ex)
        {
            ShowStatus(string.Format(_loc.Get("tour_list_load_error"), ex.Message));
        }
        finally
        {
            _loadingIndicator.IsRunning = false;
            _loadingIndicator.IsVisible = false;
            _isLoading = false;
        }
    }

    private View BuildTourCard(TourSummary tour)
    {
        var title = new Label
        {
            Text = string.IsNullOrWhiteSpace(tour.Ten) ? $"Tour #{tour.IdTour}" : tour.Ten,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#111111"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        var description = new Label
        {
            Text = string.IsNullOrWhiteSpace(tour.MoTa) ? _loc.Get("tour_default_description") : tour.MoTa,
            FontSize = 13,
            TextColor = Color.FromArgb("#6B7280"),
            LineBreakMode = LineBreakMode.WordWrap,
            MaxLines = 3
        };

        var meta = new Label
        {
            Text = BuildMetaText(tour),
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#DC2626")
        };

        var startButton = new Button
        {
            Text = _loc.Get("tour_start_button"),
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            BackgroundColor = Color.FromArgb("#DC2626"),
            CornerRadius = 20,
            Padding = new Thickness(16, 10)
        };

        startButton.Clicked += async (_, __) => await ConfirmStartTourAsync(tour);

        return new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Stroke = Color.FromArgb("#F0E6DC"),
            BackgroundColor = Colors.White,
            Padding = new Thickness(16, 14),
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    title,
                    description,
                    meta,
                    startButton
                }
            },
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.05f,
                Radius = 10,
                Offset = new Point(0, 4)
            }
        };
    }

    private async Task ConfirmStartTourAsync(TourSummary tour)
    {
        var tourName = string.IsNullOrWhiteSpace(tour.Ten) ? $"Tour #{tour.IdTour}" : tour.Ten;
        var confirm = await DisplayAlertAsync(
            _loc.Get("tour_start_confirm_title"),
            string.Format(_loc.Get("tour_start_confirm_message"), tourName),
            _loc.Get("tour_start_confirm_accept"),
            _loc.Get("tour_start_confirm_cancel"));

        if (!confirm)
            return;

        _loadingIndicator.IsVisible = true;
        _loadingIndicator.IsRunning = true;

        try
        {
            var detail = await _tourService.GetTourDetailAsync(tour.IdTour);
            if (detail is null)
            {
                await DisplayAlertAsync(_loc.Get("tour_page_title"), _loc.Get("tour_detail_load_error"), _loc.Get("alert_ok"));
                return;
            }

            if (TourService.GetUsableStops(detail).Count == 0)
            {
                await DisplayAlertAsync(_loc.Get("tour_page_title"), _loc.Get("tour_no_available_stops"), _loc.Get("alert_ok"));
                return;
            }

            if (Application.Current is App app)
                await app.StartTourAsync(detail);
        }
        finally
        {
            _loadingIndicator.IsRunning = false;
            _loadingIndicator.IsVisible = false;
        }
    }

    private void ShowStatus(string message)
    {
        _statusLabel.Text = message;
        _statusLabel.IsVisible = true;
    }

    private string BuildMetaText(TourSummary tour)
    {
        var parts = new List<string> { string.Format(_loc.Get("tour_meta_stops"), tour.SoStop) };
        if (tour.DoDaiPhutDeXuat.HasValue && tour.DoDaiPhutDeXuat.Value > 0)
            parts.Add(string.Format(_loc.Get("tour_meta_minutes"), tour.DoDaiPhutDeXuat.Value));
        if (!string.IsNullOrWhiteSpace(tour.DanhMuc))
            parts.Add(tour.DanhMuc);
        return string.Join("  |  ", parts);
    }
}
