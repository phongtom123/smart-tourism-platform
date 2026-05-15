using MauiApp1.Models;
using MauiApp1.Services;
using Microsoft.Maui.Controls.Shapes;
using System.Collections.ObjectModel;
using System.Globalization;

namespace MauiApp1.Views
{
    public class GianHangPage : ContentPage
    {
        private readonly GianHangService _gianHangService;
        private readonly LocalizationService _loc;
        private readonly ObservableCollection<GianHang> _items = new();
        private readonly CollectionView _collectionView;

        public GianHangPage(GianHangService gianHangService, LocalizationService localizationService)
        {
            _gianHangService = gianHangService;
            _loc = localizationService;

            Title = "Danh sÃ¡ch gian hÃ ng";
            BackgroundColor = Colors.White;
            SafeAreaEdges = SafeAreaEdges.None;

            var btnLoad = new Button
            {
                Text = "Táº£i dá»¯ liá»‡u gian hÃ ng",
                BackgroundColor = Color.FromArgb("#FF9F1C"),
                TextColor = Colors.White,
                CornerRadius = 12,
                Padding = new Thickness(14, 10)
            };
            btnLoad.Clicked += OnLoadClicked;

            _collectionView = new CollectionView
            {
                ItemsSource = _items,
                SelectionMode = SelectionMode.None,
                ItemTemplate = new DataTemplate(() =>
                {
                    var ten = new Label
                    {
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 18,
                        TextColor = Colors.Black
                    };
                    ten.SetBinding(Label.TextProperty, "Ten");

                    var diaChi = new Label
                    {
                        FontSize = 14,
                        TextColor = Colors.Gray
                    };
                    diaChi.SetBinding(Label.TextProperty, "DiaChi");

                    var moTa = new Label
                    {
                        FontSize = 14,
                        TextColor = Colors.Black,
                        LineBreakMode = LineBreakMode.WordWrap,
                        MaxLines = 3
                    };
                    moTa.SetBinding(Label.TextProperty, "MoTa");

                    var toaDo = new Label
                    {
                        FontSize = 13,
                        TextColor = Colors.DarkGray
                    };
                    toaDo.SetBinding(Label.TextProperty,
                        new Binding(path: ".", converter: new GianHangToaDoConverter()));

                    var btnDetail = new Button
                    {
                        Text = "Xem chi tiáº¿t",
                        BackgroundColor = Color.FromArgb("#E85D04"),
                        TextColor = Colors.White,
                        CornerRadius = 10,
                        Padding = new Thickness(14, 8),
                        HorizontalOptions = LayoutOptions.Start
                    };

                    btnDetail.Clicked += async (s, e) =>
                    {
                        try
                        {
                            var button = (Button)s!;
                            var gianHang = button.BindingContext as GianHang;

                            if (gianHang == null)
                            {
                                await DisplayAlertAsync("Debug", "gianHang Ä‘ang null", "OK");
                                return;
                            }

                            await DisplayAlertAsync("Debug", $"Stack hiá»‡n táº¡i: {Navigation.NavigationStack.Count}", "OK");

                            string imagePath = "banhtrangcoba.jpg";
                            string audioPath = "gianhang_1_vi.mp3";

                            var detailPage = new GianHangDetailPage(gianHang, imagePath, audioPath, _loc);
                            await Navigation.PushAsync(detailPage);
                        }
                        catch (Exception ex)
                        {
                            await DisplayAlertAsync("Lá»—i", ex.ToString(), "OK");
                        }
                    };
                    btnDetail.SetBinding(Button.BindingContextProperty, ".");

                    var cardContent = new VerticalStackLayout
                    {
                        Spacing = 6,
                        Children =
                        {
                            ten,
                            diaChi,
                            moTa,
                            toaDo,
                            btnDetail
                        }
                    };

                    return new Border
                    {
                        Margin = new Thickness(0, 8),
                        Padding = 14,
                        Stroke = Color.FromArgb("#EAEAEA"),
                        BackgroundColor = Colors.White,
                        StrokeShape = new RoundRectangle
                        {
                            CornerRadius = 16
                        },
                        Content = cardContent
                    };
                })
            };

            var pageBody = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 12,
                Children =
                {
                    btnLoad,
                    _collectionView
                }
            };

            var root = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition(GridLength.Star),
                    new RowDefinition(GridLength.Auto)
                }
            };

            root.Children.Add(pageBody);

            var footer = BuildFooter();
            root.Children.Add(footer);
            Grid.SetRow(footer, 1);

            Content = root;
        }

        private View BuildFooter()
        {
            View BuildFooterItem(string emoji, string text, bool active)
            {
                var iconWrap = new Border
                {
                    StrokeThickness = 0,
                    BackgroundColor = active ? Color.FromArgb("#F4E3D7") : Colors.Transparent,
                    StrokeShape = new RoundRectangle { CornerRadius = 16 },
                    Padding = new Thickness(10, 6),
                    HorizontalOptions = LayoutOptions.Center,
                    Content = new Label
                    {
                        Text = emoji,
                        FontSize = 20,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                };

                var label = new Label
                {
                    Text = text,
                    FontSize = 12,
                    TextColor = active ? Color.FromArgb("#111111") : Color.FromArgb("#8A8A8A"),
                    HorizontalTextAlignment = TextAlignment.Center
                };

                return new VerticalStackLayout
                {
                    Spacing = 4,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Children = { iconWrap, label }
                };
            }

            var home = BuildFooterItem("ðŸ ", "Trang chá»§", false);
            var explore = BuildFooterItem("ðŸ§­", "KhÃ¡m phÃ¡", true);
            var map = BuildFooterItem("ðŸ—ºï¸", "Báº£n Ä‘á»“", false);
            var profile = BuildFooterItem("ðŸ‘¤", "TÃ´i", false);

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star)
                },
                Padding = new Thickness(16, 10)
            };

            grid.Children.Add(home);

            grid.Children.Add(explore);
            Grid.SetColumn(explore, 1);

            grid.Children.Add(map);
            Grid.SetColumn(map, 2);

            grid.Children.Add(profile);
            Grid.SetColumn(profile, 3);

            return new Border
            {
                StrokeThickness = 0,
                BackgroundColor = Colors.White,
                StrokeShape = new RoundRectangle { CornerRadius = 28 },
                Content = grid,
                Shadow = new Shadow
                {
                    Brush = Brush.Black,
                    Opacity = 0.14f,
                    Radius = 18,
                    Offset = new Point(0, 6)
                },
                Margin = new Thickness(14, 0, 14, 0),
                VerticalOptions = LayoutOptions.End,
                HorizontalOptions = LayoutOptions.Fill
            };
        }

        private async void OnLoadClicked(object? sender, EventArgs e)
        {
            try
            {
                _items.Clear();

                var data = await _gianHangService.GetAllAsync();

                foreach (var item in data)
                    _items.Add(item);

                if (_items.Count == 0)
                {
                    await DisplayAlertAsync("ThÃ´ng bÃ¡o", "KhÃ´ng cÃ³ dá»¯ liá»‡u gian hÃ ng.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Lá»—i", $"KhÃ´ng táº£i Ä‘Æ°á»£c dá»¯ liá»‡u: {ex.Message}", "OK");
            }
        }
    }

    public class GianHangToaDoConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not GianHang gianHang)
                return "KhÃ´ng cÃ³ dá»¯ liá»‡u";

            if (gianHang.Lat == null || gianHang.Lon == null)
                return "ChÆ°a cÃ³ tá»a Ä‘á»™";

            return $"Lat: {gianHang.Lat}, Lon: {gianHang.Lon}";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value ?? "";
        }
    }
}
