using MauiApp1.Models;
using MauiApp1.Services;

namespace C_SA_T.Tests;

public sealed class TourRulesTests
{
    [Fact]
    public void GetUsableStops_ReturnsAvailableStopsWithValidCoordinatesInOrder()
    {
        var detail = Detail(
            Stop(3, 30),
            Stop(1, 10),
            Stop(2, 20, isAvailable: false),
            Stop(4, 40, lat: null),
            Stop(5, 50, lon: double.NaN));

        var stops = TourRules.GetUsableStops(detail);

        Assert.Equal([10, 30], stops.Select(x => x.IdGianHang));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(double.NaN, false)]
    [InlineData(double.PositiveInfinity, false)]
    [InlineData(-91.0, false)]
    [InlineData(91.0, false)]
    [InlineData(-90.0, true)]
    [InlineData(90.0, true)]
    public void HasValidCoordinate_ValidatesLatitude(double? lat, bool expected)
    {
        var stop = Stop(1, 1, lat: lat, lon: 106.0);

        Assert.Equal(expected, TourRules.HasValidCoordinate(stop));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(double.NaN, false)]
    [InlineData(double.NegativeInfinity, false)]
    [InlineData(-181.0, false)]
    [InlineData(181.0, false)]
    [InlineData(-180.0, true)]
    [InlineData(180.0, true)]
    public void HasValidCoordinate_ValidatesLongitude(double? lon, bool expected)
    {
        var stop = Stop(1, 1, lat: 10.0, lon: lon);

        Assert.Equal(expected, TourRules.HasValidCoordinate(stop));
    }

