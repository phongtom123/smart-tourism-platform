using System.Linq;
using MauiApp1.Models;
using MauiApp1.Controls;
using MauiApp1.Services;
using MauiApp1.Utils;
using Microsoft.Maui.Controls.Shapes;

namespace MauiApp1.Views;

public class GianHangFoodGalleryPage : ContentPage
{
    private readonly GianHang _gianHang;
    private readonly MonAnService _monAnService;
    private readonly LocalizationService _loc;
    private readonly string _menuLanguageCode;
    private readonly string _heroImage;

    private readonly Label _heroSubtitleLabel;
    private readonly Label _summaryLabel;
    private readonly Label _menuActionLabel;
    private readonly Grid _featuredHeader;
    private readonly Grid _menuHeader;
    private readonly Border _featuredCard;
    private readonly Border _statusCard;
    private readonly Label _statusLabel;
    private readonly VerticalStackLayout _menuList;
    private readonly VerticalStackLayout _popularList;
    private readonly HorizontalStackLayout _drinkRow;
    private readonly AppRefreshView _refreshView;

    private bool _isLoaded;
    private static readonly TimeSpan ExploreRefreshInterval = TimeSpan.FromMinutes(5);

    public GianHangFoodGalleryPage(
        GianHang gianHang,
        MonAnService monAnService,
        LocalizationService localizationService,
        string? heroImage,
        string? menuLanguageCode = null)
    {
        _gianHang = gianHang;
        _monAnService = monAnService;
        _loc = localizationService;
        _menuLanguageCode = string.IsNullOrWhiteSpace(menuLanguageCode) ? _loc.CurrentLanguage : menuLanguageCode;
        _heroImage = string.IsNullOrWhiteSpace(heroImage)
            ? (_gianHang.HinhAnhFullUrl ?? "dotnet_bot.png")
            : heroImage;

        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Colors.White;

        _heroSubtitleLabel = new Label
        {
            Text = BuildHeroSubtitle(0),
            FontSize = 12,
            TextColor = Colors.White
        };

        _summaryLabel = new Label
        {
            Text = BuildSummaryText(0),
            FontSize = 14,
            TextColor = Color.FromArgb("#5C5C5C")
        };

        _menuActionLabel = new Label
        {
            Text = string.Format(GetText("menu_count"), 0),
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#FF6B00"),
            VerticalTextAlignment = TextAlignment.End
        };

        _featuredHeader = BuildSectionTitle(GetText("featured"), (Label?)null);
        _menuHeader = BuildSectionTitle(GetText("menu"), _menuActionLabel);

        _featuredCard = new Border
        {
            Stroke = Color.FromArgb("#EDEDED"),
            StrokeShape = new RoundRectangle { CornerRadius = 26 },
            Padding = 0,
            BackgroundColor = Colors.White,
            IsVisible = false,
            Content = CreateDishCard(GetText("featured_placeholder"), GetText("dish_desc_placeholder"), 0, true)
        };

        _statusLabel = new Label
        {
            FontSize = 14,
            TextColor = Color.FromArgb("#5C5C5C")
        };

        _statusCard = new Border
        {
            Stroke = Color.FromArgb("#EDEDED"),
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            Padding = 16,
            Content = _statusLabel
        };

        _menuList = new VerticalStackLayout { Spacing = 16 };
        _popularList = new VerticalStackLayout { Spacing = 16 };
        _drinkRow = new HorizontalStackLayout { Spacing = 12 };

        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };

