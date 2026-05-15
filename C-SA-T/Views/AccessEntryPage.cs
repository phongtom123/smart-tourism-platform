using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using MauiApp1.Services;

namespace MauiApp1.Views;

public class AccessEntryPage : ContentPage
{
    private readonly AccessFlowService _accessFlowService;
    private readonly LocalizationService _loc;
    private readonly Label _statusLabel;
    private readonly Label _subtitleLabel;
    private readonly ActivityIndicator _loading;
    private readonly VerticalStackLayout _optionsLayout;
    private readonly Label _badgeLabel;
    private readonly Label _qrOptionTitleLabel;
    private readonly Label _qrOptionDescriptionLabel;
    private readonly Label _packageOptionTitleLabel;
    private readonly Label _packageOptionDescriptionLabel;
    private bool _isChecking;

    public AccessEntryPage(AccessFlowService accessFlowService, LocalizationService localizationService)
    {
        _accessFlowService = accessFlowService;
        _loc = localizationService;

        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Color.FromArgb("#FFF7F1");
        Title = string.Empty;

        _statusLabel = new Label
        {
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#111111")
        };

        _subtitleLabel = new Label
        {
            FontSize = 14,
            TextColor = Color.FromArgb("#6B7280"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        _loading = new ActivityIndicator
        {
            IsRunning = true,
            Color = Color.FromArgb("#F97316"),
            HorizontalOptions = LayoutOptions.Start
        };

        _optionsLayout = new VerticalStackLayout
        {
            Spacing = 14,
            IsVisible = false,
            Children =
            {
                BuildOptionCard(
                    out _qrOptionTitleLabel,
                    out _qrOptionDescriptionLabel,
                    async () =>
                    {
                        var qrPage = App.Current?.Handler?.MauiContext?.Services.GetRequiredService<QrScanPage>();
                        if (qrPage != null)
                            await Navigation.PushAsync(qrPage);
                    }),
                BuildOptionCard(
                    out _packageOptionTitleLabel,
                    out _packageOptionDescriptionLabel,
                    async () =>
                    {
                        var packagePage = App.Current?.Handler?.MauiContext?.Services.GetRequiredService<PackageRegistrationPage>();
                        if (packagePage != null)
                            await Navigation.PushAsync(packagePage);
                    })
            }
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 20,
                Padding = new Thickness(16, 64, 16, 28),
                Children =
                {
                    new Border
                    {
                        StrokeThickness = 0,
                        BackgroundColor = Color.FromArgb("#FFF1E6"),
                        StrokeShape = new RoundRectangle { CornerRadius = 999 },
                        Padding = new Thickness(12, 7),
                        HorizontalOptions = LayoutOptions.Start,
                        Content = _badgeLabel = new Label
                        {
                            FontSize = 12,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Color.FromArgb("#9A3412")
                        }
                    },
                    _statusLabel,
                    _subtitleLabel,
                    _loading,
                    _optionsLayout
                }
            }
        };

        UpdateLocalizedText();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_isChecking)
            return;

        _isChecking = true;
        await CheckAccessAsync();
        _isChecking = false;
    }

