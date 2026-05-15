using Microsoft.Maui.Controls.Shapes;
using MauiApp1.Models;
using MauiApp1.Services;
using MauiColor = Microsoft.Maui.Graphics.Color;

namespace MauiApp1.Views;

public class PackageRegistrationPage : ContentPage
{
    private readonly AccessFlowService _accessFlowService;
    private readonly ApiService _apiService;
    private readonly LocalizationService _loc;
    private readonly IImageGallerySaver? _gallerySaver;
    private readonly VerticalStackLayout _packageList;
    private readonly Button _paymentButton;
    private readonly Label _selectedPlanLabel;
    private readonly Label _helperLabel;
    private PackagePlanOption? _selectedPlan;
    private readonly List<PackagePlanOption> _plans;
    private bool _isLoadingPlans;

    public PackageRegistrationPage(AccessFlowService accessFlowService, ApiService apiService, LocalizationService localizationService, IImageGallerySaver? gallerySaver = null)
    {
        _accessFlowService = accessFlowService;
        _apiService = apiService;
        _loc = localizationService;
        _gallerySaver = gallerySaver;
        _plans = new List<PackagePlanOption>();
        _isLoadingPlans = true;

        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = MauiColor.FromArgb("#FFF7F1");
        Title = string.Empty;

        _selectedPlanLabel = new Label
        {
            Text = GetText("no_plan"),
            FontSize = 13,
            TextColor = MauiColor.FromArgb("#9A3412")
        };

        _helperLabel = new Label
        {
            Text = GetText("helper_default"),
            FontSize = 13,
            TextColor = MauiColor.FromArgb("#6B7280"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        _packageList = new VerticalStackLayout { Spacing = 12 };
        RenderPackages();
        _ = LoadPackagesAsync();

        _paymentButton = new Button
        {
            Text = GetText("pay"),
            BackgroundColor = MauiColor.FromArgb("#F97316"),
            TextColor = Colors.White,
            CornerRadius = 16,
            Padding = new Thickness(14, 12),
            IsVisible = false
        };
        _paymentButton.Clicked += async (_, __) => await GoToPaymentAsync();

        var backButton = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Colors.White,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Padding = new Thickness(14, 10),
            Content = new Label
            {
                Text = GetText("back"),
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = MauiColor.FromArgb("#334155")
            }
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, __) => await Navigation.PopAsync();
        backButton.GestureRecognizers.Add(tap);

        var scrollView = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 18,
                Padding = new Thickness(16, 52, 16, 20),
                Children =
                {
                    backButton,
                    new Label
                    {
                        Text = GetText("title"),
                        FontSize = 28,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = MauiColor.FromArgb("#111111")
                    },
                    new Label
                    {
                        Text = GetText("subtitle"),
                        FontSize = 14,
                        TextColor = MauiColor.FromArgb("#6B7280"),
                        LineBreakMode = LineBreakMode.WordWrap
                    },
                    BuildCard(
                        new VerticalStackLayout
                        {
                            Spacing = 10,
                            Children =
                            {
                                new Label
                                {
                                    Text = GetText("plan_section"),
                                    FontSize = 15,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = MauiColor.FromArgb("#111111")
                                },
                                _packageList,
                                _selectedPlanLabel,
                                _helperLabel
                            }
                        })
                }
            }
        };

