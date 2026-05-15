using MauiApp1.Models;
using MauiApp1.Services;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;

namespace MauiApp1.Views.Maps;

public partial class PoiMapPage
{
    private static readonly TimeSpan LiveLocationPollInterval = TimeSpan.FromSeconds(2);

    private async Task ShowCurrentLocationMarkerAsync(bool centerOnUser)
    {
        try
        {
            var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (permission != PermissionStatus.Granted)
            {
                permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (permission != PermissionStatus.Granted)
                {
                    System.Diagnostics.Debug.WriteLine("[PoiMapPage] Location permission not granted.");
                    return;
                }
            }

            // Show quickly using cached location first when available.
            var lastKnown = await Geolocation.Default.GetLastKnownLocationAsync();
            if (lastKnown is not null)
            {
                UpdateUserLocationPin(new Location(lastKnown.Latitude, lastKnown.Longitude), centerOnUser);
                _geofenceEngine.ClearDebugLocation();
            }

            // Then resolve a fresher fix in the background.
            _ = Task.Run(async () =>
            {
                try
                {
                    var precise = await Geolocation.Default.GetLocationAsync(
                        new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8)));

                    if (precise is null)
                        return;

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        UpdateUserLocationPin(new Location(precise.Latitude, precise.Longitude), centerOnUser);
                        _geofenceEngine.ClearDebugLocation();
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PoiMapPage] Precise location refresh error: {ex.Message}");
                }
            });

            if (lastKnown is null)
            {
                var location = await Geolocation.Default.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8)));

                if (location is null)
                {
                    System.Diagnostics.Debug.WriteLine("[PoiMapPage] Could not resolve current location.");
                    return;
                }

                var pinLocation = new Location(location.Latitude, location.Longitude);
                UpdateUserLocationPin(pinLocation, centerOnUser);
                _geofenceEngine.ClearDebugLocation();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PoiMapPage] ShowCurrentLocationMarkerAsync error: {ex.Message}");
        }
    }

    private void StartLiveLocationPolling()
    {
        if (_liveLocationPollingCts is not null)
            return;

        _liveLocationPollingCts = new CancellationTokenSource();
        var ct = _liveLocationPollingCts.Token;

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                    if (permission != PermissionStatus.Granted)
                    {
                        await Task.Delay(LiveLocationPollInterval, ct);
                        continue;
                    }

                    var location = await Geolocation.Default.GetLocationAsync(
                        new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(5)),
                        ct);

                    location ??= await Geolocation.Default.GetLastKnownLocationAsync();
                    if (location is not null)
                    {
                        var liveLocation = new Location(location.Latitude, location.Longitude);
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            UpdateUserLocationPin(liveLocation, centerOnUser: _shouldFollowLiveLocation);
                            RefreshVisiblePins();
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PoiMapPage] Live location poll error: {ex.Message}");
                }

                try
                {
                    await Task.Delay(LiveLocationPollInterval, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, ct);
    }

    private void StopLiveLocationPolling()
    {
        try
        {
            _liveLocationPollingCts?.Cancel();
            _liveLocationPollingCts?.Dispose();
        }
        catch
        {
        }
        finally
        {
            _liveLocationPollingCts = null;
        }
    }

    private void UpdateUserLocationPin(Location pinLocation, bool centerOnUser)
    {
        var shouldRecreatePin = _userLocationPin is null;
        var previousLocation = _userLocationPin?.Location;

        _userLocationPin ??= new UserLocationPin
        {
            Label = _loc.Get("my_location"),
            Type = PinType.SavedPin
        };

        var movedMeters = previousLocation is null
            ? double.MaxValue
            : Location.CalculateDistance(previousLocation, pinLocation, DistanceUnits.Kilometers) * 1000d;

        _userLocationPin.Address = $"{pinLocation.Latitude:F6}, {pinLocation.Longitude:F6}";
        _userLocationPin.Location = pinLocation;

        // Android map renderer can ignore in-place Pin.Location changes.
        // Re-add pin when it moved enough so marker definitely redraws.
        var needsReAdd = !double.IsInfinity(movedMeters) && movedMeters >= 0.8;
        if (needsReAdd && _map.Pins.Contains(_userLocationPin))
            _map.Pins.Remove(_userLocationPin);

        if (!_map.Pins.Contains(_userLocationPin))
            _map.Pins.Add(_userLocationPin);

        System.Diagnostics.Debug.WriteLine(
            $"[PoiMapPage] User location pin {(shouldRecreatePin ? "created" : "refreshed")} at {pinLocation.Latitude:F6}, {pinLocation.Longitude:F6}");

        if (centerOnUser)
        {
            var now = DateTime.UtcNow;
            var movedSinceAutoCenterMeters = _lastAutoCenteredLocation is null
                ? double.MaxValue
                : Location.CalculateDistance(_lastAutoCenteredLocation, pinLocation, DistanceUnits.Kilometers) * 1000d;

            var canRecenter = movedSinceAutoCenterMeters >= LiveLocationAutoCenterMinDistanceMeters ||
                              now - _lastAutoCenterAtUtc >= LiveLocationAutoCenterCooldown;

            if (canRecenter)
            {
                var currentRegion = _map.VisibleRegion;
                if (currentRegion is not null)
                {
                    // Keep user's current zoom level; only move the map center.
                    var keepZoomSpan = new MapSpan(
                        pinLocation,
                        currentRegion.LatitudeDegrees,
                        currentRegion.LongitudeDegrees);
                    _map.MoveToRegion(keepZoomSpan);
                }
                else
                {
                    _map.MoveToRegion(MapSpan.FromCenterAndRadius(pinLocation, Distance.FromMeters(350)));
                }
                RestoreMapModeAfterRegionMove();
                _lastAutoCenteredLocation = pinLocation;
                _lastAutoCenterAtUtc = now;
            }
        }
    }

    private async Task InitializeGeofenceAsync()
    {
        try
        {
            var gianHangs = await _gianHangService.GetAllAsync(_selectedLanguageCode);
            _gianHangsForPrefetch = gianHangs.ToList();
            await _geofenceEngine.UpdateTargetsAsync(gianHangs, radiusMeters: 10);

            if (!_isLiveLocationSubscribed)
            {
                _geofenceEngine.EnteredGeofence += OnEnteredGeofence;
                _geofenceEngine.ExitedGeofence += OnExitedGeofence;
                _geofenceEngine.LocationUpdated += OnLiveLocationUpdated;
                _isLiveLocationSubscribed = true;
            }

            _geofenceEngine.AutoPlayAudioWhenEntered = true;
            _geofenceEngine.PollInterval = TimeSpan.FromSeconds(3);
            await _geofenceEngine.EvaluateNowAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PoiMapPage] InitializeGeofenceAsync error: {ex.Message}");
        }
    }

    private void OnEnteredGeofence(object? sender, GeofenceTriggeredEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[Geofence] Entered '{e.Target.Name}' at {e.DistanceMeters:F1}m (radius {e.Target.RadiusMeters:F0}m)");

        if (_activeTourDetail is not null)
            _ = HandleActiveTourGeofenceEnteredAsync(e.Target.Id);
    }

    private void OnExitedGeofence(object? sender, GeofenceTriggeredEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[Geofence] Exited '{e.Target.Name}' at {e.DistanceMeters:F1}m (radius {e.Target.RadiusMeters:F0}m)");

        if (_pendingTourCompletionExitStoreId == e.Target.Id)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await FinishActiveTourAfterExitAsync(e.Target.Id);
            });
        }
    }

    private async Task HandleActiveTourGeofenceEnteredAsync(int idGianHang)
    {
        await _tourAdvanceSync.WaitAsync();
        try
        {
            var detail = _activeTourDetail;
            if (detail is null)
                return;

            var tourStop = TourService.GetUsableStops(detail)
                .FirstOrDefault(s => s.IdGianHang == idGianHang);
            if (tourStop is null)
                return;

            var expectedStop = TourRules.ResolveExpectedGeofenceStop(detail, _activeTourProgress);

            if (expectedStop is null || expectedStop.IdGianHang != idGianHang)
                return;

            var result = await _tourService.AdvanceAsync(detail.Tour.IdTour, idGianHang);
            if (result is null || !result.Success)
                return;

            _activeTourProgress = new TourProgress
            {
                IdTour = detail.Tour.IdTour,
                StepHienTai = result.StepKeTiep ?? tourStop.ThuTu,
                IsCompleted = result.IsCompleted
            };

            if (result.IsCompleted)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await MarkActiveTourCompletedPendingExitAsync(detail, idGianHang);
                });
                return;
            }

            await ApplyActiveTourGeofencePriorityAsync();

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (_activeTourDetail?.Tour.IdTour != detail.Tour.IdTour)
                    return;

                await RefreshActiveTourTextAsync();
                await RenderActiveTourRouteAsync();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Tour] Advance from geofence error: {ex.Message}");
        }
        finally
        {
            _tourAdvanceSync.Release();
        }
    }

    private void OnLiveLocationUpdated(object? sender, LocationUpdatedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateUserLocationPin(e.Location, centerOnUser: _shouldFollowLiveLocation);
            RefreshVisiblePins();
        });

        TriggerLazyAudioPrefetchIfDue(e.Location);
    }

    private void TriggerLazyAudioPrefetchIfDue(Location location)
    {
        if (_gianHangsForPrefetch.Count == 0)
            return;

        // Throttle: chỉ chạy nếu di chuyển >30m HOẶC quá 30s từ lần prefetch trước.
        var now = DateTime.UtcNow;
        var sinceLast = now - _lastLazyPrefetchAtUtc;
        var movedFar = !_lastLazyPrefetchLat.HasValue ||
                       Location.CalculateDistance(
                           _lastLazyPrefetchLat.Value, _lastLazyPrefetchLon!.Value,
                           location.Latitude, location.Longitude,
                           DistanceUnits.Kilometers) * 1000d >= 30d;

        if (sinceLast < TimeSpan.FromSeconds(30) && !movedFar)
            return;

        _lastLazyPrefetchAtUtc = now;
        _lastLazyPrefetchLat = location.Latitude;
        _lastLazyPrefetchLon = location.Longitude;

        _ = Task.Run(async () =>
        {
            try
            {
                await _audioCacheService.PrefetchNearbyAsync(
                    location.Latitude,
                    location.Longitude,
                    _gianHangsForPrefetch,
                    maxDistanceMeters: 200,
                    topN: 3);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PoiMapPage] Lazy audio prefetch error: {ex.Message}");
            }
        });
    }

    private async Task DiagnosePoiImagesAsync()
    {
        try
        {
            if (_pois.Count == 0)
                return;

            if (!HasInternet())
            {
                System.Diagnostics.Debug.WriteLine("[ImageProbe] Skip probe because device is offline.");
                return;
            }

            var failed = new List<string>();

            // Chỉ probe URL từ backend/API; ảnh local resource MAUI không probe qua HTTP.
            foreach (var poi in _pois.Take(20))
            {
                var source = NormalizeImagePath(poi.ImagePath);
                if (!source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var error = await ProbeImageUrlAsync(source, poi.Title);
                if (error is not null)
                {
                    failed.Add(error);
                }
            }

            if (failed.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[ImageProbe] Failed image count: {failed.Count}");
                foreach (var item in failed.Take(5))
                {
                    System.Diagnostics.Debug.WriteLine($"[ImageProbe][ERROR] {item}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[ImageProbe] All probed POI images are reachable.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ImageProbe] Diagnose error: {ex.Message}");
        }
    }

    private static async Task<string?> ProbeImageUrlAsync(string imageUrl, string poiTitle)
    {
        try
        {
            _imageProbeHttpClient ??= CreateImageProbeHttpClient();

            using var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
            using var response = await _imageProbeHttpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return $"'{poiTitle}' -> {imageUrl} | HTTP {(int)response.StatusCode} {response.StatusCode}";
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"'{poiTitle}' -> {imageUrl} | EX: {ex.Message}";
        }
    }

    private static HttpClient CreateImageProbeHttpClient()
    {
        var handler = new HttpClientHandler();

#if DEBUG
        handler.ServerCertificateCustomValidationCallback =
            (message, cert, chain, errors) => true;
#endif

        var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        global::MauiApp1.Utils.BackendUrlResolver.ConfigureHttpClient(httpClient);
        return httpClient;
    }

}


