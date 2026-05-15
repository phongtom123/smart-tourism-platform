using MauiApp1.Services;
using Microsoft.Maui.Controls.Shapes;

namespace MauiApp1.Controls;

public enum BottomBarTab
{
    Home,
    Explore,
    Tour,
    Settings
}

public sealed class AppBottomBar : ContentView
{
    private readonly Func<Task>? _onHomeTap;
    private readonly Func<Task>? _onExploreTap;
    private readonly Func<Task>? _onTourTap;
    private readonly Func<Task>? _onSettingsTap;
    private readonly LocalizationService _loc;
    private Label _homeLabel = null!;
    private Label _exploreLabel = null!;
    private Label _tourLabel = null!;
    private Label _settingsLabel = null!;

    public AppBottomBar(
        BottomBarTab activeTab,
        LocalizationService localizationService,
        Func<Task>? onHomeTap = null,
        Func<Task>? onExploreTap = null,
        Func<Task>? onTourTap = null,
        Func<Task>? onSettingsTap = null)
    {
        ActiveTab = activeTab;
        _loc = localizationService;
        _onHomeTap = onHomeTap;
        _onExploreTap = onExploreTap;
        _onTourTap = onTourTap;
        _onSettingsTap = onSettingsTap;

        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.End;

        Content = BuildRoot();

        localizationService.LanguageChanged += () => MainThread.BeginInvokeOnMainThread(UpdateLocalizedText);
    }

    public BottomBarTab ActiveTab { get; }

    private void UpdateLocalizedText()
    {
        _homeLabel.Text = _loc.Get("tab_home");
        _exploreLabel.Text = _loc.Get("tab_explore");
        _tourLabel.Text = _loc.Get("tab_tour");
        _settingsLabel.Text = _loc.Get("tab_settings");
    }

    private View BuildRoot()
    {
        var tabs = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            Padding = new Thickness(16, 8, 16, 10)
        };

        var home = BuildFooterItem(
            BuildHomeIcon(ActiveTab == BottomBarTab.Home),
            _loc.Get("tab_home"),
            ActiveTab == BottomBarTab.Home,
            _onHomeTap,
            out _homeLabel);

        var explore = BuildFooterItem(
            BuildExploreIcon(ActiveTab == BottomBarTab.Explore),
            _loc.Get("tab_explore"),
            ActiveTab == BottomBarTab.Explore,
            _onExploreTap,
            out _exploreLabel);

        var tour = BuildFooterItem(
            BuildTourIcon(ActiveTab == BottomBarTab.Tour),
            _loc.Get("tab_tour"),
            ActiveTab == BottomBarTab.Tour,
            _onTourTap,
            out _tourLabel);

        var settings = BuildFooterItem(
            BuildSettingsIcon(ActiveTab == BottomBarTab.Settings),
            _loc.Get("tab_settings"),
            ActiveTab == BottomBarTab.Settings,
            _onSettingsTap,
            out _settingsLabel);

        tabs.Children.Add(home);
        tabs.Children.Add(explore);
        Grid.SetColumn(explore, 1);
        tabs.Children.Add(tour);
        Grid.SetColumn(tour, 2);
        tabs.Children.Add(settings);
        Grid.SetColumn(settings, 3);