        var header = BuildHeader();
        var scroll = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 20,
                Padding = new Thickness(16, 6, 16, 24),
                Children =
                {
                    BuildHero(),
                    CreateSummaryCard(),
                    _featuredHeader,
                    _featuredCard,
                    _statusCard,
                    BuildSectionTitle(GetText("popular"), GetText("view_all")),
                    _popularList,
                    new Label
                    {
                        Text = GetText("drinks"),
                        FontSize = 32,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#1F1F1F")
                    },
                    new ScrollView
                    {
                        Orientation = ScrollOrientation.Horizontal,
                        Content = _drinkRow
                    }
                }
            }
        };
        _refreshView = new AppRefreshView(
            scroll,
            async () => await LoadMenuAsync(forceRefresh: true));

        layout.Children.Add(header);
        layout.Children.Add(_refreshView);
        Grid.SetRow(_refreshView, 1);

        Content = layout;
        ShowStatus(GetText("loading"));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _refreshView.StartAutoRefresh(Dispatcher, ExploreRefreshInterval);

        if (_isLoaded)
            return;

        _isLoaded = true;

        try
        {
            await LoadMenuAsync();
        }
        catch (Exception ex)
        {
            _isLoaded = false;
            ShowStatus(GetText("load_failed"));
            System.Diagnostics.Debug.WriteLine($"[GianHangFoodGalleryPage] Load menu error: {ex.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _refreshView.StopAutoRefresh();
    }

    private View BuildHeader()
    {
        var backIcon = new Label
        {
            Text = "←",
            FontSize = 24,
            TextColor = Color.FromArgb("#FF6B00"),
            VerticalTextAlignment = TextAlignment.Center
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, __) => await Navigation.PopAsync();
        backIcon.GestureRecognizers.Add(tap);

        var title = new Label
        {
            Text = string.IsNullOrWhiteSpace(_gianHang.Ten) ? GetText("menu") : _gianHang.Ten,
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1F1F1F"),
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1
        };

        var avatar = new Border
        {
            HeightRequest = 38,
            WidthRequest = 38,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 19 },
            BackgroundColor = Color.FromArgb("#FFF1E8"),
            Content = new Label
            {
                Text = GetAvatarText(),
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#FF6B00"),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };

        var grid = new Grid
        {
            Padding = new Thickness(16, 16, 16, 6),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        grid.Children.Add(backIcon);
        grid.Children.Add(title);
        Grid.SetColumn(title, 1);
        grid.Children.Add(avatar);
        Grid.SetColumn(avatar, 2);

        return grid;
    }

    private View BuildHero()
    {
        return new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 26 },
            HeightRequest = 280,
            Content = new Grid
            {
                Children =
                {
                    new Image
                    {
                        Source = BuildImageSource(_heroImage),
                        Aspect = Aspect.AspectFill
                    },
                    new Border
                    {
                        StrokeThickness = 0,
                        BackgroundColor = Color.FromRgba(0, 0, 0, 0.38),
                        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(0, 0, 26, 26) },
                        VerticalOptions = LayoutOptions.End,
                        Padding = 16,
                        Content = new VerticalStackLayout
                        {
                            Spacing = 6,
                            Children =
                            {
                                new Border
                                {
                                    StrokeThickness = 0,
                                    BackgroundColor = Color.FromArgb("#FF6B00"),
                                    StrokeShape = new RoundRectangle { CornerRadius = 12 },
                                    Padding = new Thickness(10, 4),
                                    HorizontalOptions = LayoutOptions.Start,
                                    Content = new Label
                                    {
                                        Text = GetText("hero_badge"),
                                        FontSize = 11,
                                        FontAttributes = FontAttributes.Bold,
                                        TextColor = Colors.White
                                    }
                                },
                                new Label
                                {
                                    Text = string.IsNullOrWhiteSpace(_gianHang.Ten) ? GetText("store_fallback") : _gianHang.Ten,
                                    FontSize = 36,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Colors.White
                                },
                                new Label
                                {
                                    Text = "⭐ 4.9 (2.4k reviews)  ·  ⏰ 07:00 - 22:00",
                                    FontSize = 12,
                                    TextColor = Colors.White,
                                    IsVisible = false
                                },
                                _heroSubtitleLabel
                            }
                        }
                    }
                }
            }
        };
    }

    private View BuildCategoryChips()
    {
        var row = new HorizontalStackLayout { Spacing = 10 };
        row.Children.Add(CreateChip("Món chính", true));
        row.Children.Add(CreateChip("Khai vị", false));
        row.Children.Add(CreateChip("Đồ uống", false));
        return row;
    }

    private View CreateChip(string text, bool active)
    {
        return new Border
        {
            StrokeThickness = 0,
            BackgroundColor = active ? Color.FromArgb("#FF6B00") : Color.FromArgb("#EFEFEF"),
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(14, 8),
            Content = new Label
            {
                Text = text,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = active ? Colors.White : Color.FromArgb("#595959")
            }
        };
    }

    private Grid BuildSectionTitle(string title, Label? actionLabel)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };

        grid.Children.Add(new Label
        {
            Text = title,
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1F1F1F")
        });

        if (actionLabel is not null)
        {
            grid.Children.Add(actionLabel);
            Grid.SetColumn(actionLabel, 1);
        }

        return grid;
    }

    private Grid BuildSectionTitle(string title, string action)
    {
        return BuildSectionTitle(title, new Label
        {
            Text = action,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#FF6B00"),
            VerticalTextAlignment = TextAlignment.End
        });
    }

    private Border CreateSummaryCard()
    {
        return new Border
        {
            Stroke = Color.FromArgb("#ECECEC"),
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            BackgroundColor = Color.FromArgb("#FFF8F1"),
            Padding = 16,
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label
                    {
                        Text = GetText("summary_title"),
                        FontSize = 12,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#FF6B00")
                    },
                    _summaryLabel
                }
            }
        };
    }

    private async Task LoadMenuAsync(bool forceRefresh = false)
    {
        var items = FilterMenuItems(await _monAnService.GetByGianHangAsync(_gianHang.IdGianHang, _menuLanguageCode, forceRefresh));

        if (items.Count == 0 && _gianHang.MonAns.Count > 0)
            items = FilterMenuItems(_gianHang.MonAns);

        _summaryLabel.Text = BuildSummaryText(items.Count);
        _heroSubtitleLabel.Text = BuildHeroSubtitle(items.Count);
        _menuActionLabel.Text = string.Format(GetText("menu_count"), items.Count);

        _popularList.Children.Clear();
        _drinkRow.Children.Clear();

        if (items.Count == 0)
        {
            _featuredHeader.IsVisible = false;
            _featuredCard.IsVisible = false;
            ShowStatus(GetText("no_items"));
            return;
        }

        _featuredHeader.IsVisible = true;
        _featuredCard.IsVisible = true;
        HideStatus();
        _featuredCard.Content = CreateDishCard(items[0], true);

        foreach (var item in items.Skip(1).Take(3))
        {
            _popularList.Children.Add(CreateDishCard(item, false));
        }

        foreach (var item in items.Skip(4).Take(4))
        {
            _drinkRow.Children.Add(CreateDrinkCard(item));
        }

        if (_popularList.Children.Count == 0)
            ShowStatus(GetText("one_item"));
    }

    private View CreateDishCard(string? name, string? description, decimal price, bool large)
    {
        return CreateDishCard(new MonAn
        {
            TenMon = name ?? string.Empty,
            ThongTinMon = description ?? string.Empty,
            DonGia = price
        }, large);
    }

    private View CreateDishCard(MonAn item, bool large)
    {
        var price = item.DonGia;
        var name = item.TenMon;
        var description = item.ThongTinMon;
        var imageSource = item.HinhAnhFullUrl;

        var priceGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };

        var priceLabel = new Label
        {
            Text = $"{price:N0}đ",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#FF6B00")
        };

        var addButton = new Border
        {
            StrokeThickness = 0,
            HeightRequest = 32,
            WidthRequest = 32,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            BackgroundColor = Color.FromArgb("#FF6B00"),
            Content = new Label
            {
                Text = "+",
                FontSize = 18,
                TextColor = Colors.White,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };

        priceGrid.Children.Add(priceLabel);
        priceGrid.Children.Add(addButton);
        Grid.SetColumn(addButton, 1);

        return new Border
        {
            Stroke = Color.FromArgb("#ECECEC"),
            StrokeShape = new RoundRectangle { CornerRadius = 24 },
            BackgroundColor = Colors.White,
            Padding = 12,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Border
                    {
                        StrokeThickness = 0,
                        StrokeShape = new RoundRectangle { CornerRadius = 20 },
                        HeightRequest = large ? 210 : 130,
                        Content = new Image
                        {
                            Source = BuildImageSource(imageSource),
                            Aspect = Aspect.AspectFill
                        }
                    },
                    new Label
                    {
                        Text = string.IsNullOrWhiteSpace(name) ? GetText("dish_fallback") : name,
                        FontSize = 16,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#1F1F1F")
                    },
                    new Label
                    {
                        Text = string.IsNullOrWhiteSpace(description) ? GetText("dish_desc_fallback") : description,
                        FontSize = 13,
                        TextColor = Color.FromArgb("#707070"),
                        LineBreakMode = LineBreakMode.TailTruncation,
                        MaxLines = 2
                    },
                    priceGrid
                }
            }
        };
    }

    private View CreateDrinkCard(MonAn item)
    {
        var name = item.TenMon;
        var price = item.DonGia;
        var imageSource = item.HinhAnhFullUrl;

        return new Border
        {
            Stroke = Color.FromArgb("#ECECEC"),
            StrokeShape = new RoundRectangle { CornerRadius = 22 },
            BackgroundColor = Color.FromArgb("#F7F7F7"),
            WidthRequest = 150,
            Padding = new Thickness(12),
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Border
                    {
                        Stroke = Color.FromArgb("#FF6B00"),
                        StrokeShape = new RoundRectangle { CornerRadius = 30 },
                        HeightRequest = 60,
                        WidthRequest = 60,
                        HorizontalOptions = LayoutOptions.Center,
                        Content = new Image
                        {
                            Source = BuildImageSource(imageSource),
                            Aspect = Aspect.AspectFill
                        }
                    },
                    new Label
                    {
                        Text = string.IsNullOrWhiteSpace(name) ? GetText("drink_fallback") : name,
                        FontSize = 13,
                        FontAttributes = FontAttributes.Bold,
                        HorizontalTextAlignment = TextAlignment.Center,
                        TextColor = Color.FromArgb("#333")
                    },
                    new Label
                    {
                        Text = $"{price:N0}đ",
                        FontSize = 14,
                        FontAttributes = FontAttributes.Bold,
                        HorizontalTextAlignment = TextAlignment.Center,
                        TextColor = Color.FromArgb("#FF6B00")
                    }
                }
            }
        };
    }

    private void ShowStatus(string message)
    {
        _statusLabel.Text = message;
        _statusCard.IsVisible = true;
    }

    private void HideStatus()
    {
        _statusCard.IsVisible = false;
    }

    private static List<MonAn> FilterMenuItems(IEnumerable<MonAn>? items)
    {
        if (items is null)
            return new List<MonAn>();

        return items
            .Where(item => item is not null)
            .Where(item =>
                string.IsNullOrWhiteSpace(item.TinhTrang) ||
                string.Equals(item.TinhTrang, "con_ban", StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.IdMonAn)
            .ToList();
    }

    private string BuildSummaryText(int itemCount)
    {
        if (itemCount <= 0)
            return GetText("summary_empty");

        var storeName = string.IsNullOrWhiteSpace(_gianHang.Ten) ? GetText("store_generic") : _gianHang.Ten;
        return string.Format(GetText("summary_count"), storeName, itemCount);
    }

    private string BuildHeroSubtitle(int itemCount)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(_gianHang.DiaChi))
            parts.Add(_gianHang.DiaChi!);

        parts.Add(itemCount <= 0
            ? GetText("hero_empty")
            : string.Format(GetText("hero_count"), itemCount));
        return string.Join(" | ", parts);
    }

    private string GetAvatarText()
    {
        if (string.IsNullOrWhiteSpace(_gianHang.Ten))
            return "GH";

        var words = _gianHang.Ten
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(word => char.ToUpperInvariant(word[0]));

        return string.Concat(words);
    }

    private static string FormatPrice(decimal price)
    {
        return $"{price:N0}d";
    }

    private static ImageSource BuildImageSource(string? imagePath)
    {
        return RemoteImageSourceFactory.Build(imagePath);
    }

    private string GetText(string key)
    {
        return _loc.CurrentLanguage switch
        {
            "en" => key switch
            {
                "menu_count" => "{0} dishes",
                "featured" => "Featured",
                "menu" => "Menu",
                "featured_placeholder" => "Featured dish",
                "dish_desc_placeholder" => "Description is being updated",
                "popular" => "Popular mains",
                "view_all" => "View all",
                "drinks" => "Signature drinks",
                "loading" => "Loading menu from backend...",
                "load_failed" => "Could not load this stall's menu.",
                "hero_badge" => "STALL MENU",
                "store_fallback" => "Restaurant",
                "summary_title" => "Menu data",
                "no_items" => "This stall has no dishes currently available on the backend.",
                "one_item" => "This stall currently has 1 available dish.",
                "dish_fallback" => "Dish",
                "dish_desc_fallback" => "A signature dish from this stall.",
                "drink_fallback" => "Drink",
                "summary_empty" => "No available dishes were received for this stall.",
                "store_generic" => "Stall",
                "summary_count" => "{0} currently has {1} available dishes synced from the backend.",
                "hero_empty" => "No dishes available",
                "hero_count" => "{0} dishes available",
                _ => key
            },
            "ko" => key switch
            {
                "menu_count" => "메뉴 {0}개",
                "featured" => "추천 메뉴",
                "menu" => "메뉴",
                "featured_placeholder" => "추천 메뉴",
                "dish_desc_placeholder" => "설명 업데이트 중",
                "popular" => "인기 메인 메뉴",
                "view_all" => "전체 보기",
                "drinks" => "추천 음료",
                "loading" => "백엔드에서 메뉴를 불러오는 중...",
                "load_failed" => "이 가게의 메뉴를 불러오지 못했습니다.",
                "hero_badge" => "가게 메뉴",
                "store_fallback" => "식당",
                "summary_title" => "메뉴 데이터",
                "no_items" => "이 가게에는 현재 판매 중인 메뉴가 없습니다.",
                "one_item" => "이 가게는 현재 판매 중인 메뉴가 1개입니다.",
                "dish_fallback" => "음식",
                "dish_desc_fallback" => "가게의 대표 메뉴입니다.",
                "drink_fallback" => "음료",
                "summary_empty" => "이 가게의 판매 중인 메뉴를 아직 받지 못했습니다.",
                "store_generic" => "가게",
                "summary_count" => "{0}에는 현재 백엔드와 동기화된 판매 메뉴가 {1}개 있습니다.",
                "hero_empty" => "판매 중인 메뉴 없음",
                "hero_count" => "판매 중인 메뉴 {0}개",
                _ => key
            },
            "ja" => key switch
            {
                "menu_count" => "{0}品",
                "featured" => "おすすめ",
                "menu" => "メニュー",
                "featured_placeholder" => "おすすめ料理",
                "dish_desc_placeholder" => "説明を更新中です",
                "popular" => "人気メイン料理",
                "view_all" => "すべて見る",
                "drinks" => "おすすめドリンク",
                "loading" => "バックエンドからメニューを読み込み中...",
                "load_failed" => "この店舗のメニューを読み込めませんでした。",
                "hero_badge" => "店舗メニュー",
                "store_fallback" => "レストラン",
                "summary_title" => "メニューデータ",
                "no_items" => "この店舗には現在販売中の料理がありません。",
                "one_item" => "この店舗には現在販売中の料理が1品あります。",
                "dish_fallback" => "料理",
                "dish_desc_fallback" => "この店舗の定番料理です。",
                "drink_fallback" => "ドリンク",
                "summary_empty" => "この店舗の販売中メニューをまだ受信していません。",
                "store_generic" => "店舗",
                "summary_count" => "{0} には現在、バックエンドと同期された販売中メニューが {1} 品あります。",
                "hero_empty" => "販売中メニューなし",
                "hero_count" => "販売中 {0}品",
                _ => key
            },
            _ => key switch
            {
                "menu_count" => "{0} món",
                "featured" => "Món nổi bật",
                "menu" => "Thực đơn",
                "featured_placeholder" => "Món nổi bật",
                "dish_desc_placeholder" => "Mô tả đang cập nhật",
                "popular" => "Món chính phổ biến",
                "view_all" => "Xem tất cả",
                "drinks" => "Đồ uống đặc sắc",
                "loading" => "Đang tải thực đơn từ backend...",
                "load_failed" => "Không tải được thực đơn của gian hàng này.",
                "hero_badge" => "THỰC ĐƠN GIAN HÀNG",
                "store_fallback" => "Nhà hàng",
                "summary_title" => "Dữ liệu thực đơn",
                "no_items" => "Gian hàng này chưa có món nào đang bán trên backend.",
                "one_item" => "Gian hàng hiện có 1 món đang bán.",
                "dish_fallback" => "Món ăn",
                "dish_desc_fallback" => "Món ngon đặc trưng của gian hàng.",
                "drink_fallback" => "Đồ uống",
                "summary_empty" => "Chưa nhận được món đang bán cho gian hàng này.",
                "store_generic" => "Gian hàng",
                "summary_count" => "{0} hiện có {1} món đang bán được đồng bộ từ backend.",
                "hero_empty" => "Chưa có món đang bán",
                "hero_count" => "{0} món đang bán",
                _ => key
            }
        };
    }
}