    private async Task CheckAccessAsync()
    {
        _loading.IsVisible = true;
        _loading.IsRunning = true;
        _optionsLayout.IsVisible = false;
        _statusLabel.Text = GetText("opening_title");
        _subtitleLabel.Text = GetText("opening_desc");

        var validation = await _accessFlowService.ValidateCurrentAccessAsync();
        if (validation.IsValid)
        {
            _statusLabel.Text = GetText("valid_title");
            _subtitleLabel.Text = validation.ExpiresAtUtc.HasValue
                ? string.Format(GetText("valid_desc_with_expiry"), validation.ExpiresAtUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm"))
                : GetText("valid_desc");

            await Task.Delay(450);
            if (Application.Current is App app)
                await app.ShowMainPageAsync();
            return;
        }

        _loading.IsRunning = false;
        _loading.IsVisible = false;
        _statusLabel.Text = GetText("choose_title");
        _subtitleLabel.Text = string.IsNullOrWhiteSpace(validation.Message)
            ? GetText("choose_desc")
            : string.Format(GetText("choose_desc_with_reason"), validation.Message);
        _optionsLayout.IsVisible = true;
    }

    private void UpdateLocalizedText()
    {
        _badgeLabel.Text = GetText("badge");
        _qrOptionTitleLabel.Text = GetText("qr_title");
        _qrOptionDescriptionLabel.Text = GetText("qr_desc");
        _packageOptionTitleLabel.Text = GetText("package_title");
        _packageOptionDescriptionLabel.Text = GetText("package_desc");
    }

    private string GetText(string key)
    {
        return _loc.CurrentLanguage switch
        {
            "en" => key switch
            {
                "badge" => "Access Flow",
                "opening_title" => "Opening app",
                "opening_desc" => "Checking the local access token before entering the system.",
                "valid_title" => "Valid token",
                "valid_desc" => "Token is valid. Opening the main experience.",
                "valid_desc_with_expiry" => "Token is valid until {0}.",
                "choose_title" => "Choose access method",
                "choose_desc" => "You can scan a QR code or register a package to enter the app.",
                "choose_desc_with_reason" => "{0} You can scan a QR code or register a package to enter the app.",
                "qr_title" => "Scan QR",
                "qr_desc" => "Scan a QR code on this device to receive an access token and unlock content.",
                "package_title" => "Register package",
                "package_desc" => "Choose a service package, open the QR payment page, then use bypass to receive a login QR token by email.",
                _ => key
            },
            "ko" => key switch
            {
                "badge" => "Access Flow",
                "opening_title" => "앱 여는 중",
                "opening_desc" => "시스템 진입 전에 로컬 access token을 확인하는 중입니다.",
                "valid_title" => "유효한 토큰",
                "valid_desc" => "토큰이 유효합니다. 메인 화면으로 이동합니다.",
                "valid_desc_with_expiry" => "토큰은 {0} 까지 유효합니다.",
                "choose_title" => "접속 방법 선택",
                "choose_desc" => "QR을 스캔하거나 패키지를 등록하여 앱에 들어갈 수 있습니다.",
                "choose_desc_with_reason" => "{0} QR을 스캔하거나 패키지를 등록하여 앱에 들어갈 수 있습니다.",
                "qr_title" => "QR 스캔",
                "qr_desc" => "이 기기에서 QR 코드를 스캔해 access token을 받고 콘텐츠를 엽니다.",
                "package_title" => "패키지 등록",
                "package_desc" => "서비스 패키지를 선택하고 QR 결제 페이지로 이동한 뒤 bypass로 로그인 QR 토큰을 이메일로 받습니다.",
                _ => key
            },
            "ja" => key switch
            {
                "badge" => "Access Flow",
                "opening_title" => "アプリを開いています",
                "opening_desc" => "システムに入る前にローカルのaccess tokenを確認しています。",
                "valid_title" => "有効なトークン",
                "valid_desc" => "トークンは有効です。メイン画面を開きます。",
                "valid_desc_with_expiry" => "トークンの有効期限は {0} です。",
                "choose_title" => "アクセス方法を選択",
                "choose_desc" => "QRをスキャンするか、パッケージ登録でアプリに入れます。",
                "choose_desc_with_reason" => "{0} QRをスキャンするか、パッケージ登録でアプリに入れます。",
                "qr_title" => "QRスキャン",
                "qr_desc" => "この端末でQRコードを読み取り、access tokenを受け取ってコンテンツを開きます。",
                "package_title" => "パッケージ登録",
                "package_desc" => "サービスパッケージを選び、QR決済ページへ進んで bypass でログイン用QRトークンをメール受信します。",
                _ => key
            },
            _ => key switch
            {
                "badge" => "Access Flow",
                "opening_title" => "Du khách mở app",
                "opening_desc" => "Đang kiểm tra access token local trước khi vào hệ thống.",
                "valid_title" => "Token hợp lệ",
                "valid_desc" => "Token hợp lệ, đang vào chức năng chính.",
                "valid_desc_with_expiry" => "Token còn hạn đến {0}.",
                "choose_title" => "Chọn cách truy cập",
                "choose_desc" => "Bạn có thể quét QR hoặc đăng ký gói dịch vụ để vào app.",
                "choose_desc_with_reason" => "{0} Bạn có thể quét QR hoặc đăng ký gói dịch vụ để vào app.",
                "qr_title" => "Quét QR",
                "qr_desc" => "Quét mã QR trên thiết bị để nhận access token và mở khoá nội dung.",
                "package_title" => "Đăng ký gói",
                "package_desc" => "Chọn gói dịch vụ, mở trang thanh toán QR và dùng bypass để nhận QR token đăng nhập qua email.",
                _ => key
            }
        };
    }

    private static View BuildOptionCard(out Label titleLabel, out Label descriptionLabel, Func<Task> onTap)
    {
        titleLabel = new Label
        {
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#111111")
        };

        descriptionLabel = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#6B7280"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        var card = new Border
        {
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#F0E6DC"),
            BackgroundColor = Colors.White,
            StrokeShape = new RoundRectangle { CornerRadius = 20 },
            Padding = new Thickness(16),
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    titleLabel,
                    descriptionLabel
                }
            }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, __) => await onTap();
        card.GestureRecognizers.Add(tap);

        return card;
    }
}
