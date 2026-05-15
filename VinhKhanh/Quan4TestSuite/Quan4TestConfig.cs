namespace Quan4TestSuite;

public static class Quan4TestConfig
{
    public static string BaseUrl { get; } =
        (Environment.GetEnvironmentVariable("QUAN4_BASE_URL") ?? "http://localhost:5114").TrimEnd('/');

    public static bool RequireBackend { get; } =
        string.Equals(Environment.GetEnvironmentVariable("QUAN4_REQUIRE_BACKEND"), "true", StringComparison.OrdinalIgnoreCase);

    public static int BurstRequestCount { get; } =
        int.TryParse(Environment.GetEnvironmentVariable("QUAN4_BURST_COUNT"), out var value)
            ? Math.Clamp(value, 1, 500)
            : 20;
}
