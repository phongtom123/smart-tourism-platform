namespace MauiApp1.Services;

public static class GeofencePriorityRules
{
    public static IOrderedEnumerable<T> Prioritize<T>(
        IEnumerable<T> targets,
        IReadOnlyDictionary<int, int> priorityBoosts,
        Func<T, int> getId,
        Func<T, double> getDistanceMeters,
        Func<T, double> getRadiusMeters,
        Func<T, decimal> getMonthlyFee)
    {
        return targets
            .OrderByDescending(x => priorityBoosts.TryGetValue(getId(x), out var priority) ? priority : 0)
            .ThenBy(x => getRadiusMeters(x) > 0
                ? getDistanceMeters(x) / getRadiusMeters(x)
                : double.MaxValue)
            .ThenByDescending(getMonthlyFee)
            .ThenBy(getId);
    }
}
