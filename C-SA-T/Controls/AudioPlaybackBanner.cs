using MauiApp1.Services;
using MauiApp1.Utils;
using Microsoft.Maui.Controls.Shapes;

namespace MauiApp1.Controls;

public sealed class AudioPlaybackBanner : ContentView
{
    private readonly GeofenceEngineService _audioService;
    private readonly LocalizationService _loc;
    private readonly Border _container;
    private readonly Image _coverImage;
    private readonly Label _statusLabel;
    private readonly Label _titleLabel;
    private readonly Border _stopButton;
    private bool _isSubscribed;

    public AudioPlaybackBanner(GeofenceEngineService audioService, LocalizationService localizationService)
    {
        _audioService = audioService;
        _loc = localizationService;

        _coverImage = new Image
        {
            HeightRequest = 42,
            WidthRequest = 42,
            Aspect = Aspect.AspectFill,
            Source = "dotnet_bot.png"
        };

        var coverWrap = new Border
        {
            StrokeThickness = 0,
            HeightRequest = 42,
            WidthRequest = 42,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            BackgroundColor = Color.FromArgb("#FFE7DA"),
            Content = _coverImage
        };

        _statusLabel = new Label
        {
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#C2410C"),
            Text = localizationService.Get("audio_playing")
        };

        _titleLabel = new Label
        {
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#0F172A"),
            MaxLines = 1,
            LineBreakMode = LineBreakMode.TailTruncation,
            Text = localizationService.Get("audio_banner_title")
        };

        var textWrap = new VerticalStackLayout
        {
            Spacing = 0,
            VerticalOptions = LayoutOptions.Center,
            Children = { _statusLabel, _titleLabel }
        };

        _stopButton = new Border
        {
            StrokeThickness = 0,
            HeightRequest = 34,
            WidthRequest = 34,
            StrokeShape = new RoundRectangle { CornerRadius = 17 },
            BackgroundColor = Colors.White,
            VerticalOptions = LayoutOptions.Center,
            Content = BuildStopIcon(),
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.05f,
                Radius = 8,
                Offset = new Point(0, 2)
            }
        };

        var stopTap = new TapGestureRecognizer();
        stopTap.Tapped += async (_, __) => await _audioService.StopPlaybackAsync();
        _stopButton.GestureRecognizers.Add(stopTap);

        var content = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10,
            Children =
            {
                coverWrap,
                textWrap,
                _stopButton
            }
        };
        Grid.SetColumn(textWrap, 1);
        Grid.SetColumn(_stopButton, 2);

        _container = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            BackgroundColor = Color.FromArgb("#FFF6EF"),
            Padding = new Thickness(12, 10),
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.07f,
                Radius = 12,
                Offset = new Point(0, 4)
            },
            Content = content
        };

        var swipeLeft = new SwipeGestureRecognizer { Direction = SwipeDirection.Left };
        swipeLeft.Swiped += async (_, __) => await _audioService.StopPlaybackAsync();
        _container.GestureRecognizers.Add(swipeLeft);

        var swipeRight = new SwipeGestureRecognizer { Direction = SwipeDirection.Right };
        swipeRight.Swiped += async (_, __) => await _audioService.StopPlaybackAsync();
        _container.GestureRecognizers.Add(swipeRight);

        Content = _container;
        IsVisible = false;
        Opacity = 0;
        VerticalOptions = LayoutOptions.End;
        HorizontalOptions = LayoutOptions.Fill;
        Margin = new Thickness(16, 0, 16, 86);
        ZIndex = 40;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        localizationService.LanguageChanged += () => MainThread.BeginInvokeOnMainThread(() =>
        {
            _statusLabel.Text = _loc.Get("audio_playing");
            _titleLabel.Text = _loc.Get("audio_banner_title");
        });
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (_isSubscribed)
            return;

        _audioService.PlaybackStateChanged += OnPlaybackStateChanged;
        _isSubscribed = true;
        ApplyState(_audioService.PlaybackState, animate: false);
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (!_isSubscribed)
            return;

        _audioService.PlaybackStateChanged -= OnPlaybackStateChanged;
        _isSubscribed = false;
    }

    private void OnPlaybackStateChanged(object? sender, AudioPlaybackStateChangedEventArgs e)
    {
        ApplyState(e.State, animate: true);
    }

    private async void ApplyState(AudioPlaybackStateSnapshot state, bool animate)
    {
        if (!state.IsVisible)
        {
            if (animate && IsVisible)
                await this.FadeToAsync(0, 140, Easing.CubicIn);

            IsVisible = false;
            Opacity = 0;
            return;
        }

        _statusLabel.Text = state.Message;
        _titleLabel.Text = string.IsNullOrWhiteSpace(state.Title) ? _loc.Get("audio_banner_title") : state.Title;
        _container.BackgroundColor = state.Phase == AudioPlaybackPhase.Pending
            ? Color.FromArgb("#FFF0E4")
            : Color.FromArgb("#FFF6EF");
        _statusLabel.TextColor = state.Phase == AudioPlaybackPhase.Pending
            ? Color.FromArgb("#EA580C")
            : Color.FromArgb("#C2410C");
        _coverImage.Source = BuildImageSource(state.ImageUrl);

        if (!IsVisible)
        {
            IsVisible = true;
            TranslationY = 16;
            Opacity = 0;
        }

        if (!animate)
        {
            TranslationY = 0;
            Opacity = 1;
            return;
        }

        await Task.WhenAll(
            this.TranslateToAsync(0, 0, 180, Easing.CubicOut),
            this.FadeToAsync(1, 180, Easing.CubicOut));
    }

    private static ImageSource BuildImageSource(string? imageUrl)
    {
        return RemoteImageSourceFactory.Build(imageUrl);
    }

    private static View BuildStopIcon()
    {
        return new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#EF4444"),
            StrokeShape = new RoundRectangle { CornerRadius = 4 },
            HeightRequest = 12,
            WidthRequest = 12,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
    }
}