    [Fact]
    public void ResolveCurrentStop_NoProgress_ReturnsFirstUsableStop()
    {
        var detail = Detail(Stop(2, 20), Stop(1, 10));

        var current = TourRules.ResolveCurrentStop(detail, progress: null);

        Assert.Equal(10, current?.IdGianHang);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 10)]
    [InlineData(2, 10)]
    [InlineData(3, 10)]
    [InlineData(4, 20)]
    [InlineData(5, 20)]
    [InlineData(6, 30)]
    public void ResolveCurrentStop_HandlesStepGapsAndPastEnd(int step, int expectedStoreId)
    {
        var detail = Detail(
            Stop(1, 10),
            Stop(3, 20),
            Stop(5, 30));

        var current = TourRules.ResolveCurrentStop(detail, new TourProgress { StepHienTai = step });

        Assert.Equal(expectedStoreId, current?.IdGianHang);
    }

    [Fact]
    public void ResolveCurrentStop_IgnoresUnavailableAndInvalidStops()
    {
        var detail = Detail(
            Stop(1, 10, isAvailable: false),
            Stop(2, 20, lat: 99),
            Stop(3, 30));

        var current = TourRules.ResolveCurrentStop(detail, new TourProgress { StepHienTai = 1 });

        Assert.Equal(30, current?.IdGianHang);
    }

    [Fact]
    public void ResolveCurrentStop_NoUsableStops_ReturnsNull()
    {
        var detail = Detail(
            Stop(1, 10, isAvailable: false),
            Stop(2, 20, lat: null));

        var current = TourRules.ResolveCurrentStop(detail, null);

        Assert.Null(current);
    }

    [Theory]
    [InlineData(1, 20)]
    [InlineData(2, 30)]
    [InlineData(3, null)]
    public void ResolveNextAvailableStop_ReturnsNextUsableStop(int currentOrder, int? expectedStoreId)
    {
        var detail = Detail(
            Stop(1, 10),
            Stop(2, 20),
            Stop(3, 30),
            Stop(4, 40, isAvailable: false));
        var current = detail.DanhSachStop.First(x => x.ThuTu == currentOrder);

        var next = TourRules.ResolveNextAvailableStop(detail, current);

        Assert.Equal(expectedStoreId, next?.IdGianHang);
    }

    [Fact]
    public void ResolveNextAvailableStop_NullCurrent_ReturnsNull()
    {
        var next = TourRules.ResolveNextAvailableStop(Detail(Stop(1, 10)), null);

        Assert.Null(next);
    }

    [Fact]
    public void ResolveExpectedGeofenceStop_InitialProgress_ExpectsCurrentStop()
    {
        var detail = Detail(Stop(1, 10), Stop(2, 20));

        var expected = TourRules.ResolveExpectedGeofenceStop(detail, new TourProgress { StepHienTai = 0 });

        Assert.Equal(10, expected?.IdGianHang);
    }

    [Fact]
    public void ResolveExpectedGeofenceStop_ActiveProgress_ExpectsNextStop()
    {
        var detail = Detail(Stop(1, 10), Stop(2, 20), Stop(3, 30));

        var expected = TourRules.ResolveExpectedGeofenceStop(detail, new TourProgress { StepHienTai = 2 });

        Assert.Equal(20, expected?.IdGianHang);
    }

    [Fact]
    public void ResolveExpectedGeofenceStop_FinalStep_ExpectsFinalStop()
    {
        var detail = Detail(Stop(1, 10), Stop(2, 20));

        var expected = TourRules.ResolveExpectedGeofenceStop(detail, new TourProgress { StepHienTai = 2 });

        Assert.Equal(20, expected?.IdGianHang);
    }

    [Fact]
    public void ResolveExpectedGeofenceStop_CompletedProgress_ReturnsNull()
    {
        var detail = Detail(Stop(1, 10), Stop(2, 20));

        var expected = TourRules.ResolveExpectedGeofenceStop(
            detail,
            new TourProgress { StepHienTai = 2, IsCompleted = true });

        Assert.Null(expected);
    }

    [Fact]
    public void BuildGeofencePriorityBoosts_InitialProgress_BoostsFirstAndSecondStop()
    {
        var detail = Detail(Stop(1, 10), Stop(2, 20), Stop(3, 30));

        var boosts = TourRules.BuildGeofencePriorityBoosts(detail, new TourProgress { StepHienTai = 0 });

        Assert.Equal(TourRules.InitialCurrentStopBoost, boosts[10]);
        Assert.Equal(TourRules.InitialNextStopBoost, boosts[20]);
        Assert.Equal(TourRules.BaseTourStopBoost, boosts[30]);
    }

    [Fact]
    public void BuildGeofencePriorityBoosts_ActiveProgress_BoostsCurrentAndNextStop()
    {
        var detail = Detail(Stop(1, 10), Stop(2, 20), Stop(3, 30));

        var boosts = TourRules.BuildGeofencePriorityBoosts(detail, new TourProgress { StepHienTai = 2 });

        Assert.Equal(TourRules.ActiveCurrentStopBoost, boosts[10]);
        Assert.Equal(TourRules.ActiveNextStopBoost, boosts[20]);
        Assert.Equal(TourRules.BaseTourStopBoost, boosts[30]);
    }

    [Fact]
    public void BuildGeofencePriorityBoosts_AfterLastStop_OnlyCurrentFinalGetsActiveBoost()
    {
        var detail = Detail(Stop(1, 10), Stop(2, 20));

        var boosts = TourRules.BuildGeofencePriorityBoosts(detail, new TourProgress { StepHienTai = 99 });

        Assert.Equal(TourRules.BaseTourStopBoost, boosts[10]);
        Assert.Equal(TourRules.ActiveCurrentStopBoost, boosts[20]);
    }

    [Fact]
    public void BuildGeofencePriorityBoosts_SkipsInvalidAndNonPositiveStoreIds()
    {
        var detail = Detail(
            Stop(1, 10),
            Stop(2, 0),
            Stop(3, -1),
            Stop(4, 40, lat: 99));

        var boosts = TourRules.BuildGeofencePriorityBoosts(detail, null);

        Assert.Contains(10, boosts.Keys);
        Assert.DoesNotContain(0, boosts.Keys);
        Assert.DoesNotContain(-1, boosts.Keys);
        Assert.DoesNotContain(40, boosts.Keys);
    }

    [Fact]
    public void BuildGeofencePriorityBoosts_EmptyTour_ReturnsEmpty()
    {
        var boosts = TourRules.BuildGeofencePriorityBoosts(Detail(), null);

        Assert.Empty(boosts);
    }

    [Fact]
    public void CalculateProgressRatio_StartOfThreeStops_IsOneThird()
    {
        var detail = Detail(Stop(1, 10), Stop(2, 20), Stop(3, 30));
        var current = detail.DanhSachStop[0];
        var next = detail.DanhSachStop[1];

        var ratio = TourRules.CalculateProgressRatio(detail, current, next);

        Assert.Equal(1d / 3d, ratio, precision: 6);
    }

    [Fact]
    public void CalculateProgressRatio_NoNextStop_IsComplete()
    {
        var detail = Detail(Stop(1, 10), Stop(2, 20), Stop(3, 30));
        var current = detail.DanhSachStop[2];

        var ratio = TourRules.CalculateProgressRatio(detail, current, nextStop: null);

        Assert.Equal(1d, ratio);
    }

    [Fact]
    public void CalculateProgressRatio_EmptyTour_IsCompleteEnoughForUi()
    {
        var ratio = TourRules.CalculateProgressRatio(Detail(), null, null);

        Assert.Equal(1d, ratio);
    }

    private static TourDetail Detail(params TourStop[] stops)
    {
        return new TourDetail
        {
            Tour = new TourSummary { IdTour = 1, Ten = "Test Tour" },
            DanhSachStop = stops.ToList()
        };
    }

    private static TourStop Stop(
        int order,
        int storeId,
        bool isAvailable = true,
        double? lat = 10.0,
        double? lon = 106.0)
    {
        return new TourStop
        {
            IdTourDiem = order,
            IdTour = 1,
            IdGianHang = storeId,
            ThuTu = order,
            IsAvailable = isAvailable,
            Lat = lat,
            Lon = lon,
            TenGianHang = $"Stop {storeId}"
        };
    }
}
