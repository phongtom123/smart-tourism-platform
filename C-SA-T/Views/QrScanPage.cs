using MauiApp1.Services;
using Microsoft.Maui.Controls.Shapes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZXing;
using ZXing.Common;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;
using MauiColor = Microsoft.Maui.Graphics.Color;
using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace MauiApp1.Views;

public class QrScanPage : ContentPage
{
    private readonly ApiService _apiService;
    private readonly AccessFlowService _accessFlowService;
    private readonly LocalizationService _loc;
    private readonly Label _titleLabel;
    private readonly Label _subtitleLabel;
    private readonly Label _backLabel;
    private readonly CameraBarcodeReaderView? _cameraView = null;
    private readonly Label _statusLabel;
    private readonly Label _hintLabel;
    private readonly Label _resultLabel;
    private readonly Button _torchButton;
    private readonly Button _rescanButton;
    private readonly Button _uploadButton;
    private bool _isProcessing;

    public QrScanPage(ApiService apiService, AccessFlowService accessFlowService, LocalizationService localizationService)
    {
        _apiService = apiService;
        _accessFlowService = accessFlowService;
        _loc = localizationService;

        Title = string.Empty;
        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = MauiColor.FromArgb("#FFF7F1");

        _titleLabel = new Label
        {
            Text = GetText("title"),
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            TextColor = MauiColor.FromArgb("#111111")
        };

        _subtitleLabel = new Label
        {
            Text = GetText("subtitle"),
            FontSize = 14,
            TextColor = MauiColor.FromArgb("#6B7280")
        };

        _statusLabel = new Label
        {
            Text = GetText("ready"),
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = MauiColor.FromArgb("#9A3412")
        };

        _hintLabel = new Label
        {
            Text = GetText("hint"),
            FontSize = 13,
            TextColor = MauiColor.FromArgb("#6B7280")
        };

        _resultLabel = new Label
        {
            Text = GetText("result_empty"),
            FontSize = 13,
            TextColor = MauiColor.FromArgb("#334155"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        _torchButton = new Button
        {
            Text = GetText("torch_on"),
            BackgroundColor = MauiColor.FromArgb("#FFF1E6"),
            TextColor = MauiColor.FromArgb("#9A3412"),
            CornerRadius = 14,
            Padding = new Thickness(14, 10),
            HorizontalOptions = LayoutOptions.Fill
        };
        _torchButton.Clicked += (_, __) => ToggleTorch();

        _rescanButton = new Button
        {
            Text = GetText("rescan"),
            BackgroundColor = Colors.White,
            TextColor = MauiColor.FromArgb("#334155"),
            BorderColor = MauiColor.FromArgb("#E2E8F0"),
            BorderWidth = 1,
            CornerRadius = 14,
            Padding = new Thickness(14, 10),
            HorizontalOptions = LayoutOptions.Fill
        };
        _rescanButton.Clicked += async (_, __) => await RestartScannerAsync();

        _uploadButton = new Button
        {
            Text = GetText("upload"),
            BackgroundColor = Colors.White,
            TextColor = MauiColor.FromArgb("#0F766E"),
            BorderColor = MauiColor.FromArgb("#CCFBF1"),
            BorderWidth = 1,
            CornerRadius = 14,
            Padding = new Thickness(14, 10),
            HorizontalOptions = LayoutOptions.Fill
        };
        _uploadButton.Clicked += async (_, __) => await PickQrImageAsync();

        var backButton = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Colors.White,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Padding = new Thickness(14, 10),
            Content = _backLabel = new Label
            {
                Text = GetText("back"),
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = MauiColor.FromArgb("#334155")
            }
        };
        var backTap = new TapGestureRecognizer();
        backTap.Tapped += async (_, __) => await Navigation.PopAsync();
        backButton.GestureRecognizers.Add(backTap);

        View scannerBody;

        if (BarcodeScanning.IsSupported)
        {
            _cameraView = new CameraBarcodeReaderView
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                HeightRequest = 340,
                Options = new BarcodeReaderOptions
                {
                    Formats = BarcodeFormats.TwoDimensional,
                    AutoRotate = true,
                    Multiple = false
                }
            };
            _cameraView.BarcodesDetected += OnBarcodesDetected;

            scannerBody = new Grid
            {
                HeightRequest = 340,
                Children =
                {
                    _cameraView,
                    BuildScannerOverlay()
                }
            };
        }
        else
        {
            scannerBody = new VerticalStackLayout
            {
                HeightRequest = 220,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                Spacing = 10,
                Children =
                {
                    new Label
                    {
                        Text = GetText("unsupported"),
                        FontSize = 14,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = MauiColor.FromArgb("#991B1B"),
                        HorizontalTextAlignment = TextAlignment.Center
                    },
                    new Label
                    {
                        Text = GetText("unsupported_desc"),
                        FontSize = 13,
                        TextColor = MauiColor.FromArgb("#6B7280"),
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                }
            };
            _torchButton.IsEnabled = false;
            _torchButton.Opacity = 0.5;
        }

        var actionButtons = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };
        actionButtons.Children.Add(_torchButton);
        actionButtons.Children.Add(_rescanButton);
        Grid.SetColumn(_rescanButton, 1);
        actionButtons.Children.Add(_uploadButton);
        Grid.SetColumn(_uploadButton, 2);

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star)
            },
            Children =
            {
                new ScrollView
                {
                    Content = new VerticalStackLayout
                    {
                        Spacing = 18,
                        Padding = new Thickness(16, 52, 16, 28),
                        Children =
                        {
                            backButton,
                            _titleLabel,
                            _subtitleLabel,
                            new Border
                            {
                                StrokeThickness = 1,
                                Stroke = MauiColor.FromArgb("#F0E6DC"),
                                BackgroundColor = Colors.White,
                                StrokeShape = new RoundRectangle { CornerRadius = 22 },
                                Padding = new Thickness(14),
                                Content = new VerticalStackLayout
                                {
                                    Spacing = 12,
                                    Children =
                                    {
                                        scannerBody,
                                        actionButtons
                                    }
                                }
                            },
                            new Border
                            {
                                StrokeThickness = 1,
                                Stroke = MauiColor.FromArgb("#F0E6DC"),
                                BackgroundColor = Colors.White,
                                StrokeShape = new RoundRectangle { CornerRadius = 18 },
                                Padding = new Thickness(16, 14),
                                Content = new VerticalStackLayout
                                {
                                    Spacing = 8,
                                    Children =
                                    {
                                        _statusLabel,
                                        _hintLabel,
                                        _resultLabel
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        _loc.LanguageChanged += OnLanguageChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await StartScannerAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopScanner();
    }

    private async Task StartScannerAsync()
    {
        if (_cameraView is null)
            return;

        var permission = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (permission != PermissionStatus.Granted)
            permission = await Permissions.RequestAsync<Permissions.Camera>();

        if (permission != PermissionStatus.Granted)
        {
            _statusLabel.Text = GetText("camera_denied");
            _hintLabel.Text = GetText("camera_denied_desc");
            _cameraView.IsDetecting = false;
            return;
        }

        _statusLabel.Text = GetText("ready");
        _hintLabel.Text = GetText("hint");
        _cameraView.IsDetecting = true;
    }

    private async Task RestartScannerAsync()
    {
        _resultLabel.Text = GetText("result_empty");
        _hintLabel.Text = GetText("hint");
        await StartScannerAsync();
    }

    private void StopScanner()
    {
        if (_cameraView is null)
            return;

        _cameraView.IsDetecting = false;
        _cameraView.IsTorchOn = false;
        _torchButton.Text = GetText("torch_on");
    }

    private View BuildScannerOverlay()
    {
        return new Grid
        {
            InputTransparent = true,
            Children =
            {
                new Border
                {
                    Stroke = MauiColor.FromArgb("#F97316"),
                    StrokeThickness = 2,
                    BackgroundColor = Colors.Transparent,
                    StrokeShape = new RoundRectangle { CornerRadius = 22 },
                    WidthRequest = 220,
                    HeightRequest = 220,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            }
        };
    }

    private async void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (_isProcessing)
            return;

        var raw = e.Results.FirstOrDefault()?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return;

        await ProcessQrRawAsync(raw);
    }

    private string BuildResultText(QrScanResult result, string raw)
    {
        var lines = new List<string>
        {
            $"{GetText("qr_value")}: {raw}"
        };

        if (!string.IsNullOrWhiteSpace(result.MaThietBi))
            lines.Add($"{GetText("device_code")}: {result.MaThietBi}");

        if (!string.IsNullOrWhiteSpace(result.TrangThai))
            lines.Add($"{GetText("status")}: {result.TrangThai}");

        if (result.HetHanLuc.HasValue)
            lines.Add($"{GetText("expires")}: {result.HetHanLuc:dd/MM/yyyy HH:mm}");

        if (!string.IsNullOrWhiteSpace(result.AccessToken))
            lines.Add($"{GetText("token")}: {Shorten(result.AccessToken)}");

        return string.Join(Environment.NewLine, lines);
    }

    private void ToggleTorch()
    {
        if (_cameraView is null)
            return;

        _cameraView.IsTorchOn = !_cameraView.IsTorchOn;
        _torchButton.Text = _cameraView.IsTorchOn ? GetText("torch_off") : GetText("torch_on");
    }

    private void OnLanguageChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _titleLabel.Text = GetText("title");
            _subtitleLabel.Text = GetText("subtitle");
            _backLabel.Text = GetText("back");
            _torchButton.Text = _cameraView?.IsTorchOn == true ? GetText("torch_off") : GetText("torch_on");
            _rescanButton.Text = GetText("rescan");
            _uploadButton.Text = GetText("upload");
        });
    }

    private async Task PickQrImageAsync()
    {
        if (_isProcessing)
            return;

        try
        {
            FileResult? file = null;

            try
            {
                var photos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
                {
                    Title = GetText("upload_picker_title")
                });
                file = photos?.FirstOrDefault();
            }
            catch (FeatureNotSupportedException)
            {
            }

            if (file is null)
            {
                file = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = GetText("upload_picker_title"),
                    FileTypes = FilePickerFileType.Images
                });
            }

            if (file is null)
                return;

            if (_cameraView is not null)
                _cameraView.IsDetecting = false;

            _statusLabel.Text = GetText("image_loading");
            _hintLabel.Text = $"{GetText("picked_file")}: {file.FileName}";
            _resultLabel.Text = GetText("processing_desc");

            var raw = await DecodeQrFromImageAsync(file);
            if (string.IsNullOrWhiteSpace(raw))
            {
                _statusLabel.Text = GetText("failed");
                _hintLabel.Text = GetText("upload_no_qr");
                _resultLabel.Text = $"{GetText("picked_file")}: {file.FileName}";
                await DisplayAlertAsync(GetText("failed"), GetText("upload_no_qr"), _loc.Get("alert_ok"));
                return;
            }

            await ProcessQrRawAsync(raw, file.FileName);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = GetText("failed");
            _hintLabel.Text = ex.Message;
            await DisplayAlertAsync(GetText("failed"), ex.Message, _loc.Get("alert_ok"));
        }
    }

    private async Task<string?> DecodeQrFromImageAsync(FileResult file)
    {
        using var stream = await file.OpenReadAsync();
        using var image = await ImageSharpImage.LoadAsync<Rgba32>(stream);

        var pixels = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(pixels);

        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true,
                TryInverted = true,
                PossibleFormats = new List<ZXing.BarcodeFormat> { ZXing.BarcodeFormat.QR_CODE }
            }
        };

        var result = reader.Decode(pixels, image.Width, image.Height, RGBLuminanceSource.BitmapFormat.RGBA32);
        return result?.Text?.Trim();
    }

    private async Task ProcessQrRawAsync(string raw, string? sourceName = null)
    {
        _isProcessing = true;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (_cameraView is not null)
                    _cameraView.IsDetecting = false;

                _statusLabel.Text = GetText("processing");
                _hintLabel.Text = string.IsNullOrWhiteSpace(sourceName)
                    ? GetText("processing_desc")
                    : $"{GetText("picked_file")}: {sourceName}";
                _resultLabel.Text = raw;

                var result = await _apiService.ScanQrAsync(raw);
                if (result.Success)
                    await _accessFlowService.SaveQrAccessAsync(result);

                _statusLabel.Text = result.Success ? GetText("success") : GetText("failed");
                _hintLabel.Text = result.Message;
                _resultLabel.Text = BuildResultText(result, raw);

                await DisplayAlertAsync(
                    result.Success ? GetText("success") : GetText("failed"),
                    result.Message,
                    _loc.Get("alert_ok"));

                if (result.Success && Navigation.NavigationStack.OfType<AccessEntryPage>().Any())
                    await Navigation.PopAsync();
            });
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private string GetText(string key)
    {
        return _loc.CurrentLanguage switch
        {
            "en" => key switch
            {
                "title" => "Scan QR",
                "subtitle" => "Point the camera at the QR code to activate access.",
                "ready" => "Camera is ready",
                "hint" => "Keep the QR inside the orange frame.",
                "processing" => "Processing QR",
                "processing_desc" => "We are sending the scan result to the server.",
                "success" => "Success",
                "failed" => "Scan failed",
                "unsupported" => "QR scanning is not supported on this device",
                "unsupported_desc" => "Open this page on Android hardware or emulator with camera support.",
                "camera_denied" => "Camera permission denied",
                "camera_denied_desc" => "Please grant camera permission to scan QR codes.",
                "torch_on" => "Turn on flashlight",
                "torch_off" => "Turn off flashlight",
                "rescan" => "Scan again",
                "upload" => "Upload QR image",
                "upload_picker_title" => "Choose a QR image",
                "upload_no_qr" => "No QR code was found in the selected image.",
                "image_loading" => "Reading QR from image",
                "picked_file" => "Selected file",
                "back" => "Back",
                "result_empty" => "No QR has been scanned yet.",
                "qr_value" => "QR value",
                "device_code" => "Device code",
                "status" => "Status",
                "expires" => "Expires",
                "token" => "Access token",
                _ => key
            },
            "ko" => key switch
            {
                "title" => "QR 스캔",
                "subtitle" => "QR 코드를 카메라에 맞춰 접근 권한을 활성화하세요.",
                "ready" => "카메라 준비 완료",
                "hint" => "주황색 프레임 안에 QR을 맞춰 주세요.",
                "processing" => "QR 처리 중",
                "processing_desc" => "스캔 결과를 서버로 보내는 중입니다.",
                "success" => "성공",
                "failed" => "스캔 실패",
                "unsupported" => "이 기기는 QR 스캔을 지원하지 않습니다",
                "unsupported_desc" => "카메라를 지원하는 Android 기기 또는 에뮬레이터에서 이 페이지를 열어 주세요.",
                "camera_denied" => "카메라 권한이 거부되었습니다",
                "camera_denied_desc" => "QR 코드를 스캔하려면 카메라 권한을 허용해 주세요.",
                "torch_on" => "손전등 켜기",
                "torch_off" => "손전등 끄기",
                "rescan" => "다시 스캔",
                "upload" => "QR 이미지 업로드",
                "upload_picker_title" => "QR 이미지 선택",
                "upload_no_qr" => "선택한 이미지에서 QR 코드를 찾지 못했습니다.",
                "image_loading" => "이미지에서 QR 읽는 중",
                "picked_file" => "선택한 파일",
                "back" => "뒤로",
                "result_empty" => "아직 스캔한 QR 결과가 없습니다.",
                "qr_value" => "QR 값",
                "device_code" => "기기 코드",
                "status" => "상태",
                "expires" => "만료 시각",
                "token" => "액세스 토큰",
                _ => key
            },
            "ja" => key switch
            {
                "title" => "QRをスキャン",
                "subtitle" => "QRコードをカメラに向けてアクセスを有効化します。",
                "ready" => "カメラの準備ができました",
                "hint" => "オレンジ色の枠の中にQRを収めてください。",
                "processing" => "QRを処理中",
                "processing_desc" => "スキャン結果をサーバーに送信しています。",
                "success" => "成功",
                "failed" => "スキャン失敗",
                "unsupported" => "この端末はQRスキャンに対応していません",
                "unsupported_desc" => "カメラ対応のAndroid端末またはエミュレーターで開いてください。",
                "camera_denied" => "カメラ権限が拒否されました",
                "camera_denied_desc" => "QRコードを読み取るにはカメラ権限を許可してください。",
                "torch_on" => "ライトをオン",
                "torch_off" => "ライトをオフ",
                "rescan" => "再スキャン",
                "upload" => "QR画像をアップロード",
                "upload_picker_title" => "QR画像を選択",
                "upload_no_qr" => "選択した画像からQRコードを検出できませんでした。",
                "image_loading" => "画像からQRを読み取り中",
                "picked_file" => "選択したファイル",
                "back" => "戻る",
                "result_empty" => "まだQRをスキャンしていません。",
                "qr_value" => "QR値",
                "device_code" => "端末コード",
                "status" => "状態",
                "expires" => "有効期限",
                "token" => "アクセストークン",
                _ => key
            },
            _ => key switch
            {
                "title" => "Quét QR",
                "subtitle" => "Hướng camera vào mã QR để kích hoạt truy cập.",
                "ready" => "Camera sẵn sàng",
                "hint" => "Đặt mã QR nằm gọn trong khung camera.",
                "processing" => "Đang xử lý QR",
                "processing_desc" => "Đang gửi kết quả quét lên server.",
                "success" => "Thành công",
                "failed" => "Quét thất bại",
                "unsupported" => "Thiết bị này chưa hỗ trợ quét QR",
                "unsupported_desc" => "Hãy mở trang này trên Android có camera để sử dụng.",
                "camera_denied" => "Bạn chưa cấp quyền camera",
                "camera_denied_desc" => "Hãy cấp quyền camera để quét mã QR.",
                "torch_on" => "Bật đèn",
                "torch_off" => "Tắt đèn",
                "rescan" => "Quét lại",
                "upload" => "Tải ảnh QR",
                "upload_picker_title" => "Chọn ảnh QR",
                "upload_no_qr" => "Không tìm thấy mã QR trong ảnh đã chọn.",
                "image_loading" => "Đang đọc QR từ ảnh",
                "picked_file" => "Ảnh đã chọn",
                "back" => "Quay lại",
                "result_empty" => "Chưa có kết quả quét QR.",
                "qr_value" => "Giá trị QR",
                "device_code" => "Mã thiết bị",
                "status" => "Trạng thái",
                "expires" => "Hết hạn",
                "token" => "Access token",
                _ => key
            }
        };
    }

    private static string Shorten(string value)
    {
        if (value.Length <= 20)
            return value;

        return $"{value[..8]}...{value[^8..]}";
    }
}
