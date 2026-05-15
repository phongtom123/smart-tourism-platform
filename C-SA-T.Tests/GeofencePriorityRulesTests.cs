using MauiApp1.Services;

namespace C_SA_T.Tests;

public sealed class GeofencePriorityRulesTests
{
    [Fact]
    public void Prioritize_Empty_ReturnsEmpty()
    {
        var ordered = Prioritize([], new Dictionary<int, int>()).ToList();

        Assert.Empty(ordered);
    }

    [Fact]
    public void Prioritize_NoBoost_UsesDistanceRatioBeforeFee()
    {
        var targets = new[]
        {
            new Candidate(1, Distance: 8, Radius: 10, Fee: 900_000m),
            new Candidate(2, Distance: 3, Radius: 10, Fee: 100_000m)
        };

        var winner = Prioritize(targets, new Dictionary<int, int>()).First();

        Assert.Equal(2, winner.Id);
    }

    [Fact]
    public void Prioritize_UsesDistanceDividedByRadius_NotRawDistance()
    {
        var targets = new[]
        {
            new Candidate(1, Distance: 6, Radius: 30, Fee: 100_000m),
            new Candidate(2, Distance: 4, Radius: 5, Fee: 900_000m)
        };

        var winner = Prioritize(targets, new Dictionary<int, int>()).First();

        Assert.Equal(1, winner.Id);
    }

    [Fact]
    public void Prioritize_BoostBeatsCloserHigherFeeTarget()
    {
        var targets = new[]
        {
            new Candidate(1, Distance: 10, Radius: 10, Fee: 10_000m),
            new Candidate(2, Distance: 0, Radius: 10, Fee: 900_000m)
        };

        var winner = Prioritize(targets, new Dictionary<int, int> { [1] = 1 });

        Assert.Equal(1, winner.First().Id);
    }

    [Fact]
    public void Prioritize_HigherBoostWins()
    {
        var targets = new[]
        {
            new Candidate(1, Distance: 0, Radius: 10, Fee: 900_000m),
            new Candidate(2, Distance: 9, Radius: 10, Fee: 100_000m)
        };

        var winner = Prioritize(targets, new Dictionary<int, int> { [1] = 1, [2] = 2 }).First();

        Assert.Equal(2, winner.Id);
    }

    [Fact]
    public void Prioritize_SameBoost_UsesDistanceRatio()
    {
        var targets = new[]
        {
            new Candidate(1, Distance: 7, Radius: 10, Fee: 900_000m),
            new Candidate(2, Distance: 2, Radius: 10, Fee: 100_000m)
        };

        var winner = Prioritize(targets, new Dictionary<int, int> { [1] = 5, [2] = 5 }).First();

        Assert.Equal(2, winner.Id);
    }

    [Fact]
    public void Prioritize_SameRatio_UsesHigherMonthlyFee()
    {
        var targets = new[]
        {
            new Candidate(1, Distance: 5, Radius: 10, Fee: 100_000m),
            new Candidate(2, Distance: 5, Radius: 10, Fee: 300_000m)
        };

        var winner = Prioritize(targets, new Dictionary<int, int>()).First();

        Assert.Equal(2, winner.Id);
    }

    [Fact]
    public void Prioritize_SameBoostRatioAndFee_UsesLowerId()
    {
        var targets = new[]
        {
            new Candidate(9, Distance: 5, Radius: 10, Fee: 100_000m),
            new Candidate(3, Distance: 5, Radius: 10, Fee: 100_000m)
        };

        var winner = Prioritize(targets, new Dictionary<int, int>()).First();

        Assert.Equal(3, winner.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Prioritize_NonPositiveRadius_LosesRatioTieBreaker(double radius)
    {
        var targets = new[]
        {
            new Candidate(1, Distance: 0, Radius: radius, Fee: 999_000m),
            new Candidate(2, Distance: 9, Radius: 10, Fee: 1_000m)
        };

        var winner = Prioritize(targets, new Dictionary<int, int>()).First();

        Assert.Equal(2, winner.Id);
    }

    [Fact]
    public void Prioritize_NonPositiveRadius_CanStillWinWithBoost()
    {
        var targets = new[]
        {
            new Candidate(1, Distance: 0, Radius: 0, Fee: 1_000m),
            new Candidate(2, Distance: 0, Radius: 10, Fee: 999_000m)
        };

        var winner = Prioritize(targets, new Dictionary<int, int> { [1] = 10 }).First();

        Assert.Equal(1, winner.Id);
    }

    [Fact]
    public void Prioritize_MissingBoostDefaultsToZero()
    {
        var targets = new[]
        {
            new Candidate(1, Distance: 0, Radius: 10, Fee: 1_000m),
            new Candidate(2, Distance: 1, Radius: 10, Fee: 999_000m)
        };

        var winner = Prioritize(targets, new Dictionary<int, int> { [999] = 10 }).First();

        Assert.Equal(1, winner.Id);
    }

    [Fact]
    public void Prioritize_OrderIsStableAcrossMultipleTargets()
    {
        var targets = new[]
        {
            new Candidate(4, Distance: 5, Radius: 10, Fee: 100_000m),
            new Candidate(2, Distance: 8, Radius: 10, Fee: 999_000m),
            new Candidate(3, Distance: 3, Radius: 10, Fee: 100_000m),
            new Candidate(1, Distance: 1, Radius: 10, Fee: 100_000m)
        };

        var order = Prioritize(targets, new Dictionary<int, int> { [2] = 1 }).Select(x => x.Id).ToList();

        Assert.Equal([2, 1, 3, 4], order);
    }

    private static IOrderedEnumerable<Candidate> Prioritize(
        IEnumerable<Candidate> targets,
        IReadOnlyDictionary<int, int> boosts)
    {
        return GeofencePriorityRules.Prioritize(
            targets,
            boosts,
            x => x.Id,
            x => x.Distance,
            x => x.Radius,
            x => x.Fee);
    }

    private sealed record Candidate(int Id, double Distance, double Radius, decimal Fee);
}
