using MauiApp1.Models;
using MauiApp1.Services;
using MauiApp1.Utils;
using Microsoft.Maui.Controls.Shapes;
using System.Collections.ObjectModel;
using System.Linq;

namespace MauiApp1.Views
{
    public class GianHangDetailPage : ContentPage
    {
        private readonly GianHang? _gianHang;
        private readonly string? _imagePath;
        private readonly string? _audioPath;
        private readonly LocalizationService _loc;

        private readonly Image _mainImage;
        private readonly Label _titleLabel;
        private readonly Label _addressLabel;
        private readonly Label _descriptionLabel;

        private readonly Button _playButton;
        private readonly Slider _progressSlider;
        private readonly Label _currentTimeLabel;
        private readonly Label _durationLabel;

        private readonly ObservableCollection<ImageSource> _demoFoodImages = new();
        private readonly Grid _detailSheet;

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
        private bool _isSheetVisible;
        private bool _hasShownInitialSheet;

        public GianHangDetailPage(GianHang gianHang, string? imagePath, string? audioPath, LocalizationService localizationService)
        {
            _gianHang = gianHang;
            _imagePath = imagePath;
            _audioPath = audioPath;
            _loc = localizationService;
            BackgroundColor = Colors.White;

            _mainImage = new Image
            {
                Source = "dotnet_bot.png",
                Aspect = Aspect.AspectFill,
                HeightRequest = 300
            };

            _titleLabel = new Label
            {
                FontSize = 24,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.Black
            };

            _addressLabel = new Label
            {
                FontSize = 14,
                TextColor = Colors.Gray
            };

            _descriptionLabel = new Label
            {
                FontSize = 15,
                TextColor = Colors.Black,
                LineBreakMode = LineBreakMode.WordWrap
            };

            _playButton = new Button
            {
                BackgroundColor = Color.FromArgb("#E85D04"),
                TextColor = Colors.White,
                CornerRadius = 12,
                Padding = new Thickness(16, 10)
            };
            _playButton.Clicked += OnPlayPauseClicked;

            _progressSlider = new Slider
            {
                Minimum = 0,
                Maximum = 1,
                Value = 0
            };
            _progressSlider.DragCompleted += OnSliderDragCompleted;

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

            SeedDemoFoodImages();
            _detailSheet = CreateDetailSheet();
            Content = BuildLayout();
            BindDataToUI();

            localizationService.LanguageChanged += () => MainThread.BeginInvokeOnMainThread(() =>
            {
                _playButton.Text = _loc.Get("btn_play");
            });

            Loaded += async (_, __) =>
            {
                InitializeSheetPositions();

                if (_hasShownInitialSheet)
                    return;

                _detailSheet.IsVisible = true;
                _isSheetVisible = true;
                _hasShownInitialSheet = true;
                await SnapSheetToAsync(_sheetHalfY);
            };

            SizeChanged += (_, __) => InitializeSheetPositions();
        }

        private View BuildLayout()
        {
            var root = new Grid();

            root.Children.Add(_mainImage);

            var overlay = new BoxView
            {
                Color = Color.FromRgba(0, 0, 0, 0.12),
                InputTransparent = true
            };
            root.Children.Add(overlay);

            root.Children.Add(_detailSheet);

            return root;
        }

        private Grid CreateDetailSheet()
        {
            var audioTitle = new Label
            {
                Text = _loc.Get("detail_audio_title"),
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.Black
            };

            var timeGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };

            timeGrid.Add(_currentTimeLabel);
            Grid.SetColumn(_currentTimeLabel, 0);

            timeGrid.Add(_durationLabel);
            Grid.SetColumn(_durationLabel, 1);

