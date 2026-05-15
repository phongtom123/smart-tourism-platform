using MauiApp1.Controls;
using MauiApp1.Models;
using MauiApp1.Services;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Maps;
using MapCircle = Microsoft.Maui.Controls.Maps.Circle;
using MapPolyline = Microsoft.Maui.Controls.Maps.Polyline;

namespace MauiApp1.Views.Maps;

public partial class PoiMapPage
{
    private const double VisualTestGeofenceRadiusMeters = 8d;
    private readonly List<MapElement> _visualTestMapElements = new();
    private readonly List<Pin> _visualTestPins = new();
    private CancellationTokenSource? _visualTestCts;
    private bool _isVisualTestRunning;
    private Label? _visualTestStatusLabel;

    private MapActionButton CreateVisualTestButton()
    {
        var label = new Label
        {
            Text = "BT",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#166534"),
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var button = new MapActionButton(label, widthRequest: 46, heightRequest: 46)
        {
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 132, 16, 0),
            ZIndex = 21
        };

        SemanticProperties.SetDescription(button, "Run batch geofence visual tests");
        button.Clicked += async (_, __) => await ToggleVisualGeofenceTestAsync();
        return button;
    }

    private Border CreateVisualTestStatusBanner()
    {
            _visualTestStatusLabel = new Label
        {
            Text = string.Empty,
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#14532D"),
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 10
        };

        return new Border
        {
            StrokeThickness = 1,
            Stroke = new SolidColorBrush(Color.FromArgb("#86EFAC")),
            BackgroundColor = Color.FromArgb("#ECFDF5"),
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Padding = new Thickness(12, 8),
            IsVisible = false,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(16, 132, 74, 0),
            ZIndex = 22,
            Content = _visualTestStatusLabel,
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.10f,
                Radius = 12,
                Offset = new Point(0, 5)
            }
        };
    }

    private async Task ToggleVisualGeofenceTestAsync()
    {
        if (_isVisualTestRunning)
        {
            StopVisualGeofenceTest(clearOverlay: true);
            return;
        }

        await RunBatchVisualGeofenceTestsAsync();
    }

    private async Task RunBatchVisualGeofenceTestsAsync()
    {
        _isVisualTestRunning = true;
        _visualTestButton.SetBusy(true);
        _visualTestCts?.Cancel();
        _visualTestCts?.Dispose();
        _visualTestCts = new CancellationTokenSource();
        var ct = _visualTestCts.Token;

        var previousAutoPlay = _geofenceEngine.AutoPlayAudioWhenEntered;
        var previousPendingDelay = _geofenceEngine.PendingAutoPlayDelay;
        var entered = new List<int>();
        var exited = new List<int>();
        var latestPlaybackState = _geofenceEngine.PlaybackState;

        void OnEntered(object? sender, GeofenceTriggeredEventArgs e) => entered.Add(e.Target.Id);
        void OnExited(object? sender, GeofenceTriggeredEventArgs e) => exited.Add(e.Target.Id);
        void OnPlaybackChanged(object? sender, AudioPlaybackStateChangedEventArgs e) => latestPlaybackState = e.State;

        try
        {
            StopLiveLocationPolling();
            ClearVisualTestOverlay();
            _visualTestStatusBanner.IsVisible = true;

            var anchor = await ResolveVisualTestAnchorAsync(ct);
            var targets = BuildVisualTestTargets(anchor);
            var scenarios = BuildVisualTestScenarios(anchor);
            var tourScenarios = BuildVisualTourScenarios();
            var visitorScenarios = BuildVisualVisitorScenarios();
            var totalCases = scenarios.Count + tourScenarios.Count + visitorScenarios.Count;

            DrawVisualTestOverlay(targets, scenarios.Select(x => x.Point).ToList());
            CenterVisualTestRoute(scenarios.Select(x => x.Point).ToList());

            _geofenceEngine.AutoPlayAudioWhenEntered = true;
            _geofenceEngine.PendingAutoPlayDelay = TimeSpan.FromSeconds(30);
            await _geofenceEngine.StopPlaybackAsync(ct);
            await _geofenceEngine.ClearPriorityBoostsAsync(resetInsideState: true);
            await _geofenceEngine.UpdateTargetsAsync(targets, VisualTestGeofenceRadiusMeters);
            await _geofenceEngine.SetPriorityBoostsAsync(
                new Dictionary<int, int> { [2] = 5 },
                resetInsideState: true);

            _geofenceEngine.EnteredGeofence += OnEntered;
            _geofenceEngine.ExitedGeofence += OnExited;
            _geofenceEngine.PlaybackStateChanged += OnPlaybackChanged;

            var passed = 0;
            var completed = 0;
            var results = new List<VisualBatchResult>();

            for (var i = 0; i < scenarios.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                entered.Clear();
                exited.Clear();
                latestPlaybackState = _geofenceEngine.PlaybackState;

                var scenario = scenarios[i];
                if (scenario.ResetBefore)
                {
                    await _geofenceEngine.StopPlaybackAsync(ct);
                    await _geofenceEngine.SetPriorityBoostsAsync(
                        new Dictionary<int, int> { [2] = 5 },
                        resetInsideState: true);
                }

                UpdateVisualTestStatus($"Running {i + 1}/{scenarios.Count}: {scenario.Name}");
                entered.Clear();
                exited.Clear();
                latestPlaybackState = _geofenceEngine.PlaybackState;

                UpdateUserLocationPin(
                    new Location(scenario.Point.Latitude, scenario.Point.Longitude),
                    centerOnUser: true);

                _geofenceEngine.SetDebugLocation(scenario.Point.Latitude, scenario.Point.Longitude);
                await _geofenceEngine.EvaluateNowAsync(ct);
                await Task.Delay(120, ct);

                var result = EvaluateGeofenceScenario(scenario, entered, exited, latestPlaybackState);
                if (result.Passed)
                    passed++;

                completed++;
                results.Add(result);
                UpdateVisualTestStatus(BuildBatchStatus(passed, completed, totalCases, results, includeDetails: false));

                await Task.Delay(650, ct);
            }

            foreach (var scenario in tourScenarios)
            {
                ct.ThrowIfCancellationRequested();

                UpdateVisualTestStatus($"Running tour {completed - scenarios.Count + 1}/{tourScenarios.Count}: {scenario.Name}");
                var result = scenario.Run();
                if (result.Passed)
                    passed++;

                completed++;
                results.Add(result);
                UpdateVisualTestStatus(BuildBatchStatus(passed, completed, totalCases, results, includeDetails: false));

                await Task.Delay(450, ct);
            }

            foreach (var scenario in visitorScenarios)
            {
                ct.ThrowIfCancellationRequested();

                UpdateVisualTestStatus($"Running visitors {completed - scenarios.Count - tourScenarios.Count + 1}/{visitorScenarios.Count}: {scenario.Name}");
                var result = scenario.Run();
                if (result.Passed)
                    passed++;

                completed++;
                results.Add(result);
                UpdateVisualTestStatus(BuildBatchStatus(passed, completed, totalCases, results, includeDetails: false));

                await Task.Delay(450, ct);
            }

            var reportPath = await WriteBatchReportAsync(passed, completed, totalCases, results, ct);
            var finalMessage = BuildBatchStatus(passed, completed, totalCases, results, includeDetails: false) +
                               $"\n\nReport: {reportPath}";
            UpdateVisualTestStatus(finalMessage);
            await DisplayAlertAsync("Batch geofence + tour test", finalMessage, _loc.Get("alert_ok"));
        }
        catch (OperationCanceledException)
        {
            UpdateVisualTestStatus("Batch test stopped.");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Batch geofence test", ex.Message, _loc.Get("alert_ok"));
        }
        finally
        {
            _geofenceEngine.EnteredGeofence -= OnEntered;
            _geofenceEngine.ExitedGeofence -= OnExited;
            _geofenceEngine.PlaybackStateChanged -= OnPlaybackChanged;

            _geofenceEngine.PendingAutoPlayDelay = previousPendingDelay;
            _geofenceEngine.AutoPlayAudioWhenEntered = previousAutoPlay;
            _geofenceEngine.ClearDebugLocation();
            await _geofenceEngine.StopPlaybackAsync();
            await _geofenceEngine.ClearPriorityBoostsAsync(resetInsideState: true);
            await InitializeGeofenceAsync();

            StartLiveLocationPolling();
            _isVisualTestRunning = false;
            _visualTestButton.SetBusy(false);
        }
    }

    private async Task<Location> ResolveVisualTestAnchorAsync(CancellationToken cancellationToken)
    {
        if (_gianHangsForPrefetch.Count == 0)
        {
            var gianHangs = await _gianHangService.GetAllAsync(_selectedLanguageCode);
            _gianHangsForPrefetch = gianHangs.ToList();
        }

        cancellationToken.ThrowIfCancellationRequested();

        var firstPoi = _gianHangsForPrefetch
            .Where(x => x.Lat.HasValue && x.Lon.HasValue)
            .OrderBy(x => x.IdGianHang)
            .FirstOrDefault();

        return firstPoi is not null
            ? new Location(firstPoi.Lat!.Value, firstPoi.Lon!.Value)
            : new Location(10.7630000, 106.6605000);
    }

    private static List<GianHang> BuildVisualTestTargets(Location anchor)
    {
        var dELat = anchor.Latitude;
        var dELon = anchor.Longitude + LongitudeOffsetMeters(anchor, 35);
        var fLat = anchor.Latitude;
        var fLon = anchor.Longitude + LongitudeOffsetMeters(anchor, 70);
        var gLat = anchor.Latitude;
        var gLon = anchor.Longitude + LongitudeOffsetMeters(anchor, 105);
        var hILat = anchor.Latitude;
        var hILon = anchor.Longitude + LongitudeOffsetMeters(anchor, 140);
        var jLat = anchor.Latitude + LatitudeOffsetMeters(4);
        var jLon = anchor.Longitude + LongitudeOffsetMeters(anchor, 175);
        var kLat = anchor.Latitude;
        var kLon = anchor.Longitude + LongitudeOffsetMeters(anchor, 175);

        return
        [
            new GianHang
            {
                IdGianHang = 1,
                Ten = "Batch A",
                Lat = anchor.Latitude,
                Lon = anchor.Longitude,
                AudioURL = "batch-a.mp3",
                PhiHangThang = 100_000m
            },
            new GianHang
            {
                IdGianHang = 2,
                Ten = "Batch B boosted",
                Lat = anchor.Latitude + LatitudeOffsetMeters(5),
                Lon = anchor.Longitude,
                AudioURL = "batch-b.mp3",
                PhiHangThang = 300_000m
            },
            new GianHang
            {
                IdGianHang = 3,
                Ten = "Batch C",
                Lat = anchor.Latitude + LatitudeOffsetMeters(20),
                Lon = anchor.Longitude,
                AudioURL = "batch-c.mp3",
                PhiHangThang = 500_000m
            },
            new GianHang
            {
                IdGianHang = 4,
                Ten = "Batch D low fee",
                Lat = dELat,
                Lon = dELon,
                AudioURL = "batch-d.mp3",
                PhiHangThang = 100_000m
            },
            new GianHang
            {
                IdGianHang = 5,
                Ten = "Batch E high fee",
                Lat = dELat,
                Lon = dELon,
                AudioURL = "batch-e.mp3",
                PhiHangThang = 900_000m
            },
            new GianHang
            {
                IdGianHang = 6,
                Ten = "Batch F no audio",
                Lat = fLat,
                Lon = fLon,
                AudioURL = null,
                PhiHangThang = 200_000m
            },
            new GianHang
            {
                IdGianHang = 7,
                Ten = "Batch G boundary",
                Lat = gLat,
                Lon = gLon,
                AudioURL = "batch-g.mp3",
                PhiHangThang = 200_000m
            },
            new GianHang
            {
                IdGianHang = 8,
                Ten = "Batch H same tie",
                Lat = hILat,
                Lon = hILon,
                AudioURL = "batch-h.mp3",
                PhiHangThang = 450_000m
            },
            new GianHang
            {
                IdGianHang = 9,
                Ten = "Batch I same tie",
                Lat = hILat,
                Lon = hILon,
                AudioURL = "batch-i.mp3",
                PhiHangThang = 450_000m
            },
            new GianHang
            {
                IdGianHang = 10,
                Ten = "Batch J high fee farther",
                Lat = jLat,
                Lon = jLon,
                AudioURL = "batch-j.mp3",
                PhiHangThang = 900_000m
            },
            new GianHang
            {
                IdGianHang = 11,
                Ten = "Batch K low fee closer",
                Lat = kLat,
                Lon = kLon,
                AudioURL = "batch-k.mp3",
                PhiHangThang = 100_000m
            }
        ];
    }

    private static List<VisualBatchScenario> BuildVisualTestScenarios(Location anchor)
    {
        var dELat = anchor.Latitude;
        var dELon = anchor.Longitude + LongitudeOffsetMeters(anchor, 35);
        var fLat = anchor.Latitude;
        var fLon = anchor.Longitude + LongitudeOffsetMeters(anchor, 70);
        var gLat = anchor.Latitude;
        var gLon = anchor.Longitude + LongitudeOffsetMeters(anchor, 105);
        var hILat = anchor.Latitude;
        var hILon = anchor.Longitude + LongitudeOffsetMeters(anchor, 140);
        var kLat = anchor.Latitude;
        var kLon = anchor.Longitude + LongitudeOffsetMeters(anchor, 175);

        return
        [
            new VisualBatchScenario(
                "outside all",
                new VisualTestRoutePoint("outside all", anchor.Latitude - LatitudeOffsetMeters(20), anchor.Longitude),
                ExpectedEntered: [],
                ExpectedExited: [],
                ExpectedPendingStoreId: null,
                ExpectedHidden: true,
                ResetBefore: true),
            new VisualBatchScenario(
                "enter A",
                new VisualTestRoutePoint("enter A", anchor.Latitude - LatitudeOffsetMeters(6), anchor.Longitude),
                ExpectedEntered: [1],
                ExpectedExited: [],
                ExpectedPendingStoreId: 1,
                ExpectedHidden: false,
                ResetBefore: false),
            new VisualBatchScenario(
                "overlap A+B, B boosted",
                new VisualTestRoutePoint("overlap A+B", anchor.Latitude + LatitudeOffsetMeters(2.5), anchor.Longitude),
                ExpectedEntered: [2],
                ExpectedExited: [],
                ExpectedPendingStoreId: 2,
                ExpectedHidden: false,
                ResetBefore: false),
            new VisualBatchScenario(
                "exit A, stay B",
                new VisualTestRoutePoint("exit A, stay B", anchor.Latitude + LatitudeOffsetMeters(10), anchor.Longitude),
                ExpectedEntered: [],
                ExpectedExited: [1],
                ExpectedPendingStoreId: 2,
                ExpectedHidden: false,
                ResetBefore: false),
            new VisualBatchScenario(
                "exit B, enter C",
                new VisualTestRoutePoint("exit B, enter C", anchor.Latitude + LatitudeOffsetMeters(20), anchor.Longitude),
                ExpectedEntered: [3],
                ExpectedExited: [2],
                ExpectedPendingStoreId: 3,
                ExpectedHidden: false,
                ResetBefore: false),
            new VisualBatchScenario(
                "exit all",
                new VisualTestRoutePoint("exit all", anchor.Latitude + LatitudeOffsetMeters(32), anchor.Longitude),
                ExpectedEntered: [],
                ExpectedExited: [3],
                ExpectedPendingStoreId: null,
                ExpectedHidden: true,
                ResetBefore: false),
            new VisualBatchScenario(
                "boundary exact is inside",
                new VisualTestRoutePoint("boundary exact", gLat + LatitudeOffsetMeters(VisualTestGeofenceRadiusMeters), gLon),
                ExpectedEntered: [7],
                ExpectedExited: [],
                ExpectedPendingStoreId: 7,
                ExpectedHidden: false,
                ResetBefore: true),
            new VisualBatchScenario(
                "boundary just outside",
                new VisualTestRoutePoint("boundary outside", gLat + LatitudeOffsetMeters(VisualTestGeofenceRadiusMeters + 1), gLon),
                ExpectedEntered: [],
                ExpectedExited: [],
                ExpectedPendingStoreId: null,
                ExpectedHidden: true,
                ResetBefore: true),
            new VisualBatchScenario(
                "same distance chooses higher fee",
                new VisualTestRoutePoint("fee priority D+E", dELat, dELon),
                ExpectedEntered: [4, 5],
                ExpectedExited: [],
                ExpectedPendingStoreId: 5,
                ExpectedHidden: false,
                ResetBefore: true),
            new VisualBatchScenario(
                "same distance and fee chooses lower id",
                new VisualTestRoutePoint("id priority H+I", hILat, hILon),
                ExpectedEntered: [8, 9],
                ExpectedExited: [],
                ExpectedPendingStoreId: 8,
                ExpectedHidden: false,
                ResetBefore: true),
            new VisualBatchScenario(
                "closer target beats higher fee",
                new VisualTestRoutePoint("distance priority J+K", kLat, kLon),
                ExpectedEntered: [10, 11],
                ExpectedExited: [],
                ExpectedPendingStoreId: 11,
                ExpectedHidden: false,
                ResetBefore: true),
            new VisualBatchScenario(
                "target without audio stays hidden",
                new VisualTestRoutePoint("no audio target", fLat, fLon),
                ExpectedEntered: [6],
                ExpectedExited: [],
                ExpectedPendingStoreId: null,
                ExpectedHidden: true,
                ResetBefore: true),
            new VisualBatchScenario(
                "re-enter A after reset",
                new VisualTestRoutePoint("re-enter A", anchor.Latitude - LatitudeOffsetMeters(6), anchor.Longitude),
                ExpectedEntered: [1],
                ExpectedExited: [],
                ExpectedPendingStoreId: 1,
                ExpectedHidden: false,
                ResetBefore: true),
            new VisualBatchScenario(
                "stay A no duplicate enter",
                new VisualTestRoutePoint("stay A", anchor.Latitude - LatitudeOffsetMeters(5), anchor.Longitude),
                ExpectedEntered: [],
                ExpectedExited: [],
                ExpectedPendingStoreId: 1,
                ExpectedHidden: false,
                ResetBefore: false),
            new VisualBatchScenario(
                "exit A cancels pending",
                new VisualTestRoutePoint("exit A cancel pending", anchor.Latitude - LatitudeOffsetMeters(20), anchor.Longitude),
                ExpectedEntered: [],
                ExpectedExited: [1],
                ExpectedPendingStoreId: null,
                ExpectedHidden: true,
                ResetBefore: false),
            new VisualBatchScenario(
                "re-enter A after actual exit",
                new VisualTestRoutePoint("re-enter A after exit", anchor.Latitude - LatitudeOffsetMeters(6), anchor.Longitude),
                ExpectedEntered: [1],
                ExpectedExited: [],
                ExpectedPendingStoreId: 1,
                ExpectedHidden: false,
                ResetBefore: false)
        ];
    }

    private static double LatitudeOffsetMeters(double meters) => meters / 111_320d;

    private static double LongitudeOffsetMeters(Location anchor, double meters)
    {
        var latRadians = anchor.Latitude * Math.PI / 180d;
        var metersPerDegree = 111_320d * Math.Cos(latRadians);
        return meters / metersPerDegree;
    }

    private static List<VisualTourScenario> BuildVisualTourScenarios()
    {
        return
        [
            new VisualTourScenario(
                "usable stops skip invalid/unavailable",
                () =>
                {
                    var detail = TourDetailOf(
                        TourStopOf(3, 30),
                        TourStopOf(1, 10),
                        TourStopOf(2, 20, isAvailable: false),
                        TourStopOf(4, 40, lat: null),
                        TourStopOf(5, 50, lon: double.NaN));
                    var actual = TourRules.GetUsableStops(detail).Select(x => x.IdGianHang).ToArray();
                    return TourResult("usable stops skip invalid/unavailable", SameIds(actual, [10, 30]),
                        "usable=[10,30]",
                        $"usable={FormatIds(actual)}");
                }),
            new VisualTourScenario(
                "initial tour expects first stop",
                () =>
                {
                    var detail = TourDetailOf(TourStopOf(1, 10), TourStopOf(2, 20), TourStopOf(3, 30));
                    var progress = new TourProgress { StepHienTai = 0 };
                    var current = TourRules.ResolveCurrentStop(detail, progress);
                    var next = TourRules.ResolveNextAvailableStop(detail, current);
                    var expected = TourRules.ResolveExpectedGeofenceStop(detail, progress);
                    var boosts = TourRules.BuildGeofencePriorityBoosts(detail, progress);
                    var ok = current?.IdGianHang == 10 &&
                             next?.IdGianHang == 20 &&
                             expected?.IdGianHang == 10 &&
                             boosts.GetValueOrDefault(10) == TourRules.InitialCurrentStopBoost &&
                             boosts.GetValueOrDefault(20) == TourRules.InitialNextStopBoost &&
                             boosts.GetValueOrDefault(30) == TourRules.BaseTourStopBoost;
                    return TourResult("initial tour expects first stop", ok,
                        "current=10 next=20 expected=10 boosts=10:4000,20:3500,30:1000",
                        $"current={IdOf(current)} next={IdOf(next)} expected={IdOf(expected)} boosts={FormatBoosts(boosts)}");
                }),
            new VisualTourScenario(
                "active step expects next stop",
                () =>
                {
                    var detail = TourDetailOf(TourStopOf(1, 10), TourStopOf(2, 20), TourStopOf(3, 30));
                    var progress = new TourProgress { StepHienTai = 2 };
                    var current = TourRules.ResolveCurrentStop(detail, progress);
                    var next = TourRules.ResolveNextAvailableStop(detail, current);
                    var expected = TourRules.ResolveExpectedGeofenceStop(detail, progress);
                    var boosts = TourRules.BuildGeofencePriorityBoosts(detail, progress);
                    var ok = current?.IdGianHang == 10 &&
                             next?.IdGianHang == 20 &&
                             expected?.IdGianHang == 20 &&
                             boosts.GetValueOrDefault(10) == TourRules.ActiveCurrentStopBoost &&
                             boosts.GetValueOrDefault(20) == TourRules.ActiveNextStopBoost;
                    return TourResult("active step expects next stop", ok,
                        "current=10 next=20 expected=20 boosts=10:3000,20:4000",
                        $"current={IdOf(current)} next={IdOf(next)} expected={IdOf(expected)} boosts={FormatBoosts(boosts)}");
                }),
            new VisualTourScenario(
                "step gaps skip to next usable stop",
                () =>
                {
                    var detail = TourDetailOf(TourStopOf(1, 10), TourStopOf(3, 20), TourStopOf(5, 30));
                    var progress = new TourProgress { StepHienTai = 4 };
                    var current = TourRules.ResolveCurrentStop(detail, progress);
                    var next = TourRules.ResolveNextAvailableStop(detail, current);
                    var expected = TourRules.ResolveExpectedGeofenceStop(detail, progress);
                    var ok = current?.IdGianHang == 20 &&
                             next?.IdGianHang == 30 &&
                             expected?.IdGianHang == 30;
                    return TourResult("step gaps skip to next usable stop", ok,
                        "current=20 next=30 expected=30",
                        $"current={IdOf(current)} next={IdOf(next)} expected={IdOf(expected)}");
                }),
            new VisualTourScenario(
                "final step still expects final geofence",
                () =>
                {
                    var detail = TourDetailOf(TourStopOf(1, 10), TourStopOf(2, 20));
                    var progress = new TourProgress { StepHienTai = 2 };
                    var expected = TourRules.ResolveExpectedGeofenceStop(detail, progress);
                    return TourResult("final step still expects final geofence", expected?.IdGianHang == 20,
                        "expected=20",
                        $"expected={IdOf(expected)}");
                }),
            new VisualTourScenario(
                "completed progress blocks more advances",
                () =>
                {
                    var detail = TourDetailOf(TourStopOf(1, 10), TourStopOf(2, 20));
                    var progress = new TourProgress { StepHienTai = 2, IsCompleted = true };
                    var expected = TourRules.ResolveExpectedGeofenceStop(detail, progress);
                    return TourResult("completed progress blocks more advances", expected is null,
                        "expected=null",
                        $"expected={IdOf(expected)}");
                }),
            new VisualTourScenario(
                "empty tour has no current/boosts",
                () =>
                {
                    var detail = TourDetailOf();
                    var current = TourRules.ResolveCurrentStop(detail, null);
                    var boosts = TourRules.BuildGeofencePriorityBoosts(detail, null);
                    var ratio = TourRules.CalculateProgressRatio(detail, null, null);
                    var ok = current is null && boosts.Count == 0 && Math.Abs(ratio - 1d) < 0.000001;
                    return TourResult("empty tour has no current/boosts", ok,
                        "current=null boosts={} progress=1",
                        $"current={IdOf(current)} boosts={FormatBoosts(boosts)} progress={ratio:F2}");
                }),
            new VisualTourScenario(
                "non-positive store ids are not boosted",
                () =>
                {
                    var detail = TourDetailOf(TourStopOf(1, 10), TourStopOf(2, 0), TourStopOf(3, -1));
                    var boosts = TourRules.BuildGeofencePriorityBoosts(detail, null);
                    var ok = boosts.ContainsKey(10) && !boosts.ContainsKey(0) && !boosts.ContainsKey(-1);
                    return TourResult("non-positive store ids are not boosted", ok,
                        "boosts contains 10 only",
                        $"boosts={FormatBoosts(boosts)}");
                }),
            new VisualTourScenario(
                "progress ratio middle and complete",
                () =>
                {
                    var detail = TourDetailOf(TourStopOf(1, 10), TourStopOf(2, 20), TourStopOf(3, 30));
                    var middle = TourRules.CalculateProgressRatio(detail, detail.DanhSachStop[0], detail.DanhSachStop[1]);
                    var complete = TourRules.CalculateProgressRatio(detail, detail.DanhSachStop[2], null);
                    var ok = Math.Abs(middle - (1d / 3d)) < 0.000001 &&
                             Math.Abs(complete - 1d) < 0.000001;
                    return TourResult("progress ratio middle and complete", ok,
                        "middle=0.33 complete=1.00",
                        $"middle={middle:F2} complete={complete:F2}");
                }),
            new VisualTourScenario(
                "invalid coordinate stops are ignored",
                () =>
                {
                    var detail = TourDetailOf(
                        TourStopOf(1, 10, lat: 91),
                        TourStopOf(2, 20, lon: -181),
                        TourStopOf(3, 30, lat: double.PositiveInfinity),
                        TourStopOf(4, 40));
                    var usable = TourRules.GetUsableStops(detail).Select(x => x.IdGianHang).ToArray();
                    return TourResult("invalid coordinate stops are ignored", SameIds(usable, [40]),
                        "usable=[40]",
                        $"usable={FormatIds(usable)}");
                }),
            new VisualTourScenario(
                "unavailable middle stop is skipped",
                () =>
                {
                    var detail = TourDetailOf(
                        TourStopOf(1, 10),
                        TourStopOf(2, 20, isAvailable: false),
                        TourStopOf(3, 30));
                    var progress = new TourProgress { StepHienTai = 2 };
                    var current = TourRules.ResolveCurrentStop(detail, progress);
                    var next = TourRules.ResolveNextAvailableStop(detail, current);
                    var expected = TourRules.ResolveExpectedGeofenceStop(detail, progress);
                    var boosts = TourRules.BuildGeofencePriorityBoosts(detail, progress);
                    var ok = current?.IdGianHang == 10 &&
                             next?.IdGianHang == 30 &&
                             expected?.IdGianHang == 30 &&
                             !boosts.ContainsKey(20) &&
                             boosts.GetValueOrDefault(30) == TourRules.ActiveNextStopBoost;
                    return TourResult("unavailable middle stop is skipped", ok,
                        "current=10 next=30 expected=30 boosts excludes 20",
                        $"current={IdOf(current)} next={IdOf(next)} expected={IdOf(expected)} boosts={FormatBoosts(boosts)}");
                }),
            new VisualTourScenario(
                "progress beyond final stop has no next expected",
                () =>
                {
                    var detail = TourDetailOf(TourStopOf(1, 10), TourStopOf(2, 20));
                    var progress = new TourProgress { StepHienTai = 99 };
                    var current = TourRules.ResolveCurrentStop(detail, progress);
                    var next = TourRules.ResolveNextAvailableStop(detail, current);
                    var expected = TourRules.ResolveExpectedGeofenceStop(detail, progress);
                    var boosts = TourRules.BuildGeofencePriorityBoosts(detail, progress);
                    var ok = current?.IdGianHang == 20 &&
                             next is null &&
                             expected is null &&
                             boosts.GetValueOrDefault(20) == TourRules.ActiveCurrentStopBoost;
                    return TourResult("progress beyond final stop has no next expected", ok,
                        "current=20 next=null expected=null boosts=20:3000",
                        $"current={IdOf(current)} next={IdOf(next)} expected={IdOf(expected)} boosts={FormatBoosts(boosts)}");
                }),
            new VisualTourScenario(
                "first usable stop can start after unavailable stop",
                () =>
                {
                    var detail = TourDetailOf(
                        TourStopOf(1, 10, isAvailable: false),
                        TourStopOf(2, 20),
                        TourStopOf(3, 30));
                    var progress = new TourProgress { StepHienTai = 0 };
                    var current = TourRules.ResolveCurrentStop(detail, progress);
                    var expected = TourRules.ResolveExpectedGeofenceStop(detail, progress);
                    var ok = current?.IdGianHang == 20 && expected?.IdGianHang == 20;
                    return TourResult("first usable stop can start after unavailable stop", ok,
                        "current=20 expected=20",
                        $"current={IdOf(current)} expected={IdOf(expected)}");
                }),
            new VisualTourScenario(
                "coordinate boundary values are usable",
                () =>
                {
                    var detail = TourDetailOf(
                        TourStopOf(1, 10, lat: 90, lon: 180),
                        TourStopOf(2, 20, lat: -90, lon: -180),
                        TourStopOf(3, 30, lat: 90.0001, lon: 106));
                    var usable = TourRules.GetUsableStops(detail).Select(x => x.IdGianHang).ToArray();
                    return TourResult("coordinate boundary values are usable", SameIds(usable, [10, 20]),
                        "usable=[10,20]",
                        $"usable={FormatIds(usable)}");
                })
        ];
    }

    private static List<VisualTourScenario> BuildVisualVisitorScenarios()
    {
        return
        [
            new VisualTourScenario(
                "two visitors enter same geofence independently",
                () =>
                {
                    var visitorA = new VisualVisitorState();
                    var visitorB = new VisualVisitorState();

                    var a = visitorA.Evaluate([1], pendingStoreId: 1);
                    var b = visitorB.Evaluate([1], pendingStoreId: 1);
                    var ok = SameIds(a.Entered, [1]) &&
                             SameIds(b.Entered, [1]) &&
                             a.PendingStoreId == 1 &&
                             b.PendingStoreId == 1;

                    return VisitorResult(
                        "two visitors enter same geofence independently",
                        ok,
                        "du khách A ENTER=[1], pending=1; du khách B ENTER=[1], pending=1",
                        $"du khách A ENTER={FormatIds(a.Entered)}, pending={FormatNullableId(a.PendingStoreId)}; du khách B ENTER={FormatIds(b.Entered)}, pending={FormatNullableId(b.PendingStoreId)}");
                }),
            new VisualTourScenario(
                "one visitor exits while another stays inside",
                () =>
                {
                    var visitorA = new VisualVisitorState();
                    var visitorB = new VisualVisitorState();
                    visitorA.Evaluate([1], pendingStoreId: 1);
                    visitorB.Evaluate([1], pendingStoreId: 1);

                    var a = visitorA.Evaluate([], pendingStoreId: null);
                    var b = visitorB.Evaluate([1], pendingStoreId: 1);
                    var ok = SameIds(a.Exited, [1]) &&
                             a.PendingStoreId is null &&
                             SameIds(b.Entered, []) &&
                             SameIds(b.Exited, []) &&
                             b.PendingStoreId == 1;

                    return VisitorResult(
                        "one visitor exits while another stays inside",
                        ok,
                        "du khách A EXIT=[1], pending=null; du khách B không có ENTER/EXIT mới, pending=1",
                        $"du khách A EXIT={FormatIds(a.Exited)}, pending={FormatNullableId(a.PendingStoreId)}; du khách B ENTER={FormatIds(b.Entered)}, EXIT={FormatIds(b.Exited)}, pending={FormatNullableId(b.PendingStoreId)}");
                }),
            new VisualTourScenario(
                "visitors in different booths keep separate pending audio",
                () =>
                {
                    var visitorA = new VisualVisitorState();
                    var visitorB = new VisualVisitorState();

                    var a = visitorA.Evaluate([1], pendingStoreId: 1);
                    var b = visitorB.Evaluate([2], pendingStoreId: 2);
                    var ok = SameIds(a.Entered, [1]) &&
                             SameIds(b.Entered, [2]) &&
                             a.PendingStoreId == 1 &&
                             b.PendingStoreId == 2;

                    return VisitorResult(
                        "visitors in different booths keep separate pending audio",
                        ok,
                        "du khách A pending=1; du khách B pending=2",
                        $"du khách A ENTER={FormatIds(a.Entered)}, pending={FormatNullableId(a.PendingStoreId)}; du khách B ENTER={FormatIds(b.Entered)}, pending={FormatNullableId(b.PendingStoreId)}");
                }),
            new VisualTourScenario(
                "silent booth visitor does not affect other visitor audio",
                () =>
                {
                    var visitorA = new VisualVisitorState();
                    var visitorB = new VisualVisitorState();

                    var a = visitorA.Evaluate([6], pendingStoreId: null);
                    var b = visitorB.Evaluate([1], pendingStoreId: 1);
                    var ok = SameIds(a.Entered, [6]) &&
                             a.PendingStoreId is null &&
                             SameIds(b.Entered, [1]) &&
                             b.PendingStoreId == 1;

                    return VisitorResult(
                        "silent booth visitor does not affect other visitor audio",
                        ok,
                        "du khách A vào gian hàng không audio nên pending=null; du khách B vẫn pending=1",
                        $"du khách A ENTER={FormatIds(a.Entered)}, pending={FormatNullableId(a.PendingStoreId)}; du khách B ENTER={FormatIds(b.Entered)}, pending={FormatNullableId(b.PendingStoreId)}");
                }),
            new VisualTourScenario(
                "two visitors swap booths independently",
                () =>
                {
                    var visitorA = new VisualVisitorState();
                    var visitorB = new VisualVisitorState();
                    visitorA.Evaluate([1], pendingStoreId: 1);
                    visitorB.Evaluate([2], pendingStoreId: 2);

                    var a = visitorA.Evaluate([2], pendingStoreId: 2);
                    var b = visitorB.Evaluate([1], pendingStoreId: 1);
                    var ok = SameIds(a.Entered, [2]) &&
                             SameIds(a.Exited, [1]) &&
                             a.PendingStoreId == 2 &&
                             SameIds(b.Entered, [1]) &&
                             SameIds(b.Exited, [2]) &&
                             b.PendingStoreId == 1;

                    return VisitorResult(
                        "two visitors swap booths independently",
                        ok,
                        "du khách A EXIT=[1], ENTER=[2], pending=2; du khách B EXIT=[2], ENTER=[1], pending=1",
                        $"du khách A ENTER={FormatIds(a.Entered)}, EXIT={FormatIds(a.Exited)}, pending={FormatNullableId(a.PendingStoreId)}; du khách B ENTER={FormatIds(b.Entered)}, EXIT={FormatIds(b.Exited)}, pending={FormatNullableId(b.PendingStoreId)}");
                }),
            new VisualTourScenario(
                "three visitors mixed audio states are isolated",
                () =>
                {
                    var visitorA = new VisualVisitorState();
                    var visitorB = new VisualVisitorState();
                    var visitorC = new VisualVisitorState();

                    var a = visitorA.Evaluate([1], pendingStoreId: 1);
                    var b = visitorB.Evaluate([6], pendingStoreId: null);
                    var c = visitorC.Evaluate([2], pendingStoreId: 2);
                    var ok = SameIds(a.Entered, [1]) &&
                             a.PendingStoreId == 1 &&
                             SameIds(b.Entered, [6]) &&
                             b.PendingStoreId is null &&
                             SameIds(c.Entered, [2]) &&
                             c.PendingStoreId == 2;

                    return VisitorResult(
                        "three visitors mixed audio states are isolated",
                        ok,
                        "du khách A pending=1; du khách B ở gian hàng không audio pending=null; du khách C pending=2",
                        $"du khách A ENTER={FormatIds(a.Entered)}, pending={FormatNullableId(a.PendingStoreId)}; du khách B ENTER={FormatIds(b.Entered)}, pending={FormatNullableId(b.PendingStoreId)}; du khách C ENTER={FormatIds(c.Entered)}, pending={FormatNullableId(c.PendingStoreId)}");
                }),
            new VisualTourScenario(
                "one visitor repeats same booth while another enters",
                () =>
                {
                    var visitorA = new VisualVisitorState();
                    var visitorB = new VisualVisitorState();
                    visitorA.Evaluate([1], pendingStoreId: 1);

                    var a = visitorA.Evaluate([1], pendingStoreId: 1);
                    var b = visitorB.Evaluate([1], pendingStoreId: 1);
                    var ok = SameIds(a.Entered, []) &&
                             SameIds(a.Exited, []) &&
                             a.PendingStoreId == 1 &&
                             SameIds(b.Entered, [1]) &&
                             SameIds(b.Exited, []) &&
                             b.PendingStoreId == 1;

                    return VisitorResult(
                        "one visitor repeats same booth while another enters",
                        ok,
                        "du khách A vẫn ở gian hàng 1 nên không có ENTER/EXIT mới; du khách B mới vào nên có ENTER=[1]",
                        $"du khách A ENTER={FormatIds(a.Entered)}, EXIT={FormatIds(a.Exited)}, pending={FormatNullableId(a.PendingStoreId)}; du khách B ENTER={FormatIds(b.Entered)}, EXIT={FormatIds(b.Exited)}, pending={FormatNullableId(b.PendingStoreId)}");
                })
        ];
    }

    private static VisualBatchResult EvaluateGeofenceScenario(
        VisualBatchScenario scenario,
        IReadOnlyList<int> actualEntered,
        IReadOnlyList<int> actualExited,
        AudioPlaybackStateSnapshot playbackState)
    {
        var eventsMatch =
            SameIds(actualEntered, scenario.ExpectedEntered) &&
            SameIds(actualExited, scenario.ExpectedExited);

        var playbackMatches = scenario.ExpectedHidden
            ? playbackState.Phase == AudioPlaybackPhase.Hidden
            : playbackState.Phase == AudioPlaybackPhase.Pending &&
              playbackState.StoreId == scenario.ExpectedPendingStoreId;

        var expectedPlayback = scenario.ExpectedHidden
            ? "Hidden"
            : $"Pending({scenario.ExpectedPendingStoreId?.ToString() ?? "null"})";
        var actualPlayback = playbackState.Phase == AudioPlaybackPhase.Pending
            ? $"Pending({playbackState.StoreId?.ToString() ?? "null"})"
            : playbackState.Phase.ToString();

        return new VisualBatchResult(
            Area: "GF",
            Name: scenario.Name,
            Passed: eventsMatch && playbackMatches,
            Expected: $"enter={FormatIds(scenario.ExpectedEntered)}, exit={FormatIds(scenario.ExpectedExited)}, playback={expectedPlayback}",
            Actual: $"enter={FormatIds(actualEntered)}, exit={FormatIds(actualExited)}, playback={actualPlayback}",
            Notes: GetMismatchNotes(
                eventsMatch,
                playbackMatches,
                $"events expected enter={FormatIds(scenario.ExpectedEntered)} exit={FormatIds(scenario.ExpectedExited)} but got enter={FormatIds(actualEntered)} exit={FormatIds(actualExited)}",
                $"playback expected {expectedPlayback} but got {actualPlayback}"));
    }

    private static bool SameIds(IEnumerable<int> actual, IReadOnlyList<int> expected)
    {
        return actual.OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x));
    }

    private static string BuildBatchStatus(
        int passed,
        int completed,
        int total,
        IReadOnlyList<VisualBatchResult> results,
        bool includeDetails)
    {
        var lines = includeDetails
            ? results
            : results.TakeLast(7);

        var renderedLines = lines.Select((x, index) =>
            includeDetails
                ? $"{(x.Passed ? "PASS" : "FAIL")} {x.Area} {index + 1}. {GetVietnameseCaseTitle(x)}\nexpected: {x.Expected}\nactual:   {x.Actual}"
                : $"{(x.Passed ? "PASS" : "FAIL")} {x.Area}. {GetVietnameseCaseTitle(x)}");

        return $"Batch GF+Tour: {passed}/{completed} passed ({total} total)\n" +
               string.Join('\n', renderedLines);
    }

    private static async Task<string> WriteBatchReportAsync(
        int passed,
        int completed,
        int total,
        IReadOnlyList<VisualBatchResult> results,
        CancellationToken cancellationToken)
    {
        var directory = System.IO.Path.Combine(FileSystem.AppDataDirectory, "batch-test-reports");
        Directory.CreateDirectory(directory);

        var fileName = $"bt-report-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
        var filePath = System.IO.Path.Combine(directory, fileName);

        var lines = new List<string>
        {
            "BÁO CÁO KIỂM THỬ BATCH C-SA-T",
            $"Thời gian tạo: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"Tổng kết: {passed}/{completed} trường hợp đạt ({total} tổng số)",
            string.Empty
        };

        foreach (var (result, index) in results.Select((value, index) => (value, index + 1)))
        {
            lines.Add($"TRƯỜNG HỢP {index:00}: {GetVietnameseCaseTitle(result)}");
            lines.Add($"Nhóm: {GetVietnameseAreaName(result.Area)}");
            lines.Add($"Kết quả: {(result.Passed ? "ĐẠT" : "KHÔNG ĐẠT")}");
            lines.Add($"Chi tiết trường hợp: {GetVietnameseCaseDetail(result)}");
            lines.Add($"Mong đợi: {TranslateExpectation(result.Expected)}");
            lines.Add($"Thực tế: {TranslateExpectation(result.Actual)}");
            lines.Add($"Ghi chú: {TranslateNotes(result.Notes)}");
            lines.Add(string.Empty);
        }

        await File.WriteAllTextAsync(filePath, string.Join(Environment.NewLine, lines), cancellationToken);
        return await TryExportBatchReportToDownloadsAsync(filePath, fileName, cancellationToken);
    }

    private static async Task<string> TryExportBatchReportToDownloadsAsync(
        string sourceFilePath,
        string fileName,
        CancellationToken cancellationToken)
    {
        try
        {
#if ANDROID
            if (!OperatingSystem.IsAndroidVersionAtLeast(29))
                return sourceFilePath;

            var context = Android.App.Application.Context;
            var resolver = context.ContentResolver;
            if (resolver is null)
                return sourceFilePath;

            var values = new Android.Content.ContentValues();
            values.Put(Android.Provider.MediaStore.IMediaColumns.DisplayName, fileName);
            values.Put(Android.Provider.MediaStore.IMediaColumns.MimeType, "text/plain");
            values.Put(Android.Provider.MediaStore.IMediaColumns.RelativePath, Android.OS.Environment.DirectoryDownloads);

            var collection = Android.Provider.MediaStore.Downloads.GetContentUri(Android.Provider.MediaStore.VolumeExternalPrimary);
            var uri = resolver.Insert(collection, values);
            if (uri is null)
                return sourceFilePath;

            await using var input = File.OpenRead(sourceFilePath);
            await using var output = resolver.OpenOutputStream(uri);
            if (output is null)
                return sourceFilePath;

            await input.CopyToAsync(output, cancellationToken);
            return $"Downloads/{fileName}";
#else
            var downloads = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads");
            Directory.CreateDirectory(downloads);
            var destination = System.IO.Path.Combine(downloads, fileName);
            File.Copy(sourceFilePath, destination, overwrite: true);
            return destination;
#endif
        }
        catch
        {
            return sourceFilePath;
        }
    }

    private static VisualBatchResult TourResult(string name, bool passed, string expected, string actual)
    {
        return new VisualBatchResult(
            Area: "TOUR",
            Name: name,
            Passed: passed,
            Expected: expected,
            Actual: actual,
            Notes: passed ? "Matched." : "Tour rule output did not match expected values.");
    }

    private static VisualBatchResult VisitorResult(string name, bool passed, string expected, string actual)
    {
        return new VisualBatchResult(
            Area: "VISITOR",
            Name: name,
            Passed: passed,
            Expected: expected,
            Actual: actual,
            Notes: passed ? "Matched." : "Trạng thái giữa các du khách không độc lập như mong đợi.");
    }

    private static string GetMismatchNotes(
        bool eventsMatch,
        bool playbackMatches,
        string eventMismatch,
        string playbackMismatch)
    {
        if (eventsMatch && playbackMatches)
            return "Matched.";

        var notes = new List<string>();
        if (!eventsMatch)
            notes.Add(eventMismatch);
        if (!playbackMatches)
            notes.Add(playbackMismatch);

        return string.Join("; ", notes);
    }

    private static string GetVietnameseAreaName(string area)
    {
        return area switch
        {
            "GF" => "Geofence và autoplay",
            "TOUR" => "Tour",
            "VISITOR" => "Nhiều du khách / nhiều thiết bị",
            _ => area
        };
    }

    private static string GetVietnameseCaseTitle(VisualBatchResult result)
    {
        return result.Name switch
        {
            "outside all" => "Đứng ngoài tất cả vùng geofence",
            "enter A" => "Đi vào geofence gian hàng A",
            "overlap A+B, B boosted" => "Đi vào vùng chồng lấp A và B, B được ưu tiên",
            "exit A, stay B" => "Thoát khỏi A nhưng vẫn ở trong B",
            "exit B, enter C" => "Thoát khỏi B và đi vào C",
            "exit all" => "Thoát khỏi tất cả geofence",
            "boundary exact is inside" => "Đứng đúng trên biên bán kính geofence",
            "boundary just outside" => "Đứng ngay ngoài biên bán kính geofence",
            "same distance chooses higher fee" => "Cùng khoảng cách thì chọn gian hàng phí cao hơn",
            "same distance and fee chooses lower id" => "Cùng khoảng cách và phí thì chọn id gian hàng nhỏ hơn",
            "closer target beats higher fee" => "Gian hàng gần hơn thắng gian hàng phí cao hơn",
            "target without audio stays hidden" => "Gian hàng không có audio thì không hiện autoplay",
            "re-enter A after reset" => "Vào lại A sau khi reset trạng thái",
            "stay A no duplicate enter" => "Đứng yên trong A không phát sinh ENTER lặp",
            "exit A cancels pending" => "Thoát khỏi A thì hủy pending autoplay",
            "re-enter A after actual exit" => "Vào lại A sau khi đã thoát vùng thật",
            "usable stops skip invalid/unavailable" => "Tour bỏ qua stop không hợp lệ hoặc không khả dụng",
            "initial tour expects first stop" => "Tour mới bắt đầu yêu cầu stop đầu tiên",
            "active step expects next stop" => "Tour đang chạy yêu cầu stop kế tiếp",
            "step gaps skip to next usable stop" => "Tour có khoảng trống thứ tự vẫn nhảy tới stop khả dụng kế tiếp",
            "final step still expects final geofence" => "Bước cuối vẫn phải vào geofence của stop cuối",
            "completed progress blocks more advances" => "Tour đã hoàn thành thì không advance tiếp",
            "empty tour has no current/boosts" => "Tour rỗng không có current stop và không có boost",
            "non-positive store ids are not boosted" => "Id gian hàng không hợp lệ không được boost",
            "progress ratio middle and complete" => "Tính tiến độ tour ở giữa và khi hoàn thành",
            "invalid coordinate stops are ignored" => "Stop có tọa độ sai bị bỏ qua",
            "unavailable middle stop is skipped" => "Tour bỏ qua stop giữa chặng không khả dụng",
            "progress beyond final stop has no next expected" => "Tiến độ vượt quá stop cuối không yêu cầu next stop",
            "first usable stop can start after unavailable stop" => "Stop đầu không khả dụng thì tour bắt đầu ở stop hợp lệ kế tiếp",
            "coordinate boundary values are usable" => "Tọa độ đúng biên địa lý vẫn hợp lệ",
            "two visitors enter same geofence independently" => "Hai du khách cùng vào một geofence",
            "one visitor exits while another stays inside" => "Một du khách thoát vùng, du khách khác vẫn ở trong vùng",
            "visitors in different booths keep separate pending audio" => "Hai du khách ở hai gian hàng khác nhau",
            "silent booth visitor does not affect other visitor audio" => "Du khách ở gian hàng không audio không ảnh hưởng du khách khác",
            "two visitors swap booths independently" => "Hai du khách đổi chéo gian hàng độc lập",
            "three visitors mixed audio states are isolated" => "Ba du khách có trạng thái audio khác nhau vẫn tách biệt",
            "one visitor repeats same booth while another enters" => "Một du khách đứng yên, du khách khác mới vào cùng gian hàng",
            _ => result.Name
        };
    }

    private static string GetVietnameseCaseDetail(VisualBatchResult result)
    {
        return result.Name switch
        {
            "outside all" => "Giả lập vị trí người dùng nằm ngoài mọi bán kính geofence; app không được tạo sự kiện vào/ra và audio phải ẩn.",
            "enter A" => "Giả lập người dùng đi vào bán kính của gian hàng A lần đầu; app phải phát sinh ENTER cho A và đặt audio A vào trạng thái chờ phát.",
            "overlap A+B, B boosted" => "Giả lập người dùng đứng trong vùng chồng lấp của A và B; B có boost ưu tiên nên B phải thắng autoplay.",
            "exit A, stay B" => "Giả lập người dùng rời khỏi A nhưng vẫn nằm trong B; app chỉ được báo EXIT A và tiếp tục giữ pending của B.",
            "exit B, enter C" => "Giả lập người dùng rời khỏi B đồng thời vào C; app phải báo EXIT B, ENTER C và chuyển pending sang C.",
            "exit all" => "Giả lập người dùng rời khỏi tất cả geofence; pending autoplay phải bị ẩn.",
            "boundary exact is inside" => "Giả lập vị trí đúng bằng bán kính geofence; theo rule distance <= radius thì vẫn tính là ở trong.",
            "boundary just outside" => "Giả lập vị trí lớn hơn bán kính 1 mét; app không được tính là ở trong geofence.",
            "same distance chooses higher fee" => "Giả lập hai gian hàng D và E cùng vị trí, cùng khoảng cách; gian hàng có phí hằng tháng cao hơn phải được chọn.",
            "same distance and fee chooses lower id" => "Giả lập hai gian hàng H và I cùng vị trí, cùng khoảng cách, cùng phí; app phải dùng id nhỏ hơn làm tiêu chí cuối để chọn autoplay.",
            "closer target beats higher fee" => "Giả lập người dùng nằm trong hai gian hàng J và K; J có phí cao hơn nhưng xa hơn, nên K gần hơn phải được chọn trước.",
            "target without audio stays hidden" => "Giả lập vào gian hàng F không có AudioURL; app có thể ghi ENTER nhưng không được hiện pending audio.",
            "re-enter A after reset" => "Reset trạng thái geofence rồi vào lại A; app phải phát sinh ENTER A mới.",
            "stay A no duplicate enter" => "Sau khi đã ở trong A, di chuyển nhẹ vẫn trong A; app không được phát sinh ENTER lặp.",
            "exit A cancels pending" => "Sau khi A đang pending autoplay, giả lập thoát khỏi A; pending phải bị hủy và audio ẩn.",
            "re-enter A after actual exit" => "Sau khi đã thoát khỏi A thật sự, giả lập đi vào A lần nữa; app phải tạo ENTER mới và bật lại pending audio cho A.",
            "usable stops skip invalid/unavailable" => "Tạo tour có stop đúng, stop bị tắt, stop thiếu tọa độ và stop tọa độ NaN; chỉ stop hợp lệ được dùng.",
            "initial tour expects first stop" => "Tour chưa có tiến độ; current stop phải là stop đầu tiên, next stop là stop thứ hai, boost phải đúng mức khởi đầu.",
            "active step expects next stop" => "Tour đang có StepHienTai=2; app phải xem stop 1 là current và stop 2 là geofence cần vào tiếp theo.",
            "step gaps skip to next usable stop" => "Tour có thứ tự 1,3,5 và progress ở bước 4; rule phải suy ra current là stop 3 và next là stop 5.",
            "final step still expects final geofence" => "Khi StepHienTai trỏ tới stop cuối nhưng chưa completed, app vẫn phải cho người dùng vào geofence stop cuối.",
            "completed progress blocks more advances" => "Khi IsCompleted=true, app không được yêu cầu geofence nào nữa để advance tour.",
            "empty tour has no current/boosts" => "Tour không có stop hợp lệ thì current stop null, danh sách boost rỗng, progress UI coi như 100%.",
            "non-positive store ids are not boosted" => "Stop có IdGianHang bằng 0 hoặc âm không hợp lệ, không được đưa vào bảng priority boost.",
            "progress ratio middle and complete" => "Kiểm tra cách tính progress bar: ở stop đầu của 3 stop là 1/3, khi hết next stop là 1.0.",
            "invalid coordinate stops are ignored" => "Stop có lat/lon vượt miền hợp lệ hoặc infinity phải bị loại khỏi danh sách usable stops.",
            "unavailable middle stop is skipped" => "Tour có stop 2 bị tắt ở giữa chặng; khi tiến độ đang tới bước 2, app phải bỏ qua stop này và yêu cầu geofence stop 3.",
            "progress beyond final stop has no next expected" => "Khi StepHienTai lớn hơn mọi ThuTu nhưng tour chưa completed, current được ghim ở stop cuối và không còn geofence kế tiếp để yêu cầu.",
            "first usable stop can start after unavailable stop" => "Tour có stop đầu tiên không khả dụng; ở trạng thái mới bắt đầu, app phải chọn stop hợp lệ kế tiếp làm current và expected.",
            "coordinate boundary values are usable" => "Kiểm tra tọa độ ở đúng biên lat/lon như 90, 180, -90, -180; các giá trị đúng biên vẫn phải được coi là hợp lệ.",
            "two visitors enter same geofence independently" => "Mô phỏng hai thiết bị/du khách cùng đi vào geofence của gian hàng 1. Mỗi thiết bị phải có ENTER và pending audio riêng, không dùng chung trạng thái.",
            "one visitor exits while another stays inside" => "Mô phỏng hai du khách ban đầu cùng ở trong gian hàng 1, sau đó du khách A rời khỏi vùng còn du khách B vẫn ở trong vùng. Việc A rời đi không được hủy pending audio của B.",
            "visitors in different booths keep separate pending audio" => "Mô phỏng du khách A ở gian hàng 1 và du khách B ở gian hàng 2. Mỗi người phải giữ pending audio theo gian hàng của chính mình.",
            "silent booth visitor does not affect other visitor audio" => "Mô phỏng du khách A vào gian hàng không có audio, trong khi du khách B vào gian hàng có audio. A không có pending nhưng không được làm mất pending của B.",
            "two visitors swap booths independently" => "Mô phỏng du khách A từ gian hàng 1 sang 2 và du khách B từ gian hàng 2 sang 1. Mỗi người phải có EXIT/ENTER của riêng mình và pending mới đúng theo vị trí mới.",
            "three visitors mixed audio states are isolated" => "Mô phỏng ba du khách cùng lúc: một người ở gian hàng có audio 1, một người ở gian hàng không audio 6, một người ở gian hàng có audio 2. Trạng thái audio không được lẫn nhau.",
            "one visitor repeats same booth while another enters" => "Mô phỏng du khách A đã ở trong gian hàng 1 và tiếp tục đứng đó, trong khi du khách B mới vào gian hàng 1. A không được phát sinh ENTER lặp, B vẫn phải có ENTER mới.",
            _ => "Chưa có mô tả chi tiết cho trường hợp này."
        };
    }

    private static string TranslateExpectation(string value)
    {
        return value
            .Replace("enter=", "ENTER=")
            .Replace("exit=", "EXIT=")
            .Replace("playback=", "Trạng thái audio=")
            .Replace("Hidden", "Ẩn")
            .Replace("Pending", "Chờ phát")
            .Replace("current=", "current stop=")
            .Replace("next=", "next stop=")
            .Replace("expected=", "geofence cần vào=")
            .Replace("pending=", "audio chờ phát=")
            .Replace("usable=", "usable stops=")
            .Replace("boosts=", "priority boosts=")
            .Replace("progress=", "tiến độ=")
            .Replace("middle=", "giữa tour=")
            .Replace("complete=", "hoàn thành=")
            .Replace("null", "không có");
    }

    private static string TranslateNotes(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes) || notes == "Matched.")
            return "Đúng như mong đợi.";

        return notes
            .Replace("events expected", "Sự kiện mong đợi")
            .Replace("but got", "nhưng thực tế là")
            .Replace("playback expected", "Audio mong đợi")
            .Replace("Tour rule output did not match expected values.", "Kết quả rule tour không khớp với giá trị mong đợi.")
            .Replace("Trạng thái giữa các du khách không độc lập như mong đợi.", "Trạng thái giữa các du khách không độc lập như mong đợi.")
            .Replace("Matched.", "Đúng như mong đợi.");
    }

    private static string FormatNullableId(int? id) => id?.ToString() ?? "null";

    private static TourDetail TourDetailOf(params TourStop[] stops)
    {
        return new TourDetail
        {
            Tour = new TourSummary
            {
                IdTour = 99,
                Ten = "BT Tour"
            },
            DanhSachStop = stops.ToList()
        };
    }

    private static TourStop TourStopOf(
        int order,
        int storeId,
        bool isAvailable = true,
        double? lat = 10.0,
        double? lon = 106.0)
    {
        return new TourStop
        {
            IdTourDiem = order,
            IdTour = 99,
            IdGianHang = storeId,
            ThuTu = order,
            IsAvailable = isAvailable,
            Lat = lat,
            Lon = lon,
            TenGianHang = $"Tour stop {storeId}"
        };
    }

    private static string FormatIds(IEnumerable<int> ids)
    {
        var list = ids.OrderBy(x => x).ToList();
        return list.Count == 0 ? "[]" : $"[{string.Join(",", list)}]";
    }

    private static string FormatBoosts(IReadOnlyDictionary<int, int> boosts)
    {
        return boosts.Count == 0
            ? "{}"
            : "{" + string.Join(",", boosts.OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Value}")) + "}";
    }

    private static string IdOf(TourStop? stop) => stop?.IdGianHang.ToString() ?? "null";

    private void DrawVisualTestOverlay(
        IReadOnlyList<GianHang> stops,
        IReadOnlyList<VisualTestRoutePoint> route)
    {
        ClearVisualTestOverlay();

        foreach (var stop in stops)
        {
            var circle = new MapCircle
            {
                Center = new Location(stop.Lat!.Value, stop.Lon!.Value),
                Radius = Distance.FromMeters(VisualTestGeofenceRadiusMeters),
                StrokeColor = Color.FromArgb("#16A34A"),
                StrokeWidth = 2,
                FillColor = Color.FromRgba(34, 197, 94, 42)
            };
            _map.MapElements.Add(circle);
            _visualTestMapElements.Add(circle);
        }

        var line = new MapPolyline
        {
            StrokeColor = Color.FromArgb("#2563EB"),
            StrokeWidth = 5
        };

        foreach (var point in route)
            line.Geopath.Add(new Location(point.Latitude, point.Longitude));

        _map.MapElements.Add(line);
        _visualTestMapElements.Add(line);

        for (var i = 0; i < route.Count; i++)
        {
            var point = route[i];
            var pin = new Pin
            {
                Label = $"Case {i + 1}",
                Address = point.Label,
                Type = PinType.SearchResult,
                Location = new Location(point.Latitude, point.Longitude)
            };
            _map.Pins.Add(pin);
            _visualTestPins.Add(pin);
        }
    }

    private void CenterVisualTestRoute(IReadOnlyList<VisualTestRoutePoint> route)
    {
        var minLat = route.Min(x => x.Latitude);
        var maxLat = route.Max(x => x.Latitude);
        var minLon = route.Min(x => x.Longitude);
        var maxLon = route.Max(x => x.Longitude);
        var center = new Location((minLat + maxLat) / 2d, (minLon + maxLon) / 2d);
        var farthestMeters = route.Max(x =>
            Location.CalculateDistance(center.Latitude, center.Longitude, x.Latitude, x.Longitude, DistanceUnits.Kilometers) * 1000d);

        _map.MoveToRegion(MapSpan.FromCenterAndRadius(center, Distance.FromMeters(Math.Max(180d, farthestMeters + 120d))));
        RestoreMapModeAfterRegionMove();
    }

    private void StopVisualGeofenceTest(bool clearOverlay)
    {
        _visualTestCts?.Cancel();
        _geofenceEngine.ClearDebugLocation();
        _shouldFollowLiveLocation = false;
        _visualTestStatusBanner.IsVisible = false;
        StartLiveLocationPolling();

        if (clearOverlay)
            ClearVisualTestOverlay();
    }

    private void ClearVisualTestOverlay()
    {
        foreach (var element in _visualTestMapElements)
            _map.MapElements.Remove(element);
        _visualTestMapElements.Clear();

        foreach (var pin in _visualTestPins)
            _map.Pins.Remove(pin);
        _visualTestPins.Clear();
    }

    private void UpdateVisualTestStatus(string text)
    {
        if (_visualTestStatusLabel is not null)
            _visualTestStatusLabel.Text = text;
    }

    private sealed record VisualTestRoutePoint(string Label, double Latitude, double Longitude);

    private sealed record VisualBatchScenario(
        string Name,
        VisualTestRoutePoint Point,
        IReadOnlyList<int> ExpectedEntered,
        IReadOnlyList<int> ExpectedExited,
        int? ExpectedPendingStoreId,
        bool ExpectedHidden,
        bool ResetBefore);

    private sealed record VisualTourScenario(string Name, Func<VisualBatchResult> Run);

    private sealed record VisualBatchResult(
        string Area,
        string Name,
        bool Passed,
        string Expected,
        string Actual,
        string Notes);

    private sealed class VisualVisitorState
    {
        private readonly HashSet<int> _insideIds = [];

        public VisualVisitorTick Evaluate(IReadOnlyList<int> currentInsideIds, int? pendingStoreId)
        {
            var current = currentInsideIds.ToHashSet();
            var entered = current
                .Where(id => !_insideIds.Contains(id))
                .OrderBy(id => id)
                .ToList();
            var exited = _insideIds
                .Where(id => !current.Contains(id))
                .OrderBy(id => id)
                .ToList();

            _insideIds.Clear();
            foreach (var id in current)
                _insideIds.Add(id);

            return new VisualVisitorTick(entered, exited, pendingStoreId);
        }
    }

    private sealed record VisualVisitorTick(
        IReadOnlyList<int> Entered,
        IReadOnlyList<int> Exited,
        int? PendingStoreId);
}
