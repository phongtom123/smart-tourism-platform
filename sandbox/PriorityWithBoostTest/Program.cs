// Sandbox test cho FULL 4-key priority sort của GeofenceEngineService.PrioritizeGeofenceTargets
// (C-SA-T/Services/GeofenceEngineService.cs:384-395).
//
// Khác với sandbox/PriorityTest gốc — chỉ test 3 key (ratio > fee > id) — bản này cover
// thêm KEY 1 quan trọng nhất: priorityBoost từ store-level (do admin/owner cấu hình tại
// CS_admin/admin/quan-ly-thiet-bi.php hoặc tương đương). Đây là logic production thực sự
// mà gốc bỏ qua.
//
// Thứ tự sort production (cao → thấp ưu tiên):
//   1. priorityBoosts (descending)             — store boosted thắng tuyệt đối
//   2. distance / radius ratio (ascending)     — gần tâm hơn thắng
//   3. monthlyFee (descending)                 — fee cao thắng
//   4. id (ascending)                          — final tie-break

int passed = 0, failed = 0;

void Case(
    string label,
    IEnumerable<Trigger> input,
    IReadOnlyDictionary<int, int> boosts,
    int expectedWinnerId,
    string reason)
{
    var ordered = Priority.Prioritize(input, boosts).ToList();
    var winner = ordered.First();
    var ok = winner.Target.Id == expectedWinnerId;
    var detail = string.Join(", ", input.Select(t =>
        $"{t.Target.Name}(id={t.Target.Id}, r={t.Target.RadiusMeters}m, fee={t.Target.MonthlyFee}, d={t.DistanceMeters}m, " +
        $"boost={(boosts.TryGetValue(t.Target.Id, out var b) ? b : 0)}, " +
        $"ratio={(t.Target.RadiusMeters > 0 ? t.DistanceMeters / t.Target.RadiusMeters : double.NaN):F3})"));

    Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {label}");
    Console.WriteLine($"        targets : {detail}");
    Console.WriteLine($"        winner  : {winner.Target.Name} (id={winner.Target.Id})");
    Console.WriteLine($"        expect  : id={expectedWinnerId} — {reason}");
    Console.WriteLine();
    if (ok) passed++; else failed++;
}

void OrderCase(
    string label,
    IEnumerable<Trigger> input,
    IReadOnlyDictionary<int, int> boosts,
    int[] expectedOrder,
    string reason)
{
    var actual = Priority.Prioritize(input, boosts).Select(t => t.Target.Id).ToArray();
    var ok = actual.SequenceEqual(expectedOrder);
    Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {label}");
    Console.WriteLine($"        actual order : [{string.Join(", ", actual)}]");
    Console.WriteLine($"        expect order : [{string.Join(", ", expectedOrder)}] — {reason}");
    Console.WriteLine();
    if (ok) passed++; else failed++;
}

// =====================================================================================
// PHẦN 1: BOOST KEY (key #1) thắng tất cả các key còn lại
// =====================================================================================

Case("1. Boosted store thắng dù xa hơn",
    new[] {
        new Trigger(new Target(1, "Premium-Far",  RadiusMeters: 30, MonthlyFee: 50_000m), DistanceMeters: 25),
        new Trigger(new Target(2, "Normal-Near",  RadiusMeters: 30, MonthlyFee: 50_000m), DistanceMeters: 1),
    },
    boosts: new Dictionary<int, int> { [1] = 10 },
    expectedWinnerId: 1,
    reason: "Boost=10 thắng dù ratio (0.83) tệ hơn ratio đối thủ (0.03)");

Case("2. Boosted store thắng dù fee thấp hơn",
    new[] {
        new Trigger(new Target(1, "Boosted-Cheap", RadiusMeters: 10, MonthlyFee: 30_000m),  DistanceMeters: 5),
        new Trigger(new Target(2, "Vip-Expensive", RadiusMeters: 10, MonthlyFee: 1_000_000m), DistanceMeters: 5),
    },
    boosts: new Dictionary<int, int> { [1] = 1 },
    expectedWinnerId: 1,
    reason: "Boost=1 vs no-boost — boosted thắng dù fee chênh ~33x");

