using MauiApp1.Controls;
using MauiApp1.Services;
using Microsoft.Maui.Controls.Shapes;

namespace MauiApp1.Views;

public class SettingsPage : ContentPage
{
    private readonly LocalizationService _loc;
    private readonly AccessFlowService _accessFlowService;
    private Label _titleLabel = null!;
    private Label _langSectionLabel = null!;
    private Label _qrSectionLabel = null!;
    private Label _qrSectionDescLabel = null!;
    private Label _resetSectionLabel = null!;
    private Label _resetSectionDescLabel = null!;
    private Label _resetActionLabel = null!;
    private Label _deleteTokenSectionLabel = null!;
    private Label _deleteTokenSectionDescLabel = null!;
    private Label _deleteTokenActionLabel = null!;
    private HorizontalStackLayout _chipGrid = null!;

    private static readonly (string Code, string NativeName)[] _languages =
    {
        ("system", "Theo hệ thống"),
        ("vi", "Tiếng Việt"),
        ("en", "English"),
        ("ko", "한국어"),
        ("ja", "日本語")
    };

    public SettingsPage(LocalizationService localizationService, AccessFlowService accessFlowService)
    {
        _loc = localizationService;
        _accessFlowService = accessFlowService;

        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Color.FromArgb("#FFF7F1");
        SafeAreaEdges = SafeAreaEdges.None;

        BuildContent();

        localizationService.LanguageChanged += () => MainThread.BeginInvokeOnMainThread(UpdateLocalizedText);
        UpdateLocalizedText();
    }

    private void BuildContent()
    {
        _titleLabel = new Label
        {
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#111111"),
            Margin = new Thickness(0, 0, 0, 4)
        };

        _langSectionLabel = new Label
        {
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#111111")
        };

        _chipGrid = new HorizontalStackLayout
        {
            Spacing = 8
        };

        RenderLanguageChips();

        var languageChipsScroll = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = _chipGrid
        };

