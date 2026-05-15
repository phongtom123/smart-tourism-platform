using MauiApp1.Models;
using Plugin.Maui.Audio;

namespace MauiApp1.Services;

public sealed class GeofenceEngineService : IAsyncDisposable
{
    private readonly IAudioManager _audioManager;
    private readonly AudioCacheService _audioCacheService;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly SemaphoreSlim _playbackSync = new(1, 1);

    private readonly Dictionary<int, GeofenceTarget> _targets = new();
    private readonly HashSet<int> _insideTargetIds = new();
    private readonly Dictionary<int, int> _priorityBoosts = new();
    private readonly List<AudioPlaybackRequest> _autoPlayQueue = [];
    private readonly HashSet<int> _autoPlaySeenStoreIds = [];

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private IAudioPlayer? _currentPlayer;
    private MemoryStream? _currentAudioStream;
    private Location? _debugLocationOverride;
    private Location? _lastPublishedLocation;
    private System.Timers.Timer? _playbackTimer;
    private CancellationTokenSource? _pendingAutoPlayCts;
    private AudioPlaybackRequest? _pendingRequest;
    private string? _currentAudioUrl;
    private int? _currentStoreId;
    private string? _currentStoreTitle;
    private string? _currentStoreImageUrl;
    private AudioPlaybackStateSnapshot _playbackState = AudioPlaybackStateSnapshot.Hidden;

    public event EventHandler<GeofenceTriggeredEventArgs>? EnteredGeofence;
    public event EventHandler<GeofenceTriggeredEventArgs>? ExitedGeofence;
    public event EventHandler<LocationUpdatedEventArgs>? LocationUpdated;
    public event EventHandler<AudioPlaybackStateChangedEventArgs>? PlaybackStateChanged;

    public bool AutoPlayAudioWhenEntered { get; set; } = true;
    public double RadiusMeters { get; set; } = 10d;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(3);
    public TimeSpan PendingAutoPlayDelay { get; set; } = TimeSpan.FromSeconds(3);
    public AudioPlaybackStateSnapshot PlaybackState => _playbackState;

    private readonly LocalizationService _loc;
    private readonly ApiService _apiService;

    public GeofenceEngineService(IAudioManager audioManager, AudioCacheService audioCacheService, LocalizationService localizationService, ApiService apiService)
    {
        _audioManager = audioManager;
        _audioCacheService = audioCacheService;
        _loc = localizationService;
        _apiService = apiService;
    }

    public async Task UpdateTargetsAsync(IEnumerable<GianHang> gianHangs, double? radiusMeters = null)
    {
        await _sync.WaitAsync();
        try
        {
            _targets.Clear();
            _insideTargetIds.Clear();

            foreach (var gianHang in gianHangs)
            {
                if (gianHang.Lat is null || gianHang.Lon is null)
                    continue;

                _targets[gianHang.IdGianHang] = new GeofenceTarget(
                    gianHang.IdGianHang,
                    string.IsNullOrWhiteSpace(gianHang.Ten) ? $"POI {gianHang.IdGianHang}" : gianHang.Ten,
                    gianHang.Lat.Value,
                    gianHang.Lon.Value,
                    gianHang.AudioFullUrl,
                    gianHang.HinhAnhFullUrl,
                    gianHang.PhiHangThang,
                    radiusMeters ?? RadiusMeters);
            }
        }
        finally
        {
            _sync.Release();
        }

        await ClearAutoPlayQueueAsync(cancelPending: false);
    }

    public async Task SetPriorityBoostsAsync(IReadOnlyDictionary<int, int> priorityBoosts, bool resetInsideState = false)
    {
        await _sync.WaitAsync();
        try
        {
            _priorityBoosts.Clear();

            foreach (var (storeId, priority) in priorityBoosts)
            {
                if (storeId <= 0 || priority <= 0)
                    continue;

                _priorityBoosts[storeId] = priority;
            }

            if (resetInsideState)
                _insideTargetIds.Clear();
        }
        finally
        {
            _sync.Release();
        }

        if (resetInsideState)
            await ClearAutoPlayQueueAsync(cancelPending: true);
    }