Case("3. Hai store cùng boost — fall back sang ratio",
    new[] {
        new Trigger(new Target(1, "Boost-Far",  RadiusMeters: 20, MonthlyFee: 100_000m), DistanceMeters: 18),
        new Trigger(new Target(2, "Boost-Near", RadiusMeters: 20, MonthlyFee: 100_000m), DistanceMeters: 2),
    },
    boosts: new Dictionary<int, int> { [1] = 5, [2] = 5 },
    expectedWinnerId: 2,
    reason: "Boost bằng nhau → key tiếp theo (ratio 0.1 < 0.9) → near thắng");

Case("4. Boost cao hơn thắng boost thấp hơn",
    new[] {
        new Trigger(new Target(1, "Boost-Low",  RadiusMeters: 10, MonthlyFee: 100_000m), DistanceMeters: 1),
        new Trigger(new Target(2, "Boost-High", RadiusMeters: 10, MonthlyFee: 100_000m), DistanceMeters: 9),
    },
    boosts: new Dictionary<int, int> { [1] = 1, [2] = 100 },
    expectedWinnerId: 2,
    reason: "Boost 100 > 1 → thắng dù ratio cực tệ (0.9 vs 0.1)");

// =====================================================================================
// PHẦN 2: BOOST 0 / KHÔNG BOOST tương đương — boost negative không xảy ra
// =====================================================================================

Case("5. Boost = 0 không khác gì không boost",
    new[] {
        new Trigger(new Target(1, "Zero-Boost", RadiusMeters: 10, MonthlyFee: 200_000m), DistanceMeters: 5),
        new Trigger(new Target(2, "No-Boost",   RadiusMeters: 10, MonthlyFee: 100_000m), DistanceMeters: 5),
    },
    boosts: new Dictionary<int, int> { [1] = 0 },
    expectedWinnerId: 1,
    reason: "Boost=0 == không-boost → fee 200K thắng 100K");

Case("6. Boost = -1 (data hỏng) bị xếp SAU no-boost (key 0)",
    new[] {
        new Trigger(new Target(1, "Negative-Boost", RadiusMeters: 10, MonthlyFee: 50_000m),  DistanceMeters: 5),
        new Trigger(new Target(2, "No-Boost-VIP",   RadiusMeters: 10, MonthlyFee: 500_000m), DistanceMeters: 5),
    },
    boosts: new Dictionary<int, int> { [1] = -1 },
    expectedWinnerId: 2,
    reason: "OrderByDescending(boost) → 0 > -1, no-boost (id=2) thắng. " +
            "Trong production GeofenceEngineService.SetPriorityBoostsAsync (line 96) đã filter `priority <= 0`, " +
            "nên dict không bao giờ chứa giá trị âm. Test này verify hành vi raw nếu data hỏng.");

// =====================================================================================
// PHẦN 3: TIE BREAKING TOÀN BỘ — đẩy đến tận key cuối (id)
// =====================================================================================

Case("7. Boost bằng + ratio bằng + fee bằng → id nhỏ thắng",
    new[] {
        new Trigger(new Target(50, "Last", RadiusMeters: 10, MonthlyFee: 100_000m), DistanceMeters: 5),
        new Trigger(new Target(10, "Mid",  RadiusMeters: 10, MonthlyFee: 100_000m), DistanceMeters: 5),
        new Trigger(new Target(3,  "First",RadiusMeters: 10, MonthlyFee: 100_000m), DistanceMeters: 5),
    },
    boosts: new Dictionary<int, int>(),
    expectedWinnerId: 3,
    reason: "Tất cả tie tuyệt đối → id=3 nhỏ nhất thắng");

Case("8. Boost bằng + ratio bằng → fee tie-break",
    new[] {
        new Trigger(new Target(1, "Mid-Fee",  RadiusMeters: 10, MonthlyFee: 100_000m), DistanceMeters: 5),
        new Trigger(new Target(2, "Top-Fee",  RadiusMeters: 10, MonthlyFee: 500_000m), DistanceMeters: 5),
        new Trigger(new Target(3, "Low-Fee",  RadiusMeters: 10, MonthlyFee: 50_000m),  DistanceMeters: 5),
    },
    boosts: new Dictionary<int, int> { [1] = 5, [2] = 5, [3] = 5 },
    expectedWinnerId: 2,
    reason: "Boost bằng + ratio bằng → fee 500K thắng");

// =====================================================================================
// PHẦN 4: ORDER CHECK — đảm bảo toàn bộ thứ tự đúng, không chỉ winner
// =====================================================================================

