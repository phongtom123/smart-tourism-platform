using Microsoft.Maui.Controls.Shapes;
using MauiApp1.Models;
using MauiApp1.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZXing.Net.Maui.Controls;
using MauiColor = Microsoft.Maui.Graphics.Color;

namespace MauiApp1.Views;

public class PackageQrPaymentPage : ContentPage
{
    private readonly AccessFlowService _accessFlowService;
    private readonly LocalizationService _loc;
    private readonly IImageGallerySaver? _gallerySaver;
    private readonly PackagePlanOption _plan;
    private readonly Entry _emailEntry;
    private readonly CheckBox _bypassCheckBox;
    private readonly Button _bypassEmailButton;
    private readonly Button _bypassDownloadButton;
    private readonly VerticalStackLayout _bypassChoiceLayout;
    private readonly Label _statusLabel;
    private readonly Label _helperLabel;
    private readonly Label _paymentReferenceLabel;
    private readonly Label _paymentAmountLabel;
    private readonly BarcodeGeneratorView _paymentQrView;
    private readonly VerticalStackLayout _successLayout;
    private bool _isSubmitting;
    private bool _isCheckingPayment;
    private bool _hasActivated;
    private bool _paymentInitialized;
    private IDispatcherTimer? _paymentStatusTimer;
    private string _paymentReference = string.Empty;
    private static readonly TimeSpan PaymentStatusPollInterval = TimeSpan.FromSeconds(5);