    public Task ClearPriorityBoostsAsync(bool resetInsideState = false)
    {
        return SetPriorityBoostsAsync(new Dictionary<int, int>(), resetInsideState);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_loopTask is { IsCompleted: false })
            return;

        var permission = await EnsurePermissionAsync();
        if (!permission)
            return;

        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loopTask = Task.Run(() => RunLoopAsync(_loopCts.Token), _loopCts.Token);
    }

    public async Task StopAsync()
    {
        if (_loopCts is null)
            return;

        try
        {
            _loopCts.Cancel();
            if (_loopTask is not null)
                await _loopTask;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _loopCts.Dispose();
            _loopCts = null;
            _loopTask = null;
            await StopPlaybackAsync();
        }
    }

    public void SetDebugLocation(double latitude, double longitude)
    {
        _debugLocationOverride = new Location(latitude, longitude);
    }

    public void ClearDebugLocation()
    {
        _debugLocationOverride = null;
    }

    public async Task EvaluateNowAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EvaluateCurrentLocationAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GeofenceEngine] Evaluate now error: {ex.Message}");
        }
    }

    public async Task TogglePlaybackAsync(AudioPlaybackRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.HasPlayableAudio)
            return;

        await _playbackSync.WaitAsync(cancellationToken);
        try
        {
            CancelPendingAutoPlayInternal();

            if (IsCurrentRequest(request) && _currentPlayer is not null)
            {
                if (_currentPlayer.IsPlaying)
                {
                    _currentPlayer.Pause();
                    StopPlaybackTimerInternal();
                    PublishPlaybackState(CreateSnapshot(AudioPlaybackPhase.Paused));
                    return;
                }

                _currentPlayer.Play();
                StartPlaybackTimerInternal();
                PublishPlaybackState(CreateSnapshot(AudioPlaybackPhase.Playing));
                return;
            }

            _autoPlayQueue.Clear();
            await PlayNowInternalAsync(request, cancellationToken);
        }
        finally
        {
            _playbackSync.Release();
        }
    }

    public async Task SeekAsync(double positionSeconds, CancellationToken cancellationToken = default)
    {
        await _playbackSync.WaitAsync(cancellationToken);
        try
        {
            if (_currentPlayer is null)
                return;

            _currentPlayer.Seek(Math.Max(0, positionSeconds));
            PublishPlaybackState(CreateSnapshot(_currentPlayer.IsPlaying ? AudioPlaybackPhase.Playing : AudioPlaybackPhase.Paused));
        }
        finally
        {
            _playbackSync.Release();
        }
    }

    public async Task StopPlaybackAsync(CancellationToken cancellationToken = default)
    {
        await _playbackSync.WaitAsync(cancellationToken);
        try
        {
            CancelPendingAutoPlayInternal();
            StopCurrentAudioInternal();
            _autoPlayQueue.Clear();
            ResetCurrentTrackInternal();
            PublishPlaybackState(AudioPlaybackStateSnapshot.Hidden);
        }
        finally
        {
            _playbackSync.Release();
        }
    }

    public async Task ScheduleAutoPlayAsync(GeofenceTarget target, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target.AudioUrl))
            return;

        var request = new AudioPlaybackRequest(
            target.Id,
            target.Name,
            target.AudioUrl,
            target.ImageUrl,
            IsAutoTriggered: true);

        await _playbackSync.WaitAsync(cancellationToken);
        try
        {
            if (IsCurrentRequest(request) || IsPendingRequest(request))
                return;

            CancelPendingAutoPlayInternal();

            var pendingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _pendingAutoPlayCts = pendingCts;
            _pendingRequest = request;

            PublishPlaybackState(new AudioPlaybackStateSnapshot(
                AudioPlaybackPhase.Pending,
                request.StoreId,
                request.Title,
                "Sắp phát sau 3 giây",
                request.AudioUrl,
                request.ImageUrl,
                0,
                0,
                request.IsAutoTriggered));

            _ = Task.Run(() => CompletePendingAutoPlayAsync(request, pendingCts), pendingCts.Token);
        }
        finally
        {
            _playbackSync.Release();
        }
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await EvaluateCurrentLocationAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GeofenceEngine] Loop error: {ex.Message}");
            }

            try
            {
                await Task.Delay(PollInterval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task EvaluateCurrentLocationAsync(CancellationToken ct)
    {
        var location = _debugLocationOverride;
        if (location is null)
        {
            location = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(6)),
                ct);

            location ??= await Geolocation.Default.GetLastKnownLocationAsync();
        }

        if (location is null)
            return;

        PublishLocationIfChanged(location);

        List<GeofenceTriggeredEventArgs> newlyEntered = [];
        List<GeofenceTriggeredEventArgs> newlyExited = [];
        List<GeofenceTriggeredEventArgs> currentlyInside = [];
        Dictionary<int, int> priorityBoosts;

        await _sync.WaitAsync(ct);
        try
        {
            foreach (var target in _targets.Values)
            {
                var distance = Location.CalculateDistance(
                    location.Latitude,
                    location.Longitude,
                    target.Latitude,
                    target.Longitude,
                    DistanceUnits.Kilometers) * 1000d;

                var isInside = distance <= target.RadiusMeters;

                if (isInside)
                {
                    var triggerArgs = new GeofenceTriggeredEventArgs(target, distance);
                    currentlyInside.Add(triggerArgs);

                    if (_insideTargetIds.Add(target.Id))
                        newlyEntered.Add(triggerArgs);
                }
                else
                {
                    if (_insideTargetIds.Remove(target.Id))
                        newlyExited.Add(new GeofenceTriggeredEventArgs(target, distance));
                }
            }

            priorityBoosts = new Dictionary<int, int>(_priorityBoosts);
        }
        finally
        {
            _sync.Release();
        }

        foreach (var trigger in PrioritizeGeofenceTargets(newlyEntered, priorityBoosts))
            EnteredGeofence?.Invoke(this, trigger);

        foreach (var trigger in newlyExited.OrderBy(x => x.Target.Id))
            ExitedGeofence?.Invoke(this, trigger);

        await ResetAutoPlayForExitedTargetsAsync(newlyExited, ct);

        var prioritizedEntered = PrioritizeGeofenceTargets(newlyEntered, priorityBoosts).ToList();
        if (prioritizedEntered.Count > 0)
        {
            var topEnteredRequest = CreateAutoPlayRequest(prioritizedEntered[0].Target);

            await _playbackSync.WaitAsync(ct);
            try
            {
                if (_currentPlayer is not null && !IsCurrentRequest(topEnteredRequest))
                {
                    StopCurrentAudioInternal();
                    ResetCurrentTrackInternal();
                    PublishPlaybackState(AudioPlaybackStateSnapshot.Hidden);
                }
            }
            finally
            {
                _playbackSync.Release();
            }
        }

        if (!AutoPlayAudioWhenEntered || currentlyInside.Count == 0)
            return;

        // Queue unseen targets in priority order so overlapping booths can play
        // once each without replaying the same winner forever.
        await EnqueueAutoPlayCandidatesAsync(PrioritizeGeofenceTargets(currentlyInside, priorityBoosts), ct);
    }

    private static IOrderedEnumerable<GeofenceTriggeredEventArgs> PrioritizeGeofenceTargets(
        IEnumerable<GeofenceTriggeredEventArgs> targets,
        IReadOnlyDictionary<int, int> priorityBoosts)
    {
        return GeofencePriorityRules.Prioritize(
            targets,
            priorityBoosts,
            x => x.Target.Id,
            x => x.DistanceMeters,
            x => x.Target.RadiusMeters,
            x => x.Target.MonthlyFee);
    }

    private async Task CompletePendingAutoPlayAsync(AudioPlaybackRequest request, CancellationTokenSource pendingCts)
    {
        try
        {
            await Task.Delay(PendingAutoPlayDelay, pendingCts.Token);

            await _playbackSync.WaitAsync(pendingCts.Token);
            try
            {
                if (!ReferenceEquals(_pendingAutoPlayCts, pendingCts) || !IsPendingRequest(request))
                    return;

                _pendingAutoPlayCts = null;
                _pendingRequest = null;
                await PlayNowInternalAsync(request, pendingCts.Token);

                if (_currentPlayer is null)
                    TryScheduleNextAutoPlayLocked(CancellationToken.None);
            }
            finally
            {
                _playbackSync.Release();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GeofenceEngine] Pending auto-play error: {ex.Message}");
        }
        finally
        {
            pendingCts.Dispose();
        }
    }

    private async Task PlayNowInternalAsync(AudioPlaybackRequest request, CancellationToken cancellationToken)
    {
        if (!request.HasPlayableAudio)
            return;

        try
        {
            CancelPendingAutoPlayInternal();
            StopCurrentAudioInternal();

            var bytes = await _audioCacheService.GetAudioBytesAsync(request.AudioUrl, cancellationToken);
            if (bytes is null || bytes.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[GeofenceEngine] No cached/network audio for {request.AudioUrl}");
                PublishPlaybackState(AudioPlaybackStateSnapshot.Hidden);
                return;
            }

            _currentAudioStream = new MemoryStream(bytes);
            _currentPlayer = _audioManager.CreatePlayer(_currentAudioStream);
            _currentAudioUrl = request.AudioUrl;
            _currentStoreId = request.StoreId;
            _currentStoreTitle = request.Title;
            _currentStoreImageUrl = request.ImageUrl;

            _currentPlayer.Play();
            StartPlaybackTimerInternal();
            PublishPlaybackState(CreateSnapshot(AudioPlaybackPhase.Playing));

            // Ghi nhận lượt truy cập NGAY khi bắt đầu phát audio (backend dedupe đảm bảo
            // 1 device/store/day = 1 lần count, dù user chỉ nghe vài giây).
            var storeIdToRecordOnStart = _currentStoreId;
            if (storeIdToRecordOnStart.HasValue)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _apiService.RecordPoiVisitAsync(storeIdToRecordOnStart.Value);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GeofenceEngine] Record POI visit (on start) error: {ex.Message}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GeofenceEngine] Play error: {ex.Message}");
            StopCurrentAudioInternal();
            ResetCurrentTrackInternal();
            PublishPlaybackState(AudioPlaybackStateSnapshot.Hidden);
        }
    }

    private void PublishLocationIfChanged(Location location)
    {
        var shouldPublish = _lastPublishedLocation is null ||
                            Location.CalculateDistance(
                                _lastPublishedLocation,
                                location,
                                DistanceUnits.Kilometers) * 1000d >= 3d;
        // Keep the UI marker responsive while still avoiding redraws for tiny GPS jitter.
        shouldPublish = shouldPublish ||
                        Location.CalculateDistance(
                            _lastPublishedLocation,
                            location,
                            DistanceUnits.Kilometers) * 1000d >= 1d;

        if (!shouldPublish)
            return;

        _lastPublishedLocation = location;
        LocationUpdated?.Invoke(this, new LocationUpdatedEventArgs(location));
    }

    private static async Task<bool> EnsurePermissionAsync()
    {
        var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (permission == PermissionStatus.Granted)
            return true;

        permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        return permission == PermissionStatus.Granted;
    }

    private void StartPlaybackTimerInternal()
    {
        StopPlaybackTimerInternal();

        _playbackTimer = new System.Timers.Timer(500);
        _playbackTimer.Elapsed += OnPlaybackTimerElapsed;
        _playbackTimer.AutoReset = true;
        _playbackTimer.Start();
    }

    private void StopPlaybackTimerInternal()
    {
        if (_playbackTimer is null)
            return;

        _playbackTimer.Elapsed -= OnPlaybackTimerElapsed;
        _playbackTimer.Stop();
        _playbackTimer.Dispose();
        _playbackTimer = null;
    }

    private void OnPlaybackTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (!_playbackSync.Wait(0))
            return;

        try
        {
            if (_currentPlayer is null)
                return;

            if (!_currentPlayer.IsPlaying &&
                _currentPlayer.Duration > 0 &&
                _currentPlayer.CurrentPosition >= _currentPlayer.Duration)
            {
                var storeIdToRecord = _currentStoreId;
                
                StopCurrentAudioInternal();
                ResetCurrentTrackInternal();
                var scheduledNext = TryScheduleNextAutoPlayLocked(CancellationToken.None);
                if (!scheduledNext)
                    PublishPlaybackState(AudioPlaybackStateSnapshot.Hidden);
                
                if (storeIdToRecord.HasValue)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await _apiService.RecordPoiVisitAsync(storeIdToRecord.Value);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[GeofenceEngine] Record POI visit error: {ex.Message}");
                        }
                    });
                }
                
                return;
            }

            PublishPlaybackState(CreateSnapshot(_currentPlayer.IsPlaying ? AudioPlaybackPhase.Playing : AudioPlaybackPhase.Paused));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GeofenceEngine] Playback timer error: {ex.Message}");
        }
        finally
        {
            _playbackSync.Release();
        }
    }

    private void PublishPlaybackState(AudioPlaybackStateSnapshot snapshot)
    {
        _playbackState = snapshot;

        void Raise()
        {
            PlaybackStateChanged?.Invoke(this, new AudioPlaybackStateChangedEventArgs(snapshot));
        }

        if (MainThread.IsMainThread)
            Raise();
        else
            MainThread.BeginInvokeOnMainThread(Raise);
    }

    private AudioPlaybackStateSnapshot CreateSnapshot(AudioPlaybackPhase phase)
    {
        var title = _currentStoreTitle ?? "Audio";
        var position = _currentPlayer?.CurrentPosition ?? 0;
        var duration = _currentPlayer?.Duration ?? 0;
        var message = phase switch
        {
            AudioPlaybackPhase.Playing => _loc.Get("audio_playing"),
            AudioPlaybackPhase.Paused => _loc.Get("audio_paused"),
            AudioPlaybackPhase.Pending => _loc.Get("audio_pending"),
            _ => string.Empty
        };

        return new AudioPlaybackStateSnapshot(
            phase,
            _currentStoreId,
            title,
            message,
            _currentAudioUrl,
            _currentStoreImageUrl,
            position,
            duration,
            false);
    }

    private bool IsCurrentRequest(AudioPlaybackRequest request)
    {
        return !string.IsNullOrWhiteSpace(_currentAudioUrl) &&
               string.Equals(_currentAudioUrl, request.AudioUrl, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsPendingRequest(AudioPlaybackRequest request)
    {
        return _pendingRequest is not null &&
               string.Equals(_pendingRequest.AudioUrl, request.AudioUrl, StringComparison.OrdinalIgnoreCase);
    }

    private async Task EnqueueAutoPlayCandidatesAsync(
        IEnumerable<GeofenceTriggeredEventArgs> prioritizedTargets,
        CancellationToken cancellationToken)
    {
        var prioritizedList = prioritizedTargets.ToList();
        var priorityOrder = prioritizedList
            .Select((trigger, index) => new { trigger.Target.Id, Index = index })
            .ToDictionary(x => x.Id, x => x.Index);

        await _playbackSync.WaitAsync(cancellationToken);
        try
        {
            foreach (var trigger in prioritizedList)
            {
                var target = trigger.Target;
                if (string.IsNullOrWhiteSpace(target.AudioUrl))
                    continue;

                var request = CreateAutoPlayRequest(target);
                if (!_autoPlaySeenStoreIds.Add(target.Id))
                    continue;

                if (IsCurrentRequest(request) || IsPendingRequest(request))
                    continue;

                _autoPlayQueue.Add(request);
            }

            _autoPlayQueue.Sort((left, right) =>
                GetQueuedPriorityOrder(left, priorityOrder)
                    .CompareTo(GetQueuedPriorityOrder(right, priorityOrder)));

            PromoteHigherPriorityPendingLocked(priorityOrder);
            TryScheduleNextAutoPlayLocked(cancellationToken);
        }
        finally
        {
            _playbackSync.Release();
        }
    }

    private static int GetQueuedPriorityOrder(
        AudioPlaybackRequest request,
        IReadOnlyDictionary<int, int> priorityOrder)
    {
        return request.StoreId is int storeId && priorityOrder.TryGetValue(storeId, out var order)
            ? order
            : int.MaxValue;
    }

    private void PromoteHigherPriorityPendingLocked(
        IReadOnlyDictionary<int, int> priorityOrder)
    {
        if (_currentPlayer is not null ||
            _pendingRequest is null ||
            !_pendingRequest.IsAutoTriggered ||
            _autoPlayQueue.Count == 0)
        {
            return;
        }

        var bestQueuedOrder = GetQueuedPriorityOrder(_autoPlayQueue[0], priorityOrder);
        var pendingOrder = GetQueuedPriorityOrder(_pendingRequest, priorityOrder);
        if (bestQueuedOrder >= pendingOrder)
            return;

        var pendingRequest = _pendingRequest;
        CancelPendingAutoPlayInternal();

        if (pendingRequest.StoreId is int storeId && priorityOrder.ContainsKey(storeId))
            _autoPlayQueue.Add(pendingRequest);

        _autoPlayQueue.Sort((left, right) =>
            GetQueuedPriorityOrder(left, priorityOrder)
                .CompareTo(GetQueuedPriorityOrder(right, priorityOrder)));
    }

    private async Task ResetAutoPlayForExitedTargetsAsync(
        IEnumerable<GeofenceTriggeredEventArgs> exitedTargets,
        CancellationToken cancellationToken)
    {
        var exitedIds = exitedTargets.Select(x => x.Target.Id).ToHashSet();
        if (exitedIds.Count == 0)
            return;

        await _playbackSync.WaitAsync(cancellationToken);
        try
        {
            foreach (var id in exitedIds)
                _autoPlaySeenStoreIds.Remove(id);

            _autoPlayQueue.RemoveAll(request =>
                request.StoreId.HasValue && exitedIds.Contains(request.StoreId.Value));

            if (_pendingRequest?.StoreId is int pendingStoreId && exitedIds.Contains(pendingStoreId))
            {
                CancelPendingAutoPlayInternal();
                var scheduledNext = TryScheduleNextAutoPlayLocked(cancellationToken);
                if (!scheduledNext && _currentPlayer is null)
                    PublishPlaybackState(AudioPlaybackStateSnapshot.Hidden);
            }
        }
        finally
        {
            _playbackSync.Release();
        }
    }

    private async Task ClearAutoPlayQueueAsync(bool cancelPending, CancellationToken cancellationToken = default)
    {
        await _playbackSync.WaitAsync(cancellationToken);
        try
        {
            _autoPlayQueue.Clear();
            _autoPlaySeenStoreIds.Clear();

            if (cancelPending)
            {
                CancelPendingAutoPlayInternal();
                if (_currentPlayer is null)
                    PublishPlaybackState(AudioPlaybackStateSnapshot.Hidden);
            }
        }
        finally
        {
            _playbackSync.Release();
        }
    }

    private static AudioPlaybackRequest CreateAutoPlayRequest(GeofenceTarget target)
    {
        return new AudioPlaybackRequest(
            target.Id,
            target.Name,
            target.AudioUrl,
            target.ImageUrl,
            IsAutoTriggered: true);
    }

    private bool TryScheduleNextAutoPlayLocked(CancellationToken cancellationToken)
    {
        if (_currentPlayer is not null || _pendingRequest is not null)
            return false;

        while (_autoPlayQueue.Count > 0)
        {
            var request = _autoPlayQueue[0];
            _autoPlayQueue.RemoveAt(0);

            if (!request.HasPlayableAudio)
                continue;

            SchedulePendingAutoPlayLocked(request, cancellationToken);
            return true;
        }

        return false;
    }

    private void SchedulePendingAutoPlayLocked(AudioPlaybackRequest request, CancellationToken cancellationToken)
    {
        CancelPendingAutoPlayInternal();

        var pendingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pendingAutoPlayCts = pendingCts;
        _pendingRequest = request;

        PublishPlaybackState(new AudioPlaybackStateSnapshot(
            AudioPlaybackPhase.Pending,
            request.StoreId,
            request.Title,
            "Sắp phát sau 3 giây",
            request.AudioUrl,
            request.ImageUrl,
            0,
            0,
            request.IsAutoTriggered));

        _ = Task.Run(() => CompletePendingAutoPlayAsync(request, pendingCts), pendingCts.Token);
    }

    private void CancelPendingAutoPlayInternal()
    {
        _pendingRequest = null;

        if (_pendingAutoPlayCts is null)
            return;

        _pendingAutoPlayCts.Cancel();
        _pendingAutoPlayCts.Dispose();
        _pendingAutoPlayCts = null;
    }

    private void StopCurrentAudioInternal()
    {
        StopPlaybackTimerInternal();

        try
        {
            _currentPlayer?.Stop();
            _currentPlayer?.Dispose();
            _currentPlayer = null;

            _currentAudioStream?.Dispose();
            _currentAudioStream = null;
        }
        catch
        {
        }
    }

    private void ResetCurrentTrackInternal()
    {
        _currentAudioUrl = null;
        _currentStoreId = null;
        _currentStoreTitle = null;
        _currentStoreImageUrl = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _sync.Dispose();
        _playbackSync.Dispose();
    }
}

public sealed record GeofenceTarget(
    int Id,
    string Name,
    double Latitude,
    double Longitude,
    string? AudioUrl,
    string? ImageUrl,
    decimal MonthlyFee,
    double RadiusMeters);

public sealed record AudioPlaybackRequest(
    int? StoreId,
    string Title,
    string? AudioUrl,
    string? ImageUrl,
    bool IsAutoTriggered = false)
{
    public bool HasPlayableAudio => !string.IsNullOrWhiteSpace(AudioUrl);
}

public enum AudioPlaybackPhase
{
    Hidden,
    Pending,
    Playing,
    Paused
}

public sealed record AudioPlaybackStateSnapshot(
    AudioPlaybackPhase Phase,
    int? StoreId,
    string Title,
    string Message,
    string? AudioUrl,
    string? ImageUrl,
    double PositionSeconds,
    double DurationSeconds,
    bool IsAutoTriggered)
{
    public static AudioPlaybackStateSnapshot Hidden { get; } =
        new(
            AudioPlaybackPhase.Hidden,
            null,
            string.Empty,
            string.Empty,
            null,
            null,
            0,
            0,
            false);

    public bool IsVisible => Phase != AudioPlaybackPhase.Hidden;
}

public sealed class AudioPlaybackStateChangedEventArgs : EventArgs
{
    public AudioPlaybackStateChangedEventArgs(AudioPlaybackStateSnapshot state)
    {
        State = state;
    }

    public AudioPlaybackStateSnapshot State { get; }
}

public sealed class LocationUpdatedEventArgs : EventArgs
{
    public LocationUpdatedEventArgs(Location location)
    {
        Location = location;
    }

    public Location Location { get; }
}

public sealed record GeofenceTriggeredEventArgs(GeofenceTarget Target, double DistanceMeters);