OrderCase("9. Thứ tự đầy đủ — boost > ratio > fee > id",
    new[] {
        new Trigger(new Target(1, "A", RadiusMeters: 10, MonthlyFee: 100_000m), DistanceMeters: 8),
        new Trigger(new Target(2, "B", RadiusMeters: 10, MonthlyFee: 200_000m), DistanceMeters: 2),
        new Trigger(new Target(3, "C", RadiusMeters: 10, MonthlyFee: 500_000m), DistanceMeters: 9),
        new Trigger(new Target(4, "D", RadiusMeters: 10, MonthlyFee: 100_000m), DistanceMeters: 1),
    },
    boosts: new Dictionary<int, int> { [3] = 100 },
    expectedOrder: new[] { 3, 4, 2, 1 },
    reason: "C boosted thắng nhất → D (ratio 0.1) → B (ratio 0.2, fee cao hơn A) → A (ratio 0.8)");

OrderCase("10. Boost phân tầng — cao xếp trước",
    new[] {
        new Trigger(new Target(1, "B5",  RadiusMeters: 10, MonthlyFee: 100_000m), DistanceMeters: 5),
        new Trigger(new Target(2, "B10", RadiusMeters: 10, MonthlyFee: 100_000m), DistanceMeters: 5),
        new Trigger(new Target(3, "B0",  RadiusMeters: 10, MonthlyFee: 100_000m), DistanceMeters: 5),
        new Trigger(new Target(4, "B20", RadiusMeters: 10, MonthlyFee: 100_000m), DistanceMeters: 5),
    },
    boosts: new Dictionary<int, int> { [1] = 5, [2] = 10, [4] = 20 },
    expectedOrder: new[] { 4, 2, 1, 3 },
    reason: "B20 > B10 > B5 > B0(no-boost). Tất cả các key khác bằng nhau.");

// =====================================================================================
// PHẦN 5: EDGE CASES
// =====================================================================================

Case("11. Empty boost dict — fall back về 3-key như sandbox cũ",
    new[] {
        new Trigger(new Target(1, "Far",  RadiusMeters: 10, MonthlyFee: 50_000m),  DistanceMeters: 9),
        new Trigger(new Target(2, "Near", RadiusMeters: 10, MonthlyFee: 50_000m),  DistanceMeters: 1),
    },
    boosts: new Dictionary<int, int>(),
    expectedWinnerId: 2,
    reason: "Không boost nào → ratio quyết định, near thắng");

Case("12. Boost cho store KHÔNG tồn tại trong targets (data inconsistency) — bỏ qua",
    new[] {
        new Trigger(new Target(1, "A", RadiusMeters: 10, MonthlyFee: 100_000m), DistanceMeters: 5),
        new Trigger(new Target(2, "B", RadiusMeters: 10, MonthlyFee: 200_000m), DistanceMeters: 5),
    },
    boosts: new Dictionary<int, int> { [99] = 1000 },  // store id 99 không có trong targets
    expectedWinnerId: 2,
    reason: "Boost cho id 99 (không match) → fee tie-break → B thắng");

Case("13. Một target boost cao + radius=0 (data hỏng)",
    new[] {
        new Trigger(new Target(1, "Boost-Hong",  RadiusMeters: 0,  MonthlyFee: 100_000m), DistanceMeters: 0),
        new Trigger(new Target(2, "OK-No-Boost", RadiusMeters: 10, MonthlyFee: 100_000m), DistanceMeters: 5),
    },
    boosts: new Dictionary<int, int> { [1] = 100 },
    expectedWinnerId: 1,
    reason: "Boost=100 thắng dù ratio = double.MaxValue (radius=0). Boost ưu tiên hơn validity của data.");

Case("14. Single target — no contention",
    new[] {
        new Trigger(new Target(7, "Only", RadiusMeters: 15, MonthlyFee: 100_000m), DistanceMeters: 10),
    },
    boosts: new Dictionary<int, int> { [7] = 5 },
    expectedWinnerId: 7,
    reason: "Một target duy nhất, boost không thay đổi gì");

Case("15. 100 targets — 1 boosted phải đứng đầu",
    Enumerable.Range(1, 100)
        .Select(i => new Trigger(
            new Target(i, $"S{i}", RadiusMeters: 50, MonthlyFee: 100_000m + i),
            DistanceMeters: i % 50))
        .ToArray(),
    boosts: new Dictionary<int, int> { [42] = 1 },
    expectedWinnerId: 42,
    reason: "Boost 1 vs 99 no-boost → boosted lên đầu");