        var footer = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Colors.White,
            Padding = new Thickness(16, 12, 16, 18),
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.05f,
                Radius = 12,
                Offset = new Point(0, -2)
            },
            Content = _paymentButton
        };

        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        rootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        rootGrid.Children.Add(scrollView);
        rootGrid.Children.Add(footer);
        rootGrid.SetRow(scrollView, 0);
        rootGrid.SetRow(footer, 1);

        Content = rootGrid;
    }

    private void RenderPackages()
    {
        _packageList.Children.Clear();

        if (_isLoadingPlans)
        {
            _packageList.Children.Add(new Label
            {
                Text = GetText("loading_packages"),
                FontSize = 13,
                TextColor = MauiColor.FromArgb("#6B7280")
            });
            return;
        }

        if (_plans.Count == 0)
        {
            _packageList.Children.Add(new Label
            {
                Text = GetText("no_packages"),
                FontSize = 13,
                TextColor = MauiColor.FromArgb("#9A3412"),
                LineBreakMode = LineBreakMode.WordWrap
            });
            return;
        }

        foreach (var plan in _plans)
        {
            var isSelected = _selectedPlan?.BackendPackageId == plan.BackendPackageId;

            var card = new Border
            {
                StrokeThickness = 1,
                Stroke = isSelected ? MauiColor.FromArgb("#F97316") : MauiColor.FromArgb("#E5E7EB"),
                BackgroundColor = isSelected ? MauiColor.FromArgb("#FFF7ED") : Colors.White,
                StrokeShape = new RoundRectangle { CornerRadius = 18 },
                Padding = new Thickness(14),
                Content = new VerticalStackLayout
                {
                    Spacing = 5,
                    Children =
                    {
                        new Label
                        {
                            Text = plan.Name,
                            FontSize = 18,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = MauiColor.FromArgb("#111111")
                        },
                        new Label
                        {
                            Text = plan.Description,
                            FontSize = 13,
                            TextColor = MauiColor.FromArgb("#6B7280"),
                            LineBreakMode = LineBreakMode.WordWrap
                        },
                        new Label
                        {
                            Text = string.Format(GetText("plan_meta"), plan.DurationDays, plan.Price),
                            FontSize = 12,
                            TextColor = isSelected ? MauiColor.FromArgb("#9A3412") : MauiColor.FromArgb("#64748B")
                        }
                    }
                }
            };

            var gesture = new TapGestureRecognizer();
            gesture.Tapped += (_, __) =>
            {
                if (!plan.IsEnabled)
                    return;

                _selectedPlan = plan;
                _selectedPlanLabel.Text = string.Format(GetText("selected_plan"), plan.Name);
                _helperLabel.Text = GetText("helper_selected");
                RenderPackages();
                RefreshPaymentButton();
            };
            card.GestureRecognizers.Add(gesture);

            _packageList.Children.Add(card);
        }
    }

    private async Task LoadPackagesAsync()
    {
        try
        {
            var packages = await _apiService.GetServicePackagesAsync();
            var plans = packages
                .Where(package => package.IdGoi > 0)
                .Select(package => new PackagePlanOption(
                    package.IdGoi,
                    package.Ten,
                    string.IsNullOrWhiteSpace(package.MoTa) ? GetText("package_no_description") : package.MoTa.Trim(),
                    Math.Max(1, package.ThoiHanNgay),
                    package.Gia,
                    true))
                .ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _plans.Clear();
                _plans.AddRange(plans);
                _isLoadingPlans = false;

                if (_selectedPlan is not null && _plans.All(plan => plan.BackendPackageId != _selectedPlan.BackendPackageId))
                {
                    _selectedPlan = null;
                    _selectedPlanLabel.Text = GetText("no_plan");
                    _helperLabel.Text = GetText("helper_default");
                    RefreshPaymentButton();
                }

                RenderPackages();
            });
        }
        catch
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _plans.Clear();
                _isLoadingPlans = false;
                _selectedPlan = null;
                _selectedPlanLabel.Text = GetText("no_plan");
                _helperLabel.Text = GetText("load_failed");
                RefreshPaymentButton();
                RenderPackages();
            });
        }
    }

    private void RefreshPaymentButton()
    {
        _paymentButton.IsVisible = _selectedPlan is not null;
    }

    private async Task GoToPaymentAsync()
    {
        if (_selectedPlan is null)
            return;

        await Navigation.PushAsync(new PackageQrPaymentPage(_accessFlowService, _loc, _selectedPlan, string.Empty, _gallerySaver));
    }

    private static View BuildCard(View content)
    {
        return new Border
        {
            StrokeThickness = 1,
            Stroke = MauiColor.FromArgb("#F0E6DC"),
            BackgroundColor = Colors.White,
            StrokeShape = new RoundRectangle { CornerRadius = 20 },
            Padding = new Thickness(16, 14),
            Content = content
        };
    }

    private List<PackagePlanOption> BuildPlans()
    {
        return _loc.CurrentLanguage switch
        {
            "en" =>
            [
                new(1, "Day pass", "1 day of access for visitors.", 1, 15000m, true),
                new(2, "7-day pass", "7 days of access with a QR login token.", 7, 70000m, true),
                new(3, "Monthly pass", "30 days of access with a QR login token.", 30, 500000m, true)
            ],
            "ko" =>
            [
                new(1, "1일권", "방문객용 1일 이용권입니다.", 1, 15000m, true),
                new(2, "7일권", "QR 로그인 토큰으로 7일 동안 이용할 수 있습니다.", 7, 70000m, true),
                new(3, "월간권", "QR 로그인 토큰으로 30일 동안 이용할 수 있습니다.", 30, 500000m, true)
            ],
            "ja" =>
            [
                new(1, "1日パス", "訪問者向けの1日アクセスです。", 1, 15000m, true),
                new(2, "7日パス", "QRログイントークンで7日間アクセスできます。", 7, 70000m, true),
                new(3, "月間パス", "QRログイントークンで30日間アクセスできます。", 30, 500000m, true)
            ],
            _ =>
            [
                new(1, "Gói ngày", "1 ngày truy cập cho du khách.", 1, 15000m, true),
                new(2, "Gói 7 ngày", "7 ngày truy cập bằng QR token đăng nhập.", 7, 70000m, true),
                new(3, "Gói tháng", "30 ngày truy cập bằng QR token đăng nhập.", 30, 500000m, true)
            ]
        };
    }

    private string GetText(string key)
    {
        switch (key)
        {
            case "loading_packages":
                return "Dang tai danh sach goi dich vu...";
            case "no_packages":
                return "Chua co goi dich vu dang hoat dong.";
            case "load_failed":
                return "Khong tai duoc danh sach goi. Vui long kiem tra backend va thu lai.";
            case "package_no_description":
                return "Goi dich vu";
        }

        return _loc.CurrentLanguage switch
        {
            "en" => key switch
            {
                "no_plan" => "No package selected yet.",
                "helper_default" => "Choose a package, then tap Pay to open the QR payment page.",
                "helper_selected" => "Tap Pay to open the QR payment page. On the next screen you can enter an email to receive the QR token, or download the QR directly.",
                "loading_packages" => "Loading service packages...",
                "no_packages" => "No active service packages are available.",
                "load_failed" => "Could not load service packages. Please check the backend and try again.",
                "package_no_description" => "Service package",
                "pay" => "Pay",
                "back" => "Back",
                "title" => "Register access package",
                "subtitle" => "Choose a service package, then tap Pay. You can enter your email (to receive the QR token) or download the QR directly on the next screen.",
                "plan_section" => "Service packages",
                "plan_meta" => "{0} days | {1:N0} VND",
                "selected_plan" => "Selected: {0}",
                _ => key
            },
            "ko" => key switch
            {
                "no_plan" => "아직 선택한 패키지가 없습니다.",
                "helper_default" => "패키지를 선택한 뒤 결제를 눌러 QR 결제 화면으로 이동하세요.",
                "helper_selected" => "결제를 누르면 QR 결제 화면이 열립니다. 다음 화면에서 이메일로 QR 토큰을 받거나 QR을 바로 내려받을 수 있습니다.",
                "pay" => "결제",
                "back" => "뒤로",
                "title" => "이용 패키지 등록",
                "subtitle" => "서비스 패키지를 선택한 뒤 결제를 누르세요. 다음 화면에서 이메일 입력(QR 수신)하거나 QR을 바로 내려받을 수 있습니다.",
                "plan_section" => "서비스 패키지 목록",
                "plan_meta" => "{0}일 | {1:N0} VND",
                "selected_plan" => "선택됨: {0}",
                _ => key
            },
            "ja" => key switch
            {
                "no_plan" => "まだプランが選択されていません。",
                "helper_default" => "プランを選んでから、支払いを押してQR決済ページへ進みます。",
                "helper_selected" => "支払いを押すとQR決済ページが開きます。次の画面でメールアドレスを入力してQRを受信するか、QRを直接ダウンロードできます。",
                "pay" => "支払い",
                "back" => "戻る",
                "title" => "アクセスプラン登録",
                "subtitle" => "サービスプランを選んでから、支払いを押してください。次の画面でメール入力(QR受信)またはQRを直接ダウンロードできます。",
                "plan_section" => "サービスプラン一覧",
                "plan_meta" => "{0}日 | {1:N0} VND",
                "selected_plan" => "選択中: {0}",
                _ => key
            },
            _ => key switch
            {
                "no_plan" => "Chưa chọn gói nào.",
                "helper_default" => "Chọn một gói, sau đó bấm Thanh toán để sang trang QR thanh toán.",
                "helper_selected" => "Bấm Thanh toán để mở trang QR thanh toán. Tại trang tiếp theo bạn có thể nhập email để nhận QR token, hoặc tải QR trực tiếp về máy.",
                "pay" => "Thanh toán",
                "back" => "Quay lại",
                "title" => "Đăng ký gói truy cập",
                "subtitle" => "Chọn gói dịch vụ, sau đó bấm Thanh toán. Tại trang tiếp theo bạn có thể nhập email để nhận QR token, hoặc tải QR trực tiếp về máy.",
                "plan_section" => "Danh sách gói dịch vụ",
                "plan_meta" => "{0} ngày | {1:N0} VND",
                "selected_plan" => "Đã chọn: {0}",
                _ => key
            }
        };
    }
}