    public PackageQrPaymentPage(AccessFlowService accessFlowService, LocalizationService localizationService, PackagePlanOption plan, string email, IImageGallerySaver? gallerySaver = null)
    {
        _accessFlowService = accessFlowService;
        _loc = localizationService;
        _gallerySaver = gallerySaver;
        _plan = plan;

        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = MauiColor.FromArgb("#FFF7F1");
        Title = string.Empty;

        _statusLabel = new Label
        {
            Text = GetText("status_waiting"),
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = MauiColor.FromArgb("#9A3412")
        };

        _helperLabel = new Label
        {
            Text = GetText("helper_waiting"),
            FontSize = 13,
            TextColor = MauiColor.FromArgb("#6B7280"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        _emailEntry = new Entry
        {
            Placeholder = GetText("email_placeholder"),
            Keyboard = Keyboard.Email,
            Text = email ?? string.Empty,
            BackgroundColor = Colors.White,
            TextColor = MauiColor.FromArgb("#111111"),
            PlaceholderColor = MauiColor.FromArgb("#94A3B8")
        };
        _emailEntry.TextChanged += (_, __) => RefreshBypassButtons();

        _paymentReferenceLabel = new Label
        {
            Text = string.Empty,
            FontSize = 12,
            TextColor = MauiColor.FromArgb("#334155"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        _paymentAmountLabel = new Label
        {
            Text = string.Empty,
            FontSize = 12,
            TextColor = MauiColor.FromArgb("#334155"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        _paymentQrView = new BarcodeGeneratorView
        {
            Value = "CSAT-PAYMENT-LOADING",
            Format = ZXing.Net.Maui.BarcodeFormat.QrCode,
            WidthRequest = 220,
            HeightRequest = 220,
            ForegroundColor = Colors.Black
        };

        _bypassEmailButton = new Button
        {
            Text = GetText("bypass_email"),
            BackgroundColor = MauiColor.FromArgb("#F97316"),
            TextColor = Colors.White,
            CornerRadius = 16,
            Padding = new Thickness(14, 12),
            HorizontalOptions = LayoutOptions.FillAndExpand,
            IsEnabled = IsValidEmail(_emailEntry.Text)
        };
        _bypassEmailButton.Clicked += async (_, __) => await ConfirmBypassAsync(sendEmail: true);

        _bypassDownloadButton = new Button
        {
            Text = GetText("bypass_download"),
            BackgroundColor = MauiColor.FromArgb("#1D4ED8"),
            TextColor = Colors.White,
            CornerRadius = 16,
            Padding = new Thickness(14, 12),
            HorizontalOptions = LayoutOptions.FillAndExpand
        };
        _bypassDownloadButton.Clicked += async (_, __) => await ConfirmBypassAsync(sendEmail: false);

        _bypassChoiceLayout = new VerticalStackLayout
        {
            Spacing = 10,
            IsVisible = false,
            Children =
            {
                new Label
                {
                    Text = GetText("email_section"),
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = MauiColor.FromArgb("#111111")
                },
                _emailEntry,
                new Label
                {
                    Text = GetText("choice_hint"),
                    FontSize = 12,
                    TextColor = MauiColor.FromArgb("#6B7280"),
                    LineBreakMode = LineBreakMode.WordWrap
                },
                _bypassEmailButton,
                _bypassDownloadButton
            }
        };

        _bypassCheckBox = new CheckBox
        {
            Color = MauiColor.FromArgb("#F97316")
        };
        _bypassCheckBox.CheckedChanged += (_, args) =>
        {
            _bypassChoiceLayout.IsVisible = args.Value && !_hasActivated;
        };

        _successLayout = new VerticalStackLayout
        {
            Spacing = 12,
            IsVisible = false
        };

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

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 18,
                Padding = new Thickness(16, 52, 16, 28),
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
                    BuildCard(
                        new VerticalStackLayout
                        {
                            Spacing = 8,
                            Children =
                            {
                                new Label
                                {
                                    Text = _plan.Name,
                                    FontSize = 18,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = MauiColor.FromArgb("#111111")
                                },
                                new Label
                                {
                                    Text = string.Format(GetText("price_line"), _plan.Price, _plan.DurationDays),
                                    FontSize = 13,
                                    TextColor = MauiColor.FromArgb("#6B7280")
                                }
                            }
                        }),
                    BuildCard(
                        new VerticalStackLayout
                        {
                            Spacing = 12,
                            Children =
                            {
                                new Label
                                {
                                    Text = GetText("payment_qr"),
                                    FontSize = 15,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = MauiColor.FromArgb("#111111")
                                },
                                new Border
                                {
                                    StrokeThickness = 1,
                                    Stroke = MauiColor.FromArgb("#F0E6DC"),
                                    BackgroundColor = Colors.White,
                                    StrokeShape = new RoundRectangle { CornerRadius = 18 },
                                    Padding = new Thickness(18),
                                    HorizontalOptions = LayoutOptions.Center,
                                    Content = _paymentQrView
                                },
                                _paymentReferenceLabel,
                                _paymentAmountLabel,
                                _statusLabel,
                                _helperLabel,
                                new HorizontalStackLayout
                                {
                                    Spacing = 10,
                                    Children =
                                    {
                                        _bypassCheckBox,
                                        new Label
                                        {
                                            Text = GetText("bypass_hint"),
                                            FontSize = 13,
                                            TextColor = MauiColor.FromArgb("#334155"),
                                            VerticalTextAlignment = TextAlignment.Center
                                        }
                                    }
                                },
                                _bypassChoiceLayout
                            }
                        }),
                    _successLayout
                }
            }
        };

        Loaded += async (_, __) => await InitializePaymentAsync();
    }

    private void RefreshBypassButtons()
    {
        _bypassEmailButton.IsEnabled = !_isSubmitting && !_hasActivated && IsValidEmail(_emailEntry.Text);
    }

    private async Task InitializePaymentAsync()
    {
        if (_paymentInitialized)
            return;

        _paymentInitialized = true;
        _statusLabel.Text = GetText("status_creating_payment");
        _helperLabel.Text = GetText("helper_creating_payment");

        var result = await _accessFlowService.CreatePackagePaymentAsync(string.Empty, _plan.BackendPackageId, sendEmail: false);
        if (!result.Success || string.IsNullOrWhiteSpace(result.PaymentQrPayload))
        {
            _statusLabel.Text = GetText("status_failed");
            _helperLabel.Text = result.Message;
            return;
        }

        _paymentReference = result.PaymentReference ?? string.Empty;
        _paymentQrView.Value = result.PaymentQrPayload;
        _paymentReferenceLabel.Text = string.Format(GetText("payment_reference"), result.PaymentContent ?? _paymentReference);
        _paymentAmountLabel.Text = string.Format(GetText("payment_amount"), result.Amount);
        _statusLabel.Text = GetText("status_waiting");
        _helperLabel.Text = GetText("helper_auto_waiting");
        StartPaymentStatusPolling();
        await CheckPaymentAsync(showPendingAlert: false);
    }

    private async Task CheckPaymentAsync(bool showPendingAlert)
    {
        if (_isSubmitting || _isCheckingPayment || _hasActivated || string.IsNullOrWhiteSpace(_paymentReference))
            return;

        _isCheckingPayment = true;
        try
        {
            if (showPendingAlert)
            {
                _statusLabel.Text = GetText("status_checking_payment");
                _helperLabel.Text = GetText("helper_checking_payment");
            }

            var result = await _accessFlowService.ConfirmPackagePaymentAsync(_paymentReference, string.Empty, _plan.BackendPackageId);
            if (!result.Success)
            {
                _statusLabel.Text = GetText("status_waiting");
                _helperLabel.Text = showPendingAlert ? result.Message : GetText("helper_auto_waiting");
                if (showPendingAlert)
                    await DisplayAlertAsync(GetText("pending_title"), result.Message, _loc.Get("alert_ok"));
                return;
            }

            StopPaymentStatusPolling();
            _hasActivated = true;
            _statusLabel.Text = GetText("status_activated");
            _helperLabel.Text = result.EmailSent
                ? GetText("helper_email_sent")
                : GetText("helper_qr_ready");

            ShowSuccess(result);

            var payload = result.QrTokenPayload ?? result.AccessToken;
            if (!string.IsNullOrWhiteSpace(payload))
                await SaveAndShareQrAsync(payload);
        }
        finally
        {
            _isCheckingPayment = false;
        }
    }

    private void StartPaymentStatusPolling()
    {
        StopPaymentStatusPolling();

        _paymentStatusTimer = Dispatcher.CreateTimer();
        _paymentStatusTimer.Interval = PaymentStatusPollInterval;
        _paymentStatusTimer.IsRepeating = true;
        _paymentStatusTimer.Tick += OnPaymentStatusTimerTick;
        _paymentStatusTimer.Start();
    }

    private void StopPaymentStatusPolling()
    {
        if (_paymentStatusTimer is null)
            return;

        _paymentStatusTimer.Stop();
        _paymentStatusTimer.Tick -= OnPaymentStatusTimerTick;
        _paymentStatusTimer = null;
    }

    private async void OnPaymentStatusTimerTick(object? sender, EventArgs e)
    {
        await CheckPaymentAsync(showPendingAlert: false);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_paymentInitialized && !_hasActivated && !string.IsNullOrWhiteSpace(_paymentReference))
            StartPaymentStatusPolling();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopPaymentStatusPolling();
    }

    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;
        return email.Contains('@') && email.Contains('.');
    }

    private async Task ConfirmBypassAsync(bool sendEmail)
    {
        if (_isSubmitting || _hasActivated)
            return;

        StopPaymentStatusPolling();
        var email = _emailEntry.Text?.Trim() ?? string.Empty;
        if (sendEmail && !IsValidEmail(email))
        {
            await DisplayAlertAsync(GetText("failed_title"), GetText("invalid_email"), _loc.Get("alert_ok"));
            if (!string.IsNullOrWhiteSpace(_paymentReference))
                StartPaymentStatusPolling();
            return;
        }

        _isSubmitting = true;
        _bypassEmailButton.IsEnabled = false;
        _bypassDownloadButton.IsEnabled = false;

        var activated = false;
        try
        {
            _statusLabel.Text = GetText("status_generating");
            _helperLabel.Text = sendEmail ? GetText("helper_generating_email") : GetText("helper_generating_download");

            var result = await _accessFlowService.RegisterPackageAccessBypassAsync(email, _plan.BackendPackageId, sendEmail);
            if (!result.Success)
            {
                _statusLabel.Text = GetText("status_failed");
                _helperLabel.Text = result.Message;
                await DisplayAlertAsync(GetText("failed_title"), result.Message, _loc.Get("alert_ok"));
                return;
            }

            activated = true;
            _statusLabel.Text = GetText("status_activated");
            _helperLabel.Text = result.EmailSent
                ? GetText("helper_email_sent")
                : GetText("helper_qr_ready");

            ShowSuccess(result);

            if (!sendEmail)
            {
                var payload = result.QrTokenPayload ?? result.AccessToken;
                if (!string.IsNullOrWhiteSpace(payload))
                    await SaveAndShareQrAsync(payload);
            }
        }
        finally
        {
            _isSubmitting = false;
            if (activated)
            {
                _hasActivated = true;
                _bypassChoiceLayout.IsVisible = false;
            }
            else
            {
                _bypassDownloadButton.IsEnabled = true;
                RefreshBypassButtons();
                if (!string.IsNullOrWhiteSpace(_paymentReference))
                    StartPaymentStatusPolling();
            }
        }
    }

    private void ShowSuccess(PackageAccessActivationState result)
    {
        _successLayout.Children.Clear();
        _successLayout.IsVisible = true;

        var hasEmail = !string.IsNullOrWhiteSpace(result.Email);

        var gmailButton = new Button
        {
            Text = result.EmailSent ? GetText("gmail_sent") : GetText("gmail_open"),
            BackgroundColor = Colors.White,
            TextColor = MauiColor.FromArgb("#0F766E"),
            BorderColor = MauiColor.FromArgb("#CCFBF1"),
            BorderWidth = 1,
            CornerRadius = 14,
            IsEnabled = hasEmail && !result.EmailSent,
            IsVisible = hasEmail
        };
        gmailButton.Clicked += async (_, __) =>
        {
            var subject = Uri.EscapeDataString(GetText("gmail_subject"));
            var expiresText = result.ExpiresAtUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? string.Empty;
            var body = Uri.EscapeDataString(
                $"{string.Format(GetText("email_line"), result.Email)}\n{string.Format(GetText("package_line"), result.PackageName)}\n{string.Format(GetText("token_line"), result.AccessToken)}\n{string.Format(GetText("payload_line"), result.QrTokenPayload)}\n{string.Format(GetText("expires_line"), expiresText)}");
            await Launcher.Default.OpenAsync(new Uri($"mailto:{result.Email}?subject={subject}&body={body}"));
        };

        var saveQrButton = new Button
        {
            Text = GetText("save_qr"),
            BackgroundColor = Colors.White,
            TextColor = MauiColor.FromArgb("#1D4ED8"),
            BorderColor = MauiColor.FromArgb("#BFDBFE"),
            BorderWidth = 1,
            CornerRadius = 14
        };
        saveQrButton.Clicked += async (_, __) =>
        {
            saveQrButton.IsEnabled = false;
            try
            {
                var payload = result.QrTokenPayload ?? result.AccessToken;
                await SaveAndShareQrAsync(payload);
            }
            finally
            {
                saveQrButton.IsEnabled = true;
            }
        };

        var enterButton = new Button
        {
            Text = GetText("enter_app"),
            BackgroundColor = MauiColor.FromArgb("#F97316"),
            TextColor = Colors.White,
            CornerRadius = 16
        };
        enterButton.Clicked += async (_, __) =>
        {
            if (Application.Current is App app)
                await app.ShowMainPageAsync();
        };

        var infoText = hasEmail
            ? $"{string.Format(GetText("package_line"), result.PackageName)}\n{string.Format(GetText("email_line"), result.Email)}\n{string.Format(GetText("expires_line"), result.ExpiresAtUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? string.Empty)}"
            : $"{string.Format(GetText("package_line"), result.PackageName)}\n{string.Format(GetText("expires_line"), result.ExpiresAtUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? string.Empty)}";

        var statusHint = hasEmail
            ? (result.EmailSent ? GetText("email_backend_sent") : (result.EmailStatusMessage ?? GetText("email_manual_hint")))
            : GetText("download_only_hint");

        _successLayout.Children.Add(
            BuildCard(
                new VerticalStackLayout
                {
                    Spacing = 12,
                    Children =
                    {
                        new Label
                        {
                            Text = GetText("success_title"),
                            FontSize = 18,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = MauiColor.FromArgb("#111111")
                        },
                        new Label
                        {
                            Text = infoText,
                            FontSize = 13,
                            TextColor = MauiColor.FromArgb("#6B7280")
                        },
                        new Border
                        {
                            StrokeThickness = 1,
                            Stroke = MauiColor.FromArgb("#F0E6DC"),
                            BackgroundColor = Colors.White,
                            StrokeShape = new RoundRectangle { CornerRadius = 18 },
                            Padding = new Thickness(18),
                            HorizontalOptions = LayoutOptions.Center,
                            Content = new BarcodeGeneratorView
                            {
                                Value = result.QrTokenPayload ?? result.AccessToken,
                                Format = ZXing.Net.Maui.BarcodeFormat.QrCode,
                                WidthRequest = 220,
                                HeightRequest = 220,
                                ForegroundColor = Colors.Black
                            }
                        },
                        new Label
                        {
                            Text = string.Format(GetText("payload_line"), result.QrTokenPayload),
                            FontSize = 12,
                            TextColor = MauiColor.FromArgb("#334155"),
                            LineBreakMode = LineBreakMode.WordWrap
                        },
                        new Label
                        {
                            Text = statusHint,
                            FontSize = 12,
                            TextColor = MauiColor.FromArgb("#9A3412"),
                            LineBreakMode = LineBreakMode.WordWrap
                        },
                        saveQrButton,
                        gmailButton,
                        enterButton
                    }
                }));
    }

    private async Task SaveAndShareQrAsync(string payload)
    {
        try
        {
            var writer = new ZXing.BarcodeWriterPixelData
            {
                Format = ZXing.BarcodeFormat.QR_CODE,
                Options = new ZXing.QrCode.QrCodeEncodingOptions
                {
                    Width = 512,
                    Height = 512,
                    Margin = 2,
                    ErrorCorrection = ZXing.QrCode.Internal.ErrorCorrectionLevel.M
                }
            };

            var pixelData = writer.Write(payload);

            using var image = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(
                pixelData.Pixels, pixelData.Width, pixelData.Height);

            var fileName = $"qr_vinhkhanh_{DateTime.Now:yyyyMMdd_HHmmss}.png";

            byte[] pngBytes;
            using (var ms = new MemoryStream())
            {
                await image.SaveAsPngAsync(ms);
                pngBytes = ms.ToArray();
            }

            if (_gallerySaver != null)
            {
                await _gallerySaver.SavePngToGalleryAsync(pngBytes, fileName);
                await DisplayAlertAsync(GetText("save_qr_title"), GetText("save_qr_success"), _loc.Get("alert_ok"));
            }
            else
            {
                var path = System.IO.Path.Combine(FileSystem.CacheDirectory, fileName);
                await File.WriteAllBytesAsync(path, pngBytes);
                await Share.RequestAsync(new ShareFileRequest
                {
                    Title = GetText("save_qr_title"),
                    File = new ShareFile(path, "image/png")
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(GetText("failed_title"), ex.Message, _loc.Get("alert_ok"));
        }
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

    private string GetText(string key)
    {
        return _loc.CurrentLanguage switch
        {
            "en" => key switch
            {
                "status_waiting" => "Waiting for payment confirmation",
                "status_creating_payment" => "Creating VietQR payment code...",
                "helper_creating_payment" => "The backend is creating a pending invoice with the package and this device code.",
                "status_checking_payment" => "Checking Casso payment confirmation...",
                "helper_checking_payment" => "If Casso has received the bank transfer webhook, the login QR token will be created now.",
                "helper_waiting" => "Scan this VietQR with your banking app. The transfer content contains the package code and this device code so Casso can match the webhook.",
                "helper_auto_waiting" => "Scan this VietQR with your banking app. The app will check Casso automatically and open the login QR when payment is confirmed.",
                "check_payment" => "I paid, check now",
                "pending_title" => "Payment pending",
                "payment_reference" => "Transfer content: {0}",
                "payment_amount" => "Amount: {0:N0} VND",
                "bypass_button" => "Confirm bypass",
                "back" => "Back",
                "title" => "QR payment",
                "email_section" => "Email to receive the login QR token (optional)",
                "email_placeholder" => "Enter your Gmail address (required only if you choose to receive by email)",
                "email_line" => "Email: {0}",
                "price_line" => "Price: {0:N0} VND | {1} days",
                "payment_qr" => "Payment QR code",
                "bypass_hint" => "Bypass payment to create the login QR token",
                "choice_hint" => "Choose: send QR by email (email required) or download QR directly to this device (no email needed).",
                "bypass_email" => "Send QR by email",
                "bypass_download" => "Download QR to device",
                "status_generating" => "Generating token for the selected package...",
                "helper_generating_email" => "The system will bypass payment, activate the token on this device, and send the login QR token by email.",
                "helper_generating_download" => "The system will bypass payment, activate the token on this device, and save the login QR image to your device.",
                "status_failed" => "Bypass failed",
                "failed_title" => "Failed",
                "invalid_email" => "Please enter a valid email address to receive the QR token.",
                "status_activated" => "Token activated",
                "helper_email_sent" => "The token has been activated and the login QR token email has been sent.",
                "helper_qr_ready" => "The token has been activated and the login QR token is ready.",
                "gmail_sent" => "Email sent",
                "gmail_open" => "Open Gmail to send the QR token",
                "gmail_subject" => "Vinh Khanh Smart Tourism login QR token",
                "package_line" => "Package: {0}",
                "token_line" => "Token: {0}",
                "payload_line" => "QR payload: {0}",
                "expires_line" => "Expires: {0}",
                "enter_app" => "Enter app",
                "success_title" => "Package activated successfully",
                "email_backend_sent" => "The backend sent the QR token email automatically.",
                "email_manual_hint" => "Automatic email could not be sent. You can use the Gmail button to send it manually.",
                "download_only_hint" => "You chose to download the QR directly. Keep the image safe — it is required to log in.",
                "save_qr" => "Save QR to device",
                "save_qr_title" => "QR saved",
                "save_qr_success" => "QR code saved to Pictures/VinhKhanh on your device.",
                _ => key
            },
            "ko" => key switch
            {
                "status_waiting" => "결제 확인 대기 중",
                "status_creating_payment" => "Creating VietQR payment code...",
                "helper_creating_payment" => "Creating a pending invoice with this package and device code.",
                "status_checking_payment" => "Checking Casso payment confirmation...",
                "helper_checking_payment" => "If Casso has received the webhook, the login QR token will be created now.",
                "helper_waiting" => "Scan this VietQR with your banking app. The transfer content identifies the package and device.",
                "helper_auto_waiting" => "Scan this VietQR with your banking app. The app checks Casso automatically and opens the login QR after payment.",
                "check_payment" => "I paid, check now",
                "pending_title" => "Payment pending",
                "payment_reference" => "Transfer content: {0}",
                "payment_amount" => "Amount: {0:N0} VND",
                "bypass_button" => "bypass 확인",
                "back" => "뒤로",
                "title" => "QR 결제",
                "email_section" => "로그인 QR 토큰 수신 이메일 (선택)",
                "email_placeholder" => "이메일 수신을 선택한 경우에만 필요 — Gmail 주소 입력",
                "email_line" => "이메일: {0}",
                "price_line" => "가격: {0:N0} VND | {1}일",
                "payment_qr" => "결제 QR 코드",
                "bypass_hint" => "로그인 QR 토큰을 만들기 위해 결제를 bypass 합니다",
                "choice_hint" => "선택: 이메일로 QR 받기(이메일 필요) 또는 이 기기에 QR 직접 내려받기(이메일 불필요).",
                "bypass_email" => "이메일로 QR 보내기",
                "bypass_download" => "기기에 QR 내려받기",
                "status_generating" => "선택한 패키지의 토큰을 생성하는 중...",
                "helper_generating_email" => "시스템이 결제를 bypass 하고, 이 기기에서 토큰을 활성화한 뒤 이메일로 로그인 QR 토큰을 보냅니다.",
                "helper_generating_download" => "시스템이 결제를 bypass 하고, 이 기기에서 토큰을 활성화한 뒤 로그인 QR 이미지를 기기에 저장합니다.",
                "status_failed" => "bypass 실패",
                "failed_title" => "실패",
                "invalid_email" => "QR 토큰을 받을 유효한 이메일 주소를 입력하세요.",
                "status_activated" => "토큰 활성화 완료",
                "helper_email_sent" => "토큰이 활성화되었고 로그인 QR 토큰 이메일이 전송되었습니다.",
                "helper_qr_ready" => "토큰이 활성화되었고 로그인 QR 토큰이 준비되었습니다.",
                "gmail_sent" => "이메일 전송 완료",
                "gmail_open" => "Gmail을 열어 QR 토큰 보내기",
                "gmail_subject" => "Vinh Khanh Smart Tourism 로그인 QR 토큰",
                "package_line" => "패키지: {0}",
                "token_line" => "토큰: {0}",
                "payload_line" => "QR payload: {0}",
                "expires_line" => "만료: {0}",
                "enter_app" => "앱으로 이동",
                "success_title" => "패키지 활성화 완료",
                "email_backend_sent" => "백엔드가 QR 토큰 이메일을 자동으로 보냈습니다.",
                "email_manual_hint" => "자동 이메일 전송에 실패했습니다. Gmail 버튼으로 수동 발송할 수 있습니다.",
                "download_only_hint" => "QR을 직접 내려받기로 선택했습니다. 로그인에 필요하므로 이미지를 안전하게 보관하세요.",
                "save_qr" => "QR을 기기에 저장",
                "save_qr_title" => "QR 저장됨",
                "save_qr_success" => "QR 코드가 기기의 Pictures/VinhKhanh에 저장되었습니다.",
                _ => key
            },
            "ja" => key switch
            {
                "status_waiting" => "支払い確認待ち",
                "status_creating_payment" => "Creating VietQR payment code...",
                "helper_creating_payment" => "Creating a pending invoice with this package and device code.",
                "status_checking_payment" => "Checking Casso payment confirmation...",
                "helper_checking_payment" => "If Casso has received the webhook, the login QR token will be created now.",
                "helper_waiting" => "Scan this VietQR with your banking app. The transfer content identifies the package and device.",
                "helper_auto_waiting" => "Scan this VietQR with your banking app. The app checks Casso automatically and opens the login QR after payment.",
                "check_payment" => "I paid, check now",
                "pending_title" => "Payment pending",
                "payment_reference" => "Transfer content: {0}",
                "payment_amount" => "Amount: {0:N0} VND",
                "bypass_button" => "bypass を確認",
                "back" => "戻る",
                "title" => "QR決済",
                "email_section" => "ログイン用QRトークン受信メール (任意)",
                "email_placeholder" => "メール受信を選ぶ場合のみ必要 — Gmailアドレス",
                "email_line" => "メール: {0}",
                "price_line" => "価格: {0:N0} VND | {1}日",
                "payment_qr" => "決済QRコード",
                "bypass_hint" => "ログイン用QRトークンを作るために支払いを bypass します",
                "choice_hint" => "選択: メールでQRを受け取る(メール必須) または この端末にQRを直接ダウンロード(メール不要)。",
                "bypass_email" => "メールでQRを送る",
                "bypass_download" => "端末にQRをダウンロード",
                "status_generating" => "選択したプランのトークンを生成中...",
                "helper_generating_email" => "システムは支払いを bypass し、この端末でトークンを有効化してからログイン用QRトークンをメール送信します。",
                "helper_generating_download" => "システムは支払いを bypass し、この端末でトークンを有効化してからログイン用QR画像を端末に保存します。",
                "status_failed" => "bypass 失敗",
                "failed_title" => "失敗",
                "invalid_email" => "QRトークンを受け取る有効なメールアドレスを入力してください。",
                "status_activated" => "トークン有効化完了",
                "helper_email_sent" => "トークンを有効化し、ログイン用QRトークンのメールを送信しました。",
                "helper_qr_ready" => "トークンを有効化し、ログイン用QRトークンを生成しました。",
                "gmail_sent" => "メール送信済み",
                "gmail_open" => "Gmail を開いてQRトークンを送信",
                "gmail_subject" => "Vinh Khanh Smart Tourism ログイン用QRトークン",
                "package_line" => "プラン: {0}",
                "token_line" => "トークン: {0}",
                "payload_line" => "QR payload: {0}",
                "expires_line" => "有効期限: {0}",
                "enter_app" => "アプリに入る",
                "success_title" => "プランの有効化に成功しました",
                "email_backend_sent" => "バックエンドがQRトークンメールを自動送信しました。",
                "email_manual_hint" => "自動メール送信に失敗しました。Gmail ボタンから手動送信できます。",
                "download_only_hint" => "QRを直接ダウンロードを選択しました。ログインに必要なので画像を安全に保管してください。",
                "save_qr" => "QRをデバイスに保存",
                "save_qr_title" => "QR保存完了",
                "save_qr_success" => "QRコードが端末のPictures/VinhKhanh に保存されました。",
                _ => key
            },
            _ => key switch
            {
                "status_waiting" => "Chờ xác nhận thanh toán",
                "status_creating_payment" => "Dang tao ma thanh toan VietQR...",
                "helper_creating_payment" => "Backend dang tao hoa don cho thanh toan voi ma goi va ma thiet bi hien tai.",
                "status_checking_payment" => "Dang kiem tra xac nhan thanh toan Casso...",
                "helper_checking_payment" => "Neu Casso da gui webhook giao dich ngan hang, he thong se kich hoat goi va sinh QR token dang nhap.",
                "helper_waiting" => "Quet VietQR nay bang app ngan hang. Noi dung chuyen khoan co ma goi va ma thiet bi de Casso webhook khop dung hoa don.",
                "helper_auto_waiting" => "Quet VietQR nay bang app ngan hang. App se tu kiem tra Casso va mo QR token dang nhap khi thanh toan duoc xac nhan.",
                "check_payment" => "Da thanh toan, kiem tra",
                "pending_title" => "Chua co thanh toan",
                "payment_reference" => "Noi dung chuyen khoan: {0}",
                "payment_amount" => "So tien: {0:N0} VND",
                "bypass_button" => "Xác thực bypass",
                "back" => "Quay lại",
                "title" => "Thanh toán QR",
                "email_section" => "Email nhận QR token đăng nhập (tuỳ chọn)",
                "email_placeholder" => "Nhập gmail (chỉ cần khi bạn chọn nhận qua email)",
                "email_line" => "Email: {0}",
                "price_line" => "Giá: {0:N0} VND | {1} ngày",
                "payment_qr" => "Mã QR thanh toán",
                "bypass_hint" => "Bypass thanh toán để tạo QR token đăng nhập",
                "choice_hint" => "Chọn: gửi QR qua email (cần email) hoặc tải QR trực tiếp về máy (không cần email).",
                "bypass_email" => "Gửi QR qua email",
                "bypass_download" => "Tải QR về máy",
                "status_generating" => "Đang sinh token theo gói dịch vụ...",
                "helper_generating_email" => "Hệ thống sẽ bypass thanh toán, kích hoạt token trên máy này và gửi QR token đến email.",
                "helper_generating_download" => "Hệ thống sẽ bypass thanh toán, kích hoạt token trên máy này và lưu ảnh QR về máy của bạn.",
                "status_failed" => "Bypass thất bại",
                "failed_title" => "Thất bại",
                "invalid_email" => "Vui lòng nhập gmail hợp lệ để nhận QR token.",
                "status_activated" => "Đã kích hoạt token",
                "helper_email_sent" => "Đã kích hoạt token và gửi email QR token đăng nhập.",
                "helper_qr_ready" => "Đã kích hoạt token và sinh QR token đăng nhập.",
                "gmail_sent" => "Email đã gửi",
                "gmail_open" => "Mở Gmail để gửi QR token",
                "gmail_subject" => "QR token đăng nhập Vinh Khanh Smart Tourism",
                "package_line" => "Gói: {0}",
                "token_line" => "Token: {0}",
                "payload_line" => "QR payload: {0}",
                "expires_line" => "Hết hạn: {0}",
                "enter_app" => "Vào app",
                "success_title" => "Đã kích hoạt gói thành công",
                "email_backend_sent" => "Backend đã gửi email QR token tự động.",
                "email_manual_hint" => "Chưa gửi được email tự động, bạn có thể dùng nút Gmail để gửi thủ công.",
                "download_only_hint" => "Bạn đã chọn tải QR trực tiếp về máy. Vui lòng giữ kỹ ảnh QR — cần thiết để đăng nhập.",
                "save_qr" => "Tải QR về máy",
                "save_qr_title" => "Đã lưu QR",
                "save_qr_success" => "Đã lưu mã QR vào Pictures/VinhKhanh trên máy bạn.",
                _ => key
            }
        };
    }
}