// =====================================================================================
// PHẦN 6: STABILITY — 2 lần Prioritize trên cùng input phải cho cùng order (deterministic)
// =====================================================================================

{
    var input = new[] {
        new Trigger(new Target(1, "A", RadiusMeters: 10, MonthlyFee: 100_000m), DistanceMeters: 5),
        new Trigger(new Target(2, "B", RadiusMeters: 10, MonthlyFee: 100_000m), DistanceMeters: 5),
        new Trigger(new Target(3, "C", RadiusMeters: 10, MonthlyFee: 100_000m), DistanceMeters: 5),
    };
    var boosts = new Dictionary<int, int> { [2] = 10 };
    var run1 = Priority.Prioritize(input, boosts).Select(t => t.Target.Id).ToArray();
    var run2 = Priority.Prioritize(input, boosts).Select(t => t.Target.Id).ToArray();
    var ok = run1.SequenceEqual(run2);
    Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] 16. Determinism: 2 lần sort cho output identical");
    Console.WriteLine($"        run1: [{string.Join(",", run1)}]");
    Console.WriteLine($"        run2: [{string.Join(",", run2)}]");
    Console.WriteLine();
    if (ok) passed++; else failed++;
}

// =====================================================================================
// PHẦN 7: DEMO USE-CASES THỰC TẾ
// =====================================================================================

// 17. Owner boost gian hàng VIP của mình; du khách đứng giữa 3 booth chen lẫn
Case("17. Use-case: VIP owner boost — du khách giữa hội chợ",
    new[] {
        new Trigger(new Target(101, "Booth-A-Normal",  RadiusMeters: 20, MonthlyFee: 100_000m), DistanceMeters: 4),
        new Trigger(new Target(102, "Booth-VIP-Owner", RadiusMeters: 20, MonthlyFee: 200_000m), DistanceMeters: 12),
        new Trigger(new Target(103, "Booth-C-Normal",  RadiusMeters: 20, MonthlyFee: 150_000m), DistanceMeters: 8),
    },
    boosts: new Dictionary<int, int> { [102] = 1 },
    expectedWinnerId: 102,
    reason: "VIP owner boost active → audio VIP phát trước dù xa nhất");

// 18. Hai owner cùng boost — fee cao hơn thắng
Case("18. Use-case: 2 VIP boost cùng level — fee cao thắng",
    new[] {
        new Trigger(new Target(201, "VIP-Cheap", RadiusMeters: 15, MonthlyFee: 100_000m), DistanceMeters: 5),
        new Trigger(new Target(202, "VIP-Pro",   RadiusMeters: 15, MonthlyFee: 800_000m), DistanceMeters: 5),
        new Trigger(new Target(203, "Normal",    RadiusMeters: 15, MonthlyFee: 1_500_000m), DistanceMeters: 5),
    },
    boosts: new Dictionary<int, int> { [201] = 1, [202] = 1 },
    expectedWinnerId: 202,
    reason: "Cả 2 VIP boost → ratio bằng → fee 800K > 100K. Normal fee 1.5M cao nhưng không boost nên thua.");

Console.WriteLine($"=== Total: {passed} passed, {failed} failed ===");
return failed == 0 ? 0 : 1;

record Target(int Id, string Name, double RadiusMeters, decimal MonthlyFee);
record Trigger(Target Target, double DistanceMeters);

static class Priority
{
    // Mirror đúng logic của GeofenceEngineService.PrioritizeGeofenceTargets:
    // C-SA-T/Services/GeofenceEngineService.cs line 384-395
    public static IOrderedEnumerable<Trigger> Prioritize(
        IEnumerable<Trigger> targets,
        IReadOnlyDictionary<int, int> priorityBoosts)
    {
        return targets
            .OrderByDescending(x => priorityBoosts.TryGetValue(x.Target.Id, out var p) ? p : 0)
            .ThenBy(x => x.Target.RadiusMeters > 0
                ? x.DistanceMeters / x.Target.RadiusMeters
                : double.MaxValue)
            .ThenByDescending(x => x.Target.MonthlyFee)
            .ThenBy(x => x.Target.Id);
    }
}