        return new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Colors.White,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(24, 24, 0, 0) },
            Content = tabs,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.End,
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.08f,
                Radius = 18,
                Offset = new Point(0, -3)
            }
        };
    }

    private static View BuildFooterItem(View icon, string text, bool active, Func<Task>? onTap, out Label labelRef)
    {
        labelRef = new Label
        {
            Text = text,
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            TextColor = active ? Color.FromArgb("#DC2626") : Color.FromArgb("#7D7D82"),
            HorizontalTextAlignment = TextAlignment.Center
        };

        var content = new VerticalStackLayout
        {
            Spacing = 4,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                new Border
                {
                    StrokeThickness = 0,
                    BackgroundColor = active ? Color.FromArgb("#FFE8E2") : Color.FromArgb("#F8FAFC"),
                    StrokeShape = new RoundRectangle { CornerRadius = 18 },
                    Padding = new Thickness(14, 7),
                    Content = icon
                },
                labelRef
            }
        };

        if (onTap is not null)
        {
            var tap = new TapGestureRecognizer();
            var isHandlingTap = false;
            tap.Tapped += async (_, __) =>
            {
                if (isHandlingTap)
                    return;

                isHandlingTap = true;
                content.InputTransparent = true;

                try
                {
                    await onTap();
                }
                finally
                {
                    content.InputTransparent = false;
                    isHandlingTap = false;
                }
            };
            content.GestureRecognizers.Add(tap);
        }

        return content;
    }

    private static View BuildHomeIcon(bool active)
    {
        var stroke = new SolidColorBrush(active ? Color.FromArgb("#DC2626") : Color.FromArgb("#94A3B8"));

        return new Grid
        {
            WidthRequest = 20,
            HeightRequest = 20,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Line
                {
                    X1 = 4.5,
                    Y1 = 9,
                    X2 = 10,
                    Y2 = 4.5,
                    Stroke = stroke,
                    StrokeThickness = 1.8
                },
                new Line
                {
                    X1 = 10,
                    Y1 = 4.5,
                    X2 = 15.5,
                    Y2 = 9,
                    Stroke = stroke,
                    StrokeThickness = 1.8
                },
                new Line
                {
                    X1 = 5.4,
                    Y1 = 8.7,
                    X2 = 5.4,
                    Y2 = 15.3,
                    Stroke = stroke,
                    StrokeThickness = 1.8
                },
                new Line
                {
                    X1 = 14.6,
                    Y1 = 8.7,
                    X2 = 14.6,
                    Y2 = 15.3,
                    Stroke = stroke,
                    StrokeThickness = 1.8
                },
                new Line
                {
                    X1 = 5.4,
                    Y1 = 15.3,
                    X2 = 14.6,
                    Y2 = 15.3,
                    Stroke = stroke,
                    StrokeThickness = 1.8
                },
                new Line
                {
                    X1 = 10,
                    Y1 = 11.6,
                    X2 = 10,
                    Y2 = 15.3,
                    Stroke = stroke,
                    StrokeThickness = 1.8
                }
            }
        };
    }

    private static View BuildSettingsIcon(bool active)
    {
        var strokeColor = active ? Color.FromArgb("#DC2626") : Color.FromArgb("#94A3B8");

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
                    WidthRequest = 18,
                    HeightRequest = 18,
                    Stroke = new SolidColorBrush(strokeColor),
                    StrokeThickness = 1.7,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                },
                new Ellipse
                {
                    WidthRequest = 6,
                    HeightRequest = 6,
                    Stroke = new SolidColorBrush(strokeColor),
                    StrokeThickness = 1.5,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            }
        };
    }

    private static View BuildExploreIcon(bool active)
    {
        var strokeColor = active ? Color.FromArgb("#DC2626") : Color.FromArgb("#94A3B8");
        var accentColor = active ? Color.FromArgb("#FB7185") : Color.FromArgb("#CBD5E1");

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
                    WidthRequest = 18,
                    HeightRequest = 18,
                    Stroke = new SolidColorBrush(strokeColor),
                    StrokeThickness = 1.7,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                },
                new Polygon
                {
                    Points = new PointCollection
                    {
                        new Point(12, 6),
                        new Point(8.2, 8.2),
                        new Point(6, 12),
                        new Point(9.8, 9.8)
                    },
                    Fill = new SolidColorBrush(accentColor),
                    Stroke = new SolidColorBrush(strokeColor),
                    StrokeThickness = 1.1,
                    WidthRequest = 12,
                    HeightRequest = 12,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                },
                new Ellipse
                {
                    WidthRequest = 3.2,
                    HeightRequest = 3.2,
                    Fill = new SolidColorBrush(strokeColor),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            }
        };
    }

    private static View BuildTourIcon(bool active)
    {
        var strokeColor = active ? Color.FromArgb("#DC2626") : Color.FromArgb("#94A3B8");
        var accentColor = active ? Color.FromArgb("#FB7185") : Color.FromArgb("#CBD5E1");

        return new Grid
        {
            WidthRequest = 20,
            HeightRequest = 20,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Line
                {
                    X1 = 4,
                    Y1 = 15,
                    X2 = 9,
                    Y2 = 7,
                    Stroke = new SolidColorBrush(strokeColor),
                    StrokeThickness = 1.8
                },
                new Line
                {
                    X1 = 9,
                    Y1 = 7,
                    X2 = 16,
                    Y2 = 12,
                    Stroke = new SolidColorBrush(strokeColor),
                    StrokeThickness = 1.8
                },
                new Ellipse
                {
                    WidthRequest = 5,
                    HeightRequest = 5,
                    Fill = new SolidColorBrush(accentColor),
                    Stroke = new SolidColorBrush(strokeColor),
                    StrokeThickness = 1,
                    TranslationX = -6,
                    TranslationY = 5
                },
                new Ellipse
                {
                    WidthRequest = 5,
                    HeightRequest = 5,
                    Fill = new SolidColorBrush(accentColor),
                    Stroke = new SolidColorBrush(strokeColor),
                    StrokeThickness = 1,
                    TranslationX = -1,
                    TranslationY = -3
                },
                new Ellipse
                {
                    WidthRequest = 5,
                    HeightRequest = 5,
                    Fill = new SolidColorBrush(accentColor),
                    Stroke = new SolidColorBrush(strokeColor),
                    StrokeThickness = 1,
                    TranslationX = 6,
                    TranslationY = 2
                }
            }
        };
    }
}