            var audioCard = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 18 },
                Stroke = Color.FromArgb("#EAEAEA"),
                BackgroundColor = Color.FromArgb("#FAFAFA"),
                Padding = 16,
                Content = new VerticalStackLayout
                {
                    Spacing = 12,
                    Children =
                    {
                        audioTitle,
                        _playButton,
                        _progressSlider,
                        timeGrid
                    }
                }
            };

            var foodTitle = new Label
            {
                Text = _loc.Get("detail_food_images"),
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.Black
            };

            var foodCollection = new CollectionView
            {
                ItemsSource = _demoFoodImages,
                ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Horizontal)
                {
                    ItemSpacing = 12
                },
                HeightRequest = 120,
                SelectionMode = SelectionMode.None,
                ItemTemplate = new DataTemplate(() =>
                {
                    var img = new Image
                    {
                        Aspect = Aspect.AspectFill,
                        HeightRequest = 100,
                        WidthRequest = 140
                    };
                    img.SetBinding(Image.SourceProperty, ".");

                    return new Border
                    {
                        StrokeShape = new RoundRectangle { CornerRadius = 16 },
                        Stroke = Color.FromArgb("#EEEEEE"),
                        Padding = 0,
                        Content = img
                    };
                })
            };

            var infoCard = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 20 },
                Stroke = Color.FromArgb("#EEEEEE"),
                BackgroundColor = Colors.White,
                Padding = 18,
                Content = new VerticalStackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        _titleLabel,
                        _addressLabel,
                        _descriptionLabel
                    }
                }
            };

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

            var contentScroll = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Spacing = 16,
                    Padding = new Thickness(16, 0, 16, 56),
                    Children =
                    {
                        infoCard,
                        audioCard,
                        foodTitle,
                        foodCollection
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
            body.Children.Add(contentScroll);
            Grid.SetRow(contentScroll, 1);

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
                Children = { panel },
                IsVisible = false,
                InputTransparent = false
            };
        }

        private void InitializeSheetPositions()
        {
            if (Width <= 0 || Height <= 0)
                return;

            _sheetFullY = Height * 0.02;
            _sheetHalfY = Height * 0.34;

            const double miniPeekHeight = 140;
            _sheetMiniY = Math.Max(_sheetHalfY + 40, Height - miniPeekHeight);

            _sheetHiddenY = Height + 20;

            if (!_isLayoutReady)
            {
                _currentSheetY = _sheetHiddenY;
                _detailSheet.TranslationY = _sheetHiddenY;
                _detailSheet.IsVisible = false;
                _isSheetVisible = false;
                _isLayoutReady = true;
            }
        }

        private void OnSheetPanUpdated(object? sender, PanUpdatedEventArgs e)
        {
            if (!_isLayoutReady || _isAnimating || !_isSheetVisible)
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
                    _detailSheet.TranslationY = _currentSheetY;
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

            if (y >= _sheetMiniY + 8)
                return _sheetHiddenY;

            if (deltaY > 0 && y >= _sheetMiniY - 4)
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

        private double ResolveSnapTarget(double y)
        {
            var points = new[] { _sheetFullY, _sheetHalfY, _sheetMiniY };
            return points.OrderBy(p => Math.Abs(p - y)).First();
        }

        private async Task SnapSheetToAsync(double targetY)
        {
            if (_isAnimating)
                return;

            _isAnimating = true;

            try
            {
                targetY = Math.Max(_sheetFullY, Math.Min(_sheetMiniY, targetY));

                await _detailSheet.TranslateToAsync(0, targetY, 180, Easing.CubicOut);

                _currentSheetY = targetY;
                _detailSheet.TranslationY = targetY;
            }
            finally
            {
                _isAnimating = false;
            }
        }

        private async Task HideSheetAsync()
        {
            if (!_isSheetVisible || _isAnimating)
                return;

            _isAnimating = true;

            try
            {
                await _detailSheet.TranslateToAsync(0, _sheetHiddenY, 180, Easing.CubicIn);

                _currentSheetY = _sheetHiddenY;
                _detailSheet.TranslationY = _sheetHiddenY;
                _detailSheet.IsVisible = false;
                _isSheetVisible = false;
            }
            finally
            {
                _isAnimating = false;
            }
        }

        private void BindDataToUI()
        {
            _playButton.Text = _loc.Get("btn_play");

            if (_gianHang != null)
            {
                _titleLabel.Text = string.IsNullOrWhiteSpace(_gianHang.Ten)
                    ? _loc.Get("fallback_name")
                    : _gianHang.Ten;

                _addressLabel.Text = string.IsNullOrWhiteSpace(_gianHang.DiaChi)
                    ? _loc.Get("fallback_address")
                    : _gianHang.DiaChi;

                _descriptionLabel.Text = string.IsNullOrWhiteSpace(_gianHang.MoTa)
                    ? _loc.Get("fallback_description")
                    : _gianHang.MoTa;

                _mainImage.Source = RemoteImageSourceFactory.Build(_gianHang.HinhAnhFullUrl);
            }
            else
            {
                _mainImage.Source = RemoteImageSourceFactory.Build(null);
            }
        }

        private void SeedDemoFoodImages()
        {
            _demoFoodImages.Clear();

            var imagePaths = _gianHang?.MonAns?
                .Where(item =>
                    !string.IsNullOrWhiteSpace(item.HinhAnhFullUrl) &&
                    (string.IsNullOrWhiteSpace(item.TinhTrang) ||
                     string.Equals(item.TinhTrang, "con_ban", StringComparison.OrdinalIgnoreCase)))
                .Select(item => item.HinhAnhFullUrl!)
                .Distinct()
                .Take(6)
                .ToList();

            if (imagePaths is not null && imagePaths.Count > 0)
            {
                foreach (var imagePath in imagePaths)
                    _demoFoodImages.Add(RemoteImageSourceFactory.Build(imagePath));

                return;
            }

            _demoFoodImages.Add(RemoteImageSourceFactory.Build(null));
        }

        private void OnPlayPauseClicked(object? sender, EventArgs e)
        {
        }

        private void OnSliderDragCompleted(object? sender, EventArgs e)
        {
        }
    }
}