        var languageCard = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Stroke = Color.FromArgb("#F0E6DC"),
            BackgroundColor = Colors.White,
            Padding = new Thickness(16, 14),
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children = { _langSectionLabel, languageChipsScroll }
            }
        };

        _qrSectionLabel = new Label
        {
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#111111")
        };

        _qrSectionDescLabel = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#6B7280"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        var qrCard = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Stroke = Color.FromArgb("#F0E6DC"),
            BackgroundColor = Colors.White,
            Padding = new Thickness(16, 14),
            Content = BuildQrAccessRow()
        };

        _resetSectionLabel = new Label
        {
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#111111")
        };

        _resetSectionDescLabel = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#6B7280"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        var resetCard = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Stroke = Color.FromArgb("#F0E6DC"),
            BackgroundColor = Colors.White,
            Padding = new Thickness(16, 14),
            Content = BuildResetAccessRow()
        };

        _deleteTokenSectionLabel = new Label
        {
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#111111")
        };

        _deleteTokenSectionDescLabel = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#6B7280"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        var deleteTokenCard = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Stroke = Color.FromArgb("#F0E6DC"),
            BackgroundColor = Colors.White,
            Padding = new Thickness(16, 14),
            Content = BuildDeleteTokenRow()
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
                Padding = new Thickness(16, 52, 16, 28),
                Children = { _titleLabel, languageCard, resetCard, deleteTokenCard }
            }
        };

        root.Children.Add(scroll);

        var footer = new AppBottomBar(
            BottomBarTab.Settings,
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
            onTourTap: async () =>
            {
                if (Application.Current is App app)
                    await app.ShowTourPageAsync();
            });
        root.Children.Add(footer);
        Grid.SetRow(footer, 1);

        Content = root;
    }

    private void RenderLanguageChips()
    {
        _chipGrid.Children.Clear();

        foreach (var (code, nativeName) in _languages)
        {
            var isSelected = IsLanguageChipSelected(code);
            _chipGrid.Children.Add(BuildLanguageChip(code, nativeName, isSelected));
        }
    }

    private bool IsLanguageChipSelected(string code)
    {
        var mode = Preferences.Get(LocalizationService.LanguageModePreferenceKey, LocalizationService.SystemLanguageMode);

        if (code.Equals("system", StringComparison.OrdinalIgnoreCase))
            return mode.Equals(LocalizationService.SystemLanguageMode, StringComparison.OrdinalIgnoreCase);

        return mode.Equals(LocalizationService.ManualLanguageMode, StringComparison.OrdinalIgnoreCase) &&
               code.Equals(_loc.CurrentLanguage, StringComparison.OrdinalIgnoreCase);
    }

    private View BuildLanguageChip(string code, string nativeName, bool selected)
    {
        var chip = new Border
        {
            Stroke = selected ? Color.FromArgb("#FF6B00") : Color.FromArgb("#E5E7EB"),
            StrokeThickness = 1,
            BackgroundColor = selected ? Color.FromArgb("#FFF1E6") : Colors.White,
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
            Padding = new Thickness(14, 8),
            Content = new Label
            {
                Text = nativeName,
                FontSize = 13,
                FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None,
                TextColor = selected ? Color.FromArgb("#9A3412") : Color.FromArgb("#374151")
            }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, __) => OnLanguageSelected(code);
        chip.GestureRecognizers.Add(tap);

        return chip;
    }

    private void OnLanguageSelected(string code)
    {
        if (code.Equals("system", StringComparison.OrdinalIgnoreCase))
        {
            var systemLanguage = LocalizationService.DetectSystemLanguage();
            Preferences.Set(LocalizationService.LanguageModePreferenceKey, LocalizationService.SystemLanguageMode);
            Preferences.Set(LocalizationService.LanguagePreferenceKey, systemLanguage);
            _loc.SetLanguage(systemLanguage);
            RenderLanguageChips();
            return;
        }

        var normalizedCode = LocalizationService.NormalizeLanguageCode(code);
        var mode = Preferences.Get(LocalizationService.LanguageModePreferenceKey, LocalizationService.SystemLanguageMode);
        if (mode.Equals(LocalizationService.ManualLanguageMode, StringComparison.OrdinalIgnoreCase) &&
            normalizedCode.Equals(_loc.CurrentLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Preferences.Set(LocalizationService.LanguageModePreferenceKey, LocalizationService.ManualLanguageMode);
        Preferences.Set(LocalizationService.LanguagePreferenceKey, normalizedCode);
        _loc.SetLanguage(normalizedCode);
        RenderLanguageChips();
    }

    private void UpdateLocalizedText()
    {
        Title = _loc.Get("settings_title");
        _titleLabel.Text = _loc.Get("settings_title");
        _langSectionLabel.Text = _loc.Get("settings_language_title");
        _qrSectionLabel.Text = GetQrLabel();
        _qrSectionDescLabel.Text = GetQrDescription();
        _resetSectionLabel.Text = GetResetLabel();
        _resetSectionDescLabel.Text = GetResetDescription();
        _resetActionLabel.Text = GetDeleteActionText();
        _deleteTokenSectionLabel.Text = GetDeleteTokenLabel();
        _deleteTokenSectionDescLabel.Text = GetDeleteTokenDescription();
        _deleteTokenActionLabel.Text = GetDeleteActionText();
        RenderLanguageChips();
    }

    private View BuildQrAccessRow()
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        row.Children.Add(new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#FFF1E6"),
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Padding = new Thickness(12),
            WidthRequest = 48,
            HeightRequest = 48,
            Content = new Label
            {
                Text = "QR",
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Color.FromArgb("#9A3412")
            }
        });

        var textWrap = new VerticalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                _qrSectionLabel,
                _qrSectionDescLabel
            }
        };
        row.Children.Add(textWrap);
        row.SetColumn(textWrap, 1);

        var arrowLabel = new Label
        {
            Text = ">",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#94A3B8"),
            VerticalTextAlignment = TextAlignment.Center
        };
        row.Children.Add(arrowLabel);
        row.SetColumn(arrowLabel, 2);

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, __) =>
        {
            var qrPage = App.Current?.Handler?.MauiContext?.Services.GetRequiredService<QrScanPage>();
            if (qrPage != null)
                await Navigation.PushAsync(qrPage);
        };
        row.GestureRecognizers.Add(tap);

        return row;
    }

    private View BuildResetAccessRow()
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        row.Children.Add(new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#FFF1E6"),
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Padding = new Thickness(12),
            WidthRequest = 48,
            HeightRequest = 48,
            Content = new Label
            {
                Text = "X",
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Color.FromArgb("#9A3412")
            }
        });

        var textWrap = new VerticalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                _resetSectionLabel,
                _resetSectionDescLabel
            }
        };
        row.Children.Add(textWrap);
        row.SetColumn(textWrap, 1);

        _resetActionLabel = new Label
        {
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#DC2626"),
            VerticalTextAlignment = TextAlignment.Center
        };
        row.Children.Add(_resetActionLabel);
        row.SetColumn(_resetActionLabel, 2);

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, __) => await ResetAccessAsync();
        row.GestureRecognizers.Add(tap);

        return row;
    }

    private View BuildDeleteTokenRow()
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        row.Children.Add(new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#FEF2F2"),
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Padding = new Thickness(12),
            WidthRequest = 48,
            HeightRequest = 48,
            Content = new Label
            {
                Text = "T",
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Color.FromArgb("#B91C1C")
            }
        });

        var textWrap = new VerticalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                _deleteTokenSectionLabel,
                _deleteTokenSectionDescLabel
            }
        };
        row.Children.Add(textWrap);
        row.SetColumn(textWrap, 1);

        _deleteTokenActionLabel = new Label
        {
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#B91C1C"),
            VerticalTextAlignment = TextAlignment.Center
        };
        row.Children.Add(_deleteTokenActionLabel);
        row.SetColumn(_deleteTokenActionLabel, 2);

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, __) => await DeleteTokenOnDeviceAsync();
        row.GestureRecognizers.Add(tap);

        return row;
    }

    private async Task ResetAccessAsync()
    {
        var confirmed = await DisplayAlertAsync(
            GetResetConfirmTitle(),
            GetResetConfirmMessage(),
            GetDeleteTokenConfirmButtonText(),
            GetCancelText());

        if (!confirmed)
            return;

        await _accessFlowService.ClearAccessAsync();

        if (Application.Current is App app)
            await app.ShowAccessEntryAsync();
    }

    private async Task DeleteTokenOnDeviceAsync()
    {
        var confirmed = await DisplayAlertAsync(
            GetDeleteTokenConfirmTitle(),
            GetDeleteTokenConfirmMessage(),
            GetDeleteTokenConfirmButtonText(),
            GetCancelText());

        if (!confirmed)
            return;

        await _accessFlowService.ClearAccessAsync();
        await DisplayAlertAsync(GetDoneTitle(), GetDeleteTokenDoneMessage(), _loc.Get("alert_ok"));
    }

    private string GetBackendLabel()
    {
        return _loc.CurrentLanguage switch
        {
            "en" => "Backend URL",
            "ko" => "백엔드 URL",
            "ja" => "バックエンドURL",
            _ => "URL backend"
        };
    }

    private string GetBackendDescription()
    {
        return _loc.CurrentLanguage switch
        {
            "en" => "Use this when Android cannot reach localhost. Emulator usually uses https://10.0.2.2:7123/, while a real device should use your PC LAN IP.",
            "ko" => "Android가 localhost에 접근하지 못할 때 사용합니다. 에뮬레이터는 보통 https://10.0.2.2:7123/ 를, 실제 기기는 PC의 LAN IP를 사용해야 합니다.",
            "ja" => "Android が localhost に接続できない場合に使います。エミュレーターは通常 https://10.0.2.2:7123/、実機は PC の LAN IP を使ってください。",
            _ => "Dùng khi Android không truy cập được localhost. Emulator thường dùng https://10.0.2.2:7123/, còn máy thật cần dùng IP LAN của máy tính."
        };
    }

    private string GetBackendPlaceholder()
    {
        return _loc.CurrentLanguage switch
        {
            "en" => "https://192.168.x.x:7123/",
            _ => "https://192.168.x.x:7123/"
        };
    }

    private string GetBackendSaveText()
    {
        return _loc.CurrentLanguage switch
        {
            "en" => "Save backend URL",
            "ko" => "백엔드 URL 저장",
            "ja" => "バックエンドURLを保存",
            _ => "Lưu URL backend"
        };
    }

    private string GetBackendHint()
    {
        return _loc.CurrentLanguage switch
        {
            "en" => "Leave blank to use the default URL for the current platform.",
            "ko" => "비워 두면 현재 플랫폼의 기본 URL을 사용합니다.",
            "ja" => "空欄のままなら現在のプラットフォーム既定URLを使います。",
            _ => "Để trống để dùng URL mặc định theo từng nền tảng."
        };
    }

    private string GetBackendSavedMessage()
    {
        return _loc.CurrentLanguage switch
        {
            "en" => "Saved backend URL: {0}",
            "ko" => "백엔드 URL 저장됨: {0}",
            "ja" => "バックエンドURLを保存しました: {0}",
            _ => "Đã lưu URL backend: {0}"
        };
    }

    private string GetBackendClearedMessage()
    {
        return _loc.CurrentLanguage switch
        {
            "en" => "Removed custom backend URL. The app will use the platform default.",
            "ko" => "사용자 지정 백엔드 URL을 제거했습니다. 앱은 플랫폼 기본값을 사용합니다.",
            "ja" => "カスタムのバックエンドURLを削除しました。アプリは既定値を使います。",
            _ => "Đã xoá URL backend tuỳ chỉnh. App sẽ quay về URL mặc định."
        };
    }

    private string GetBackendInvalidMessage()
    {
        return _loc.CurrentLanguage switch
        {
            "en" => "Invalid URL. Use a full http:// or https:// address.",
            "ko" => "잘못된 URL입니다. http:// 또는 https:// 전체 주소를 입력하세요.",
            "ja" => "無効なURLです。http:// または https:// から始まる完全なURLを入力してください。",
            _ => "URL không hợp lệ. Hãy nhập đầy đủ địa chỉ http:// hoặc https://."
        };
    }

    private string GetQrLabel()
    {
        return _loc.CurrentLanguage switch
        {
            "en" => "Scan QR",
            "ko" => "QR 스캔",
            "ja" => "QRをスキャン",
            _ => "Quét QR"
        };
    }

    private string GetQrDescription()
    {
        return _loc.CurrentLanguage switch
        {
            "en" => "Open the QR scanner to activate access from a login token.",
            "ko" => "로그인 QR 토큰으로 접근 권한을 활성화하려면 스캐너를 엽니다.",
            "ja" => "ログイン用QRトークンからアクセスを有効化するにはスキャナーを開きます。",
            _ => "Mở màn hình quét QR để truy cập bằng QR token đăng nhập."
        };
    }

    private string GetResetLabel()
    {
        return _loc.CurrentLanguage switch
        {
            "en" => "Reset access",
            "ko" => "접근 초기화",
            "ja" => "アクセスをリセット",
            _ => "Reset truy cập"
        };
    }

    private string GetResetDescription()
    {
        return _loc.CurrentLanguage switch
        {
            "en" => "Clear the local token so you can test package purchase and QR token login again.",
            "ko" => "로컬 토큰을 지워 패키지 구매와 QR 로그인 흐름을 다시 테스트할 수 있습니다.",
            "ja" => "ローカルトークンを削除して、パッケージ購入とQRログインの流れを再テストできます。",
            _ => "Xoá token local để bạn test lại luồng chọn gói, thanh toán và QR token đăng nhập."
        };
    }

    private string GetDeleteTokenLabel()
    {
        return _loc.CurrentLanguage switch
        {
            "en" => "Delete token on device",
            "ko" => "기기 토큰 삭제",
            "ja" => "端末のトークンを削除",
            _ => "Xoá token trên máy"
        };
    }

    private string GetDeleteTokenDescription()
    {
        return _loc.CurrentLanguage switch
        {
            "en" => "Delete only the local token on this device without changing server data.",
            "ko" => "서버 데이터는 건드리지 않고 이 기기의 로컬 토큰만 삭제합니다.",
            "ja" => "サーバーデータは変更せず、この端末のローカルトークンだけ削除します。",
            _ => "Chỉ xoá token local trên máy này, không động vào dữ liệu token ở backend."
        };
    }

    private string GetDeleteActionText()
    {
        return _loc.CurrentLanguage switch
        {
            "en" => "Delete",
            "ko" => "삭제",
            "ja" => "削除",
            _ => "Xoá"
        };
    }

    private string GetResetConfirmTitle()
    {
        return _loc.CurrentLanguage switch
        {
            "en" => "Reset access",
            "ko" => "접근 초기화",
            "ja" => "アクセスをリセット",
            _ => "Reset truy cập"
        };
    }

    private string GetResetConfirmMessage()
    {
        return _loc.CurrentLanguage switch
        {
            "en" => "Remove the current access token and return to the screen for choosing a package or scanning a QR code?",
            "ko" => "현재 접근 토큰을 지우고 패키지 선택 또는 QR 스캔 화면으로 돌아가시겠습니까?",
            "ja" => "現在のアクセストークンを削除して、パッケージ選択またはQRスキャン画面に戻りますか？",
            _ => "Xoá token truy cập hiện tại để quay lại màn hình chọn gói và quét QR?"
        };
    }

    private string GetDeleteTokenConfirmTitle()
    {
        return _loc.CurrentLanguage switch
        {
            "en" => "Delete token on device",
            "ko" => "기기 토큰 삭제",
            "ja" => "端末のトークンを削除",
            _ => "Xoá token trên máy"
        };
    }

    private string GetDeleteTokenConfirmMessage()
    {
        return _loc.CurrentLanguage switch
        {
            "en" => "Delete only the local access token on this device so you can test activation again?",
            "ko" => "이 기기의 로컬 접근 토큰만 삭제해서 활성화 흐름을 다시 테스트하시겠습니까?",
            "ja" => "この端末のローカルアクセストークンだけ削除して、再度アクティベーションを試しますか？",
            _ => "Chỉ xoá access token local trên thiết bị này để bạn test lại luồng kích hoạt?"
        };
    }

    private string GetDeleteTokenConfirmButtonText()
    {
        return _loc.CurrentLanguage switch
        {
            "en" => "Delete token",
            "ko" => "토큰 삭제",
            "ja" => "トークンを削除",
            _ => "Xoá token"
        };
    }

    private string GetCancelText()
    {
        return _loc.CurrentLanguage switch
        {
            "en" => "Cancel",
            "ko" => "취소",
            "ja" => "キャンセル",
            _ => "Huỷ"
        };
    }

    private string GetDoneTitle()
    {
        return _loc.CurrentLanguage switch
        {
            "en" => "Done",
            "ko" => "완료",
            "ja" => "完了",
            _ => "Hoàn tất"
        };
    }

    private string GetDeleteTokenDoneMessage()
    {
        return _loc.CurrentLanguage switch
        {
            "en" => "The token on this device has been removed.",
            "ko" => "이 기기의 토큰을 삭제했습니다.",
            "ja" => "この端末のトークンを削除しました。",
            _ => "Đã xoá token trên máy này."
        };
    }
}
