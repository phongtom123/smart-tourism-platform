using MauiApp1.Models;
using Microsoft.Maui.Controls.Shapes;

namespace MauiApp1.Views.Maps;

public partial class PoiMapPage
{
    private async Task LoadLanguagesAsync(bool forceReload = false)
    {
        if (!forceReload && _languages.Count > 0)
            return;

        _languages.Clear();
        _languages.Add(new NgonNgu { MaNgonNgu = "vi", TenNgonNgu = "Tiếng Việt" });
        _languages.Add(new NgonNgu { MaNgonNgu = "en", TenNgonNgu = "English" });
        _languages.Add(new NgonNgu { MaNgonNgu = "ko", TenNgonNgu = "한국어" });
        _languages.Add(new NgonNgu { MaNgonNgu = "ja", TenNgonNgu = "日本語" });

        if (_languages.All(x => !x.MaNgonNgu.Equals(_selectedLanguageCode, StringComparison.OrdinalIgnoreCase)))
        {
            _selectedLanguageCode = _languages[0].MaNgonNgu;
        }
    }

    private void RenderLanguageOptions()
    {
        _languageRow.Children.Clear();

        foreach (var language in _languages)
        {
            var code = language.MaNgonNgu;
            var selected = code.Equals(_selectedLanguageCode, StringComparison.OrdinalIgnoreCase);

            var chip = new Border
            {
                Stroke = selected ? Color.FromArgb("#FF6B00") : Color.FromArgb("#E5E7EB"),
                StrokeThickness = 1,
                BackgroundColor = selected ? Color.FromArgb("#FFF1E6") : Colors.White,
                StrokeShape = new RoundRectangle { CornerRadius = 999 },
                Padding = new Thickness(12, 8),
                Content = new Label
                {
                    Text = string.IsNullOrWhiteSpace(language.TenNgonNgu) ? code.ToUpperInvariant() : language.TenNgonNgu,
                    FontSize = 13,
                    FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None,
                    TextColor = selected ? Color.FromArgb("#9A3412") : Color.FromArgb("#374151")
                }
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, __) => await SelectLanguageAsync(code);
            chip.GestureRecognizers.Add(tap);

            _languageRow.Children.Add(chip);
        }
    }

    private async Task SelectLanguageAsync(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            return;

        languageCode = NormalizeLanguageCode(languageCode);

        if (_selectedLanguageCode.Equals(languageCode, StringComparison.OrdinalIgnoreCase))
            return;

        _selectedLanguageCode = languageCode;
        ResetAudioState();
        RenderLanguageOptions();
        await ApplySelectedLanguageToCurrentDetailAsync();
    }

    private async Task ApplySelectedLanguageToCurrentDetailAsync()
    {
        if (_currentDetailGianHang is null)
            return;

        // forceRefresh khi đổi ngôn ngữ: tránh hiển thị moTa/audio cache cũ sau khi web admin update DB.
        var translated = await _gianHangService.GetByIdAsync(_currentDetailGianHang.IdGianHang, NormalizeLanguageCode(_selectedLanguageCode), forceRefresh: true);
        if (translated is null)
        {
            SetDetailInfo(_currentDetailGianHang);
            await PopulateDetailFoodImagesAsync(_currentDetailGianHang, NormalizeLanguageCode(_selectedLanguageCode));
            return;
        }

        _detailTitle.Text = string.IsNullOrWhiteSpace(translated.Ten) ? _loc.Get("fallback_name") : translated.Ten;
        _detailAddress.Text = string.IsNullOrWhiteSpace(translated.DiaChi) ? _loc.Get("fallback_address") : translated.DiaChi;
        _detailDescription.Text = string.IsNullOrWhiteSpace(translated.MoTa) ? _loc.Get("fallback_description") : translated.MoTa;

        _detailAudioLabel.Text = string.IsNullOrWhiteSpace(translated.AudioURL)
            ? _loc.Get("fallback_audio_none")
            : _loc.Get("fallback_audio_ready");

        System.Diagnostics.Debug.WriteLine(
            $"[Lang] selected={_selectedLanguageCode}, translatedAudio={translated.AudioURL}");

        if (!string.IsNullOrWhiteSpace(translated.HinhAnhChinh) || !string.IsNullOrWhiteSpace(translated.HinhAnh))
        {
            _detailImage.Source = BuildImageSource(
                !string.IsNullOrWhiteSpace(translated.HinhAnhChinh)
                    ? translated.HinhAnhChinh
                    : translated.HinhAnh);
        }

        _currentDetailGianHang = translated;
        await PopulateDetailFoodImagesAsync(translated, NormalizeLanguageCode(_selectedLanguageCode));
    }

    private static string NormalizeLanguageCode(string? rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
            return "vi";

        var code = rawCode.Trim().ToLowerInvariant();
        return code switch
        {
            var c when c.StartsWith("vi") => "vi",
            var c when c.StartsWith("en") => "en",
            var c when c.StartsWith("ko") => "ko",
            var c when c.StartsWith("ja") => "ja",
            _ => code
        };
    }

    private string? ResolveAudioPathForSelectedLanguage(string? rawAudioPath)
    {
        if (string.IsNullOrWhiteSpace(rawAudioPath))
            return rawAudioPath;

        var lang = NormalizeLanguageCode(_selectedLanguageCode);
        var audioPath = rawAudioPath.Trim();
        var targetSuffix = $"_{lang}";
        var alternateSuffix = lang == "en" ? "_vi" : "_en";

        var lastSlash = audioPath.LastIndexOf('/');
        var basePath = lastSlash >= 0 ? audioPath[..(lastSlash + 1)] : string.Empty;
        var fileName = lastSlash >= 0 ? audioPath[(lastSlash + 1)..] : audioPath;

        var dotIdx = fileName.LastIndexOf('.');
        var stem = dotIdx >= 0 ? fileName[..dotIdx] : fileName;
        var ext = dotIdx >= 0 ? fileName[dotIdx..] : string.Empty;

        if (stem.EndsWith(targetSuffix, StringComparison.OrdinalIgnoreCase))
            return audioPath;

        if (stem.EndsWith(alternateSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var replaced = stem[..^alternateSuffix.Length] + targetSuffix + ext;
            return basePath + replaced;
        }

        return audioPath;
    }

}

