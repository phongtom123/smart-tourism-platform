namespace MauiApp1.Models;

public sealed record PackagePlanOption(
    int BackendPackageId,
    string Name,
    string Description,
    int DurationDays,
    decimal Price,
    bool IsEnabled);
