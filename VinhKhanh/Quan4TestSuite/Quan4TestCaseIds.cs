namespace Quan4TestSuite;

/// <summary>Trait: <c>[Trait("Quan4", Id)]</c>. Example filter: <c>dotnet test --filter "Quan4=Q4-PV-001"</c>.</summary>
public static class Quan4TestCaseIds
{
    public const string Api001_BackendReachable = "Q4-API-001";
    public const string Pv001_PoiListHasGpsPoi = "Q4-PV-001";
    public const string Pv002_VisitMissingDeviceHeaderBadRequest = "Q4-PV-002";
    public const string Pv003_VisitAcceptedAndQueued = "Q4-PV-003";
    public const string Pv004_DuplicateVisitDeduped = "Q4-PV-004";
    public const string Pv005_BurstDifferentDevicesAccepted = "Q4-PV-005";

    public const string Gf001_OverlapMidpointInsideBoth = "Q4-GF-001";
    public const string Gf002_PriorityPriceWins = "Q4-GF-002";
    public const string Gf003_UnheardWinsBeforeHeard = "Q4-GF-003";
    public const string Gf004_NearbyEndpointReturnsSeededOverlapStores = "Q4-GF-004";
    public const string Gf005_HaversineDistanceIsStable = "Q4-GF-005";

    public static IReadOnlyList<string> All { get; } =
    [
        Api001_BackendReachable,
        Pv001_PoiListHasGpsPoi,
        Pv002_VisitMissingDeviceHeaderBadRequest,
        Pv003_VisitAcceptedAndQueued,
        Pv004_DuplicateVisitDeduped,
        Pv005_BurstDifferentDevicesAccepted,
        Gf001_OverlapMidpointInsideBoth,
        Gf002_PriorityPriceWins,
        Gf003_UnheardWinsBeforeHeard,
        Gf004_NearbyEndpointReturnsSeededOverlapStores,
        Gf005_HaversineDistanceIsStable
    ];
}
