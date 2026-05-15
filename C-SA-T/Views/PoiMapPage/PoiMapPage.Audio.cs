using MauiApp1.Services;

namespace MauiApp1.Views.Maps;

public partial class PoiMapPage
{
    private async void OnPlayButtonClicked(object? sender, EventArgs e)
    {
        try
        {
            var request = BuildCurrentDetailPlaybackRequest();
            if (request is null)
            {
                await DisplayAlertAsync(_loc.Get("alert_notice"), _loc.Get("alert_no_audio"), _loc.Get("alert_ok"));
                return;
            }

            await _geofenceEngine.TogglePlaybackAsync(request);
            SyncDetailAudioUi(_geofenceEngine.PlaybackState);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(_loc.Get("alert_audio_error"), ex.Message, _loc.Get("alert_ok"));
        }
    }

    private async void OnProgressDragCompleted(object? sender, EventArgs e)
    {
        try
        {
            await _geofenceEngine.SeekAsync(_progressSlider.Value);
            _currentTimeLabel.Text = FormatTime(_progressSlider.Value);
        }
        finally
        {
            _isDraggingSlider = false;
        }
    }

    private void OnPlaybackStateChanged(object? sender, AudioPlaybackStateChangedEventArgs e)
    {
        SyncDetailAudioUi(e.State);
    }

    private void SyncDetailAudioUi(AudioPlaybackStateSnapshot state)
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(() => SyncDetailAudioUi(state));
            return;
        }

        if (_playButton is null || _progressSlider is null || _currentTimeLabel is null || _durationLabel is null)
            return;

        if (!MatchesCurrentDetailAudio(state))
        {
            ResetAudioUiOnly();
            return;
        }

        _playButton.Text = state.Phase switch
        {
            AudioPlaybackPhase.Playing => _loc.Get("btn_pause"),
            AudioPlaybackPhase.Pending => _loc.Get("btn_pending"),
            _ => _loc.Get("btn_play")
        };

        _progressSlider.Minimum = 0;
        _progressSlider.Maximum = state.DurationSeconds > 0 ? state.DurationSeconds : 1;

        if (!_isDraggingSlider)
            _progressSlider.Value = Math.Min(state.PositionSeconds, _progressSlider.Maximum);

        _currentTimeLabel.Text = FormatTime(state.PositionSeconds);
        _durationLabel.Text = FormatTime(state.DurationSeconds);
    }

    private bool MatchesCurrentDetailAudio(AudioPlaybackStateSnapshot state)
    {
        var request = BuildCurrentDetailPlaybackRequest();
        if (request is null || string.IsNullOrWhiteSpace(state.AudioUrl))
            return false;

        return string.Equals(request.AudioUrl, state.AudioUrl, StringComparison.OrdinalIgnoreCase);
    }

    private AudioPlaybackRequest? BuildCurrentDetailPlaybackRequest()
    {
        if (_currentDetailGianHang is null)
            return null;

        var selectedAudioPath = ResolveAudioPathForSelectedLanguage(_currentDetailGianHang.AudioURL);
        var audioUrl = BuildFullUrl(selectedAudioPath);
        if (string.IsNullOrWhiteSpace(audioUrl))
            return null;

        return new AudioPlaybackRequest(
            _currentDetailGianHang.IdGianHang,
            string.IsNullOrWhiteSpace(_detailTitle?.Text) ? _currentDetailGianHang.Ten : _detailTitle.Text,
            audioUrl,
            _currentDetailGianHang.HinhAnhFullUrl);
    }

    private void ResetAudioState()
    {
        ResetAudioUiOnly();
        SyncDetailAudioUi(_geofenceEngine.PlaybackState);
    }

    private void StopAndDisposeAudio()
    {
        ResetAudioUiOnly();
    }

    private void ResetAudioUiOnly()
    {
        if (_playButton is null || _progressSlider is null || _currentTimeLabel is null || _durationLabel is null)
            return;

        _playButton.Text = _loc.Get("btn_play");
        _progressSlider.Minimum = 0;
        _progressSlider.Maximum = 1;
        _progressSlider.Value = 0;
        _currentTimeLabel.Text = "00:00";
        _durationLabel.Text = "00:00";
        _isDraggingSlider = false;
    }

    private static string? BuildFullUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        return global::MauiApp1.Utils.BackendUrlResolver.BuildUrl(path);
    }

    private static HttpClient CreateHttpClientForAudio()
    {
        var handler = new HttpClientHandler();

#if DEBUG
        handler.ServerCertificateCustomValidationCallback =
            (_, _, _, _) => true;
#endif

        var httpClient = new HttpClient(handler);
        global::MauiApp1.Utils.BackendUrlResolver.ConfigureHttpClient(httpClient);
        return httpClient;
    }

    private static string FormatTime(double seconds)
    {
        if (seconds < 0)
            seconds = 0;

        var ts = TimeSpan.FromSeconds(seconds);
        return ts.Hours > 0
            ? ts.ToString(@"hh\:mm\:ss")
            : ts.ToString(@"mm\:ss");
    }
}
