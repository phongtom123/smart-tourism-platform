// Sandbox: test tour-aware geofence priority without MAUI/Android runtime.
//
// Mirrors the current app behavior:
// - Normal mode: priority = distance/radius ASC, monthly fee DESC, id ASC.
// - Active tour: all tour stops get priority boost.
// - Current/next tour stop get strongest boost.
// - Stop tour: boosts are cleared and normal priority returns.
// - Entering a tour geofence calls AdvanceAsync-like state transition.
// - Auto audio: inside geofence schedules/plays audio for the priority winner.

int passed = 0, failed = 0;

void Assert(bool ok, string message)
{
    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {message}");
    if (ok) passed++; else failed++;
}

void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}

var booths = new Dictionary<int, Booth>
{
    [1] = new(1, "Stop 1 - Welcome", 0, 0, 10, 100_000m, "/audio/stop-1.mp3"),
    [2] = new(2, "Stop 2 - Vietnam", 0, 0, 10, 80_000m, "/audio/stop-2.mp3"),
    [3] = new(3, "Stop 3 - Korea", 0, 0, 10, 60_000m, "/audio/stop-3.mp3"),
    [4] = new(4, "Stop 4 - Japan", 0, 0, 10, 50_000m, "/audio/stop-4.mp3"),
    [98] = new(98, "Silent Booth", 0, 0, 10, 900_000m, null),
    [99] = new(99, "Outsider VIP", 0, 0, 20, 2_000_000m, "/audio/outsider.mp3"),
};

var tour = new Tour(7, "Sandbox Food Tour", new List<TourStop>
{
    new(1, 1),
    new(2, 2),
    new(3, 3),
    new(4, 4),
});

Section("1. Normal geofence priority before tour");
{
    var triggers = new[]
    {
        new Trigger(booths[2], DistanceMeters: 1),
        new Trigger(booths[99], DistanceMeters: 0),
    };

    var winner = Priority.Prioritize(triggers, new Dictionary<int, int>()).First();
    Assert(winner.Booth.Id == 99, "No active tour: outsider wins by better ratio and higher fee.");
}

Section("2. Active tour boosts all tour stops above normal booths");
{
    var state = new TourState();
    var boosts = TourPriority.BuildBoosts(tour, state);
    var triggers = new[]
    {
        new Trigger(booths[2], DistanceMeters: 9),
        new Trigger(booths[99], DistanceMeters: 0),
    };

    var winner = Priority.Prioritize(triggers, boosts).First();
    Assert(winner.Booth.Id == 2, "Tour stop wins even when outsider is closer and pays more.");
}

Section("3. Current/next tour stop beats later tour stops");
{
    var state = new TourState { StepHienTai = 2 };
    var boosts = TourPriority.BuildBoosts(tour, state);
    var triggers = new[]
    {
        new Trigger(booths[2], DistanceMeters: 8),
        new Trigger(booths[4], DistanceMeters: 0),
    };

    var winner = Priority.Prioritize(triggers, boosts).First();
    Assert(winner.Booth.Id == 2, "Current/next stop has stronger boost than a later tour stop.");
}

Section("4. Clear boosts after stopping tour");
{
    var triggers = new[]
    {
        new Trigger(booths[2], DistanceMeters: 9),
        new Trigger(booths[99], DistanceMeters: 0),
    };

    var winner = Priority.Prioritize(triggers, new Dictionary<int, int>()).First();
    Assert(winner.Booth.Id == 99, "After stop tour: normal priority returns and outsider wins again.");
}

Section("5. Geofence entry advances tour progress");
{
    var state = new TourState();
    var r1 = TourProgress.Advance(tour, state, idGianHangVuaDen: 1);
    var r2 = TourProgress.Advance(tour, state, idGianHangVuaDen: 2);

    Assert(r1.Success && r1.StepKeTiep == 2, "Enter stop 1 -> next step is 2.");
    Assert(r2.Success && r2.StepKeTiep == 3, "Enter stop 2 -> next step is 3.");
    Assert(!state.Completed, "Tour is still running.");
}

Section("6. Outsider geofence does not advance tour");
{
    var state = new TourState { StepHienTai = 2 };
    var result = TourProgress.Advance(tour, state, idGianHangVuaDen: 99);

    Assert(!result.Success, "Outsider is rejected by Advance.");
    Assert(state.StepHienTai == 2, "Progress is unchanged.");
}

Section("7. Re-entering an old stop never moves progress backward");
{
    var state = new TourState { StepHienTai = 4 };
    var result = TourProgress.Advance(tour, state, idGianHangVuaDen: 2);

    Assert(result.Success, "Old tour stop still belongs to tour.");
    Assert(state.StepHienTai == 4, "StepHienTai remains 4.");
}

Section("8. Last stop completes tour and keeps boosts harmless");
{
    var state = new TourState { StepHienTai = 4 };
    var result = TourProgress.Advance(tour, state, idGianHangVuaDen: 4);

    Assert(result.Success, "Last stop advance succeeds.");
    Assert(result.IsCompleted && state.Completed, "Tour is completed.");
    Assert(result.StepKeTiep == 4 && result.IdGianHangKeTiep is null, "No next booth after final stop.");
}

Section("9. Audio does not play when no geofence is active");
{
    var audio = new MockAudioEngine();
    audio.EvaluateGeofence(Array.Empty<Trigger>(), new Dictionary<int, int>());

    Assert(audio.PlayCount == 0, "No inside geofence -> no audio.");
    Assert(audio.CurrentStoreId is null, "Current audio remains empty.");
}

Section("10. Normal geofence winner plays audio");
{
    var audio = new MockAudioEngine();
    var triggers = new[]
    {
        new Trigger(booths[2], DistanceMeters: 1),
        new Trigger(booths[99], DistanceMeters: 0),
    };

    audio.EvaluateGeofence(triggers, new Dictionary<int, int>());

    Assert(audio.CurrentStoreId == 99, "Normal mode plays outsider winner.");
    Assert(audio.CurrentAudioUrl == "/audio/outsider.mp3", "Outsider audio URL is playing.");
    Assert(audio.PlayCount == 1, "Audio was started once.");
}

Section("11. Active tour geofence plays boosted tour stop audio");
{
    var state = new TourState();
    var boosts = TourPriority.BuildBoosts(tour, state);
    var audio = new MockAudioEngine();
    var triggers = new[]
    {
        new Trigger(booths[2], DistanceMeters: 9),
        new Trigger(booths[99], DistanceMeters: 0),
    };

    audio.EvaluateGeofence(triggers, boosts);

    Assert(audio.CurrentStoreId == 2, "Tour boost makes stop 2 audio win.");
    Assert(audio.CurrentAudioUrl == "/audio/stop-2.mp3", "Stop 2 audio URL is playing.");
}

Section("12. Repeated same geofence tick does not restart same audio");
{
    var audio = new MockAudioEngine();
    var triggers = new[] { new Trigger(booths[1], DistanceMeters: 0) };

    audio.EvaluateGeofence(triggers, new Dictionary<int, int>());
    audio.EvaluateGeofence(triggers, new Dictionary<int, int>());

    Assert(audio.CurrentStoreId == 1, "Stop 1 remains current audio.");
    Assert(audio.PlayCount == 1, "Same current audio is not restarted.");
}

Section("13. Winner without audio is skipped by autoplay");
{
    var audio = new MockAudioEngine();
    var triggers = new[] { new Trigger(booths[98], DistanceMeters: 0) };

    audio.EvaluateGeofence(triggers, new Dictionary<int, int>());

    Assert(audio.CurrentStoreId is null, "Silent booth has no playable audio.");
    Assert(audio.PlayCount == 0, "No audio start for silent booth.");
}

Section("14. After tour stop, normal geofence audio returns");
{
    var audio = new MockAudioEngine();
    var triggers = new[]
    {
        new Trigger(booths[2], DistanceMeters: 9),
        new Trigger(booths[99], DistanceMeters: 0),
    };

    audio.EvaluateGeofence(triggers, new Dictionary<int, int>());

    Assert(audio.CurrentStoreId == 99, "After clearing boosts, outsider audio wins again.");
    Assert(audio.CurrentAudioUrl == "/audio/outsider.mp3", "Normal audio URL is restored.");
}

Section("15. Starting tour while already inside geofence re-confirms entry");
{
    var geofence = new MockGeofenceEventEngine();
    var triggers = new[] { new Trigger(booths[1], DistanceMeters: 0) };

    geofence.Evaluate(triggers);
    var entriesBeforeTour = geofence.EntryCount;

    geofence.ResetInsideState();
    geofence.Evaluate(triggers);

    Assert(entriesBeforeTour == 1, "Normal geofence entry was recorded once before tour.");
    Assert(geofence.EntryCount == 2, "Reset on tour start makes current geofence fire again.");
    Assert(geofence.LastEnteredStoreId == 1, "The currently occupied tour stop is confirmed.");
}

Section("16. Final stop completion waits until leaving audio radius");
{
    var completion = new MockTourCompletionNotifier();

    completion.MarkFinalStopReached(finalStoreId: 4);
    completion.HandleGeofenceExit(exitedStoreId: 99);

    Assert(completion.AlertCount == 0, "Reaching final stop does not show completion immediately.");
    Assert(completion.IsTourActive, "Tour remains active while visitor is still inside final stop radius.");

    completion.HandleGeofenceExit(exitedStoreId: 4);

    Assert(completion.AlertCount == 1, "Completion alert shows after exiting final stop radius.");
    Assert(!completion.IsTourActive, "Tour state is cleared after the final-radius exit alert.");
}

Section("17. Geofence exit fires once after leaving radius");
{
    var geofence = new MockGeofenceEventEngine();

    geofence.Evaluate(new[] { new Trigger(booths[1], DistanceMeters: 0) });
    geofence.Evaluate(Array.Empty<Trigger>());
    geofence.Evaluate(Array.Empty<Trigger>());

    Assert(geofence.EntryCount == 1, "Initial inside tick records one entry.");
    Assert(geofence.ExitCount == 1, "Leaving the radius records exactly one exit.");
    Assert(geofence.LastExitedStoreId == 1, "Exit belongs to the booth that was previously inside.");
}

Section("18. Moving between booths exits old and enters new");
{
    var geofence = new MockGeofenceEventEngine();

    geofence.Evaluate(new[] { new Trigger(booths[1], DistanceMeters: 0) });
    geofence.Evaluate(new[] { new Trigger(booths[2], DistanceMeters: 0) });

    Assert(geofence.EntryCount == 2, "New booth entry is recorded.");
    Assert(geofence.ExitCount == 1, "Previous booth exit is recorded.");
    Assert(geofence.LastEnteredStoreId == 2 && geofence.LastExitedStoreId == 1, "Move is tracked as exit 1 then enter 2.");
}

Section("19. Overlapping booths do not re-fire while still inside");
{
    var geofence = new MockGeofenceEventEngine();
    var overlap = new[]
    {
        new Trigger(booths[1], DistanceMeters: 0),
        new Trigger(booths[2], DistanceMeters: 0),
    };

    geofence.Evaluate(overlap);
    geofence.Evaluate(overlap);

    Assert(geofence.EntryCount == 2, "Two booths enter once each.");
    Assert(geofence.ExitCount == 0, "No exit fires while still inside both booths.");
}

Section("20. Final completion alert is idempotent");
{
    var completion = new MockTourCompletionNotifier();

    completion.MarkFinalStopReached(finalStoreId: 4);
    completion.HandleGeofenceExit(exitedStoreId: 4);
    completion.HandleGeofenceExit(exitedStoreId: 4);

    Assert(completion.AlertCount == 1, "Duplicate final exit does not show duplicate alert.");
    Assert(!completion.IsTourActive, "Tour remains cleared after duplicate final exit.");
}

Section("21. Exit before final stop reached never completes tour");
{
    var completion = new MockTourCompletionNotifier();

    completion.HandleGeofenceExit(exitedStoreId: 4);

    Assert(completion.AlertCount == 0, "No pending final stop means no completion alert.");
    Assert(completion.IsTourActive, "Tour stays active.");
}

Section("22. Wrong exit is ignored before correct final exit");
{
    var completion = new MockTourCompletionNotifier();

    completion.MarkFinalStopReached(finalStoreId: 4);
    completion.HandleGeofenceExit(exitedStoreId: 3);
    completion.HandleGeofenceExit(exitedStoreId: 4);

    Assert(completion.AlertCount == 1, "Only the final booth exit completes the tour.");
    Assert(!completion.IsTourActive, "Tour clears after the correct final exit.");
}

Section("23. Normal priority tie uses higher monthly fee");
{
    var cheap = new Booth(201, "Cheap Tie", 0, 0, 10, 10_000m, "/audio/cheap.mp3");
    var expensive = new Booth(202, "Expensive Tie", 0, 0, 10, 20_000m, "/audio/expensive.mp3");
    var triggers = new[]
    {
        new Trigger(cheap, DistanceMeters: 5),
        new Trigger(expensive, DistanceMeters: 5),
    };

    var winner = Priority.Prioritize(triggers, new Dictionary<int, int>()).First();
    Assert(winner.Booth.Id == 202, "When distance ratio ties, higher fee wins.");
}

Section("24. Normal priority final tie uses lower id");
{
    var later = new Booth(302, "Later Id", 0, 0, 10, 10_000m, "/audio/later.mp3");
    var earlier = new Booth(301, "Earlier Id", 0, 0, 10, 10_000m, "/audio/earlier.mp3");
    var triggers = new[]
    {
        new Trigger(later, DistanceMeters: 5),
        new Trigger(earlier, DistanceMeters: 5),
    };

    var winner = Priority.Prioritize(triggers, new Dictionary<int, int>()).First();
    Assert(winner.Booth.Id == 301, "When ratio and fee tie, lower booth id wins.");
}

Section("25. Zero-radius normal booth loses distance-ratio priority");
{
    var zeroRadius = new Booth(401, "Zero Radius", 0, 0, 0, 999_000m, "/audio/zero.mp3");
    var normal = new Booth(402, "Normal Radius", 0, 0, 10, 1_000m, "/audio/normal.mp3");
    var triggers = new[]
    {
        new Trigger(zeroRadius, DistanceMeters: 0),
        new Trigger(normal, DistanceMeters: 9),
    };

    var winner = Priority.Prioritize(triggers, new Dictionary<int, int>()).First();
    Assert(winner.Booth.Id == 402, "Zero radius is treated as max ratio in normal priority.");
}

Section("26. Boosted zero-radius booth still wins while tour is active");
{
    var zeroRadius = new Booth(401, "Zero Radius", 0, 0, 0, 1_000m, "/audio/zero.mp3");
    var normal = new Booth(402, "Normal Radius", 0, 0, 10, 999_000m, "/audio/normal.mp3");
    var boosts = new Dictionary<int, int> { [401] = 4000 };
    var triggers = new[]
    {
        new Trigger(zeroRadius, DistanceMeters: 0),
        new Trigger(normal, DistanceMeters: 0),
    };

    var winner = Priority.Prioritize(triggers, boosts).First();
    Assert(winner.Booth.Id == 401, "Tour boost is applied before distance-ratio fallback.");
}

Section("27. Audio switches when tour priority winner changes");
{
    var audio = new MockAudioEngine();

    audio.EvaluateGeofence(new[] { new Trigger(booths[1], DistanceMeters: 0) }, new Dictionary<int, int>());
    audio.EvaluateGeofence(
        new[]
        {
            new Trigger(booths[1], DistanceMeters: 0),
            new Trigger(booths[2], DistanceMeters: 9),
        },
        new Dictionary<int, int> { [2] = 4000 });

    Assert(audio.CurrentStoreId == 2, "Boosted stop 2 replaces previous audio.");
    Assert(audio.PlayCount == 2, "Switching to a different playable booth starts audio once more.");
}

Section("28. Silent boosted winner does not kill current audio");
{
    var audio = new MockAudioEngine();

    audio.EvaluateGeofence(new[] { new Trigger(booths[1], DistanceMeters: 0) }, new Dictionary<int, int>());
    audio.EvaluateGeofence(
        new[]
        {
            new Trigger(booths[1], DistanceMeters: 0),
            new Trigger(booths[98], DistanceMeters: 0),
        },
        new Dictionary<int, int> { [98] = 4000 });

    Assert(audio.CurrentStoreId == 1, "Silent boosted booth is ignored by autoplay.");
    Assert(audio.PlayCount == 1, "Ignoring silent booth does not restart audio.");
}

Section("29. Old stop re-entry after progress does not change next target");
{
    var state = new TourState { StepHienTai = 3 };
    var result = TourProgress.Advance(tour, state, idGianHangVuaDen: 1);

    Assert(result.Success, "Old stop is still recognized as a tour stop.");
    Assert(state.StepHienTai == 3, "Progress does not move backward.");
    Assert(result.IdGianHangKeTiep == 3, "Next booth remains the current forward target.");
}

Section("30. Empty tour is safely ignored");
{
    var emptyTour = new Tour(8, "Empty Tour", new List<TourStop>());
    var state = new TourState();

    var boosts = TourPriority.BuildBoosts(emptyTour, state);
    var result = TourProgress.Advance(emptyTour, state, idGianHangVuaDen: 1);

    Assert(boosts.Count == 0, "Empty tour creates no geofence boosts.");
    Assert(!result.Success, "Advance rejects booths when tour has no stops.");
}

Console.WriteLine();
Console.WriteLine($"=== Total: {passed} passed, {failed} failed ===");
return failed == 0 ? 0 : 1;

record Booth(int Id, string Name, double Lat, double Lon, double RadiusMeters, decimal MonthlyFee, string? AudioUrl);
record Trigger(Booth Booth, double DistanceMeters);
record TourStop(int ThuTu, int IdGianHang);
record Tour(int IdTour, string Ten, List<TourStop> Stops);
record AdvanceResult(bool Success, int? StepKeTiep, int? IdGianHangKeTiep, bool IsCompleted, string Message);

sealed class TourState
{
    public int StepHienTai { get; set; } = 1;
    public bool Completed { get; set; }
}

static class Priority
{
    public static IOrderedEnumerable<Trigger> Prioritize(
        IEnumerable<Trigger> triggers,
        IReadOnlyDictionary<int, int> priorityBoosts)
    {
        return triggers
            .OrderByDescending(t => priorityBoosts.TryGetValue(t.Booth.Id, out var boost) ? boost : 0)
            .ThenBy(t => t.Booth.RadiusMeters > 0
                ? t.DistanceMeters / t.Booth.RadiusMeters
                : double.MaxValue)
            .ThenByDescending(t => t.Booth.MonthlyFee)
            .ThenBy(t => t.Booth.Id);
    }
}

static class TourPriority
{
    public static Dictionary<int, int> BuildBoosts(Tour tour, TourState state)
    {
        var boosts = tour.Stops.ToDictionary(s => s.IdGianHang, _ => 1000);
        var current = ResolveCurrentStop(tour, state);
        var next = ResolveNextStop(tour, current);

        if (current is not null)
            boosts[current.IdGianHang] = state.StepHienTai <= 1 ? 4000 : 3000;

        if (next is not null)
            boosts[next.IdGianHang] = 4000;

        return boosts;
    }

    private static TourStop? ResolveCurrentStop(Tour tour, TourState state)
    {
        var stops = tour.Stops.OrderBy(s => s.ThuTu).ToList();
        if (stops.Count == 0)
            return null;

        if (state.StepHienTai <= 1)
            return stops[0];

        var nextAvailable = stops.FirstOrDefault(s => s.ThuTu >= state.StepHienTai);
        if (nextAvailable is null)
            return stops[^1];

        return stops.LastOrDefault(s => s.ThuTu < nextAvailable.ThuTu) ?? nextAvailable;
    }

    private static TourStop? ResolveNextStop(Tour tour, TourStop? current)
    {
        if (current is null)
            return null;

        return tour.Stops
            .OrderBy(s => s.ThuTu)
            .FirstOrDefault(s => s.ThuTu > current.ThuTu);
    }
}

static class TourProgress
{
    public static AdvanceResult Advance(Tour tour, TourState state, int idGianHangVuaDen)
    {
        var stop = tour.Stops.FirstOrDefault(s => s.IdGianHang == idGianHangVuaDen);
        if (stop is null)
            return new(false, null, null, false, "Booth is not in tour.");

        var totalStops = tour.Stops.Count;
        var newStep = stop.ThuTu >= totalStops ? totalStops : stop.ThuTu + 1;
        state.StepHienTai = Math.Max(state.StepHienTai, newStep);

        var completed = stop.ThuTu >= totalStops;
        if (completed)
            state.Completed = true;

        int? nextBoothId = null;
        if (!completed)
            nextBoothId = tour.Stops.FirstOrDefault(s => s.ThuTu == state.StepHienTai)?.IdGianHang;

        return new(true, state.StepHienTai, nextBoothId, completed, completed ? "Completed." : "Advanced.");
    }
}

sealed class MockAudioEngine
{
    public int? CurrentStoreId { get; private set; }
    public string? CurrentAudioUrl { get; private set; }
    public int PlayCount { get; private set; }

    public void EvaluateGeofence(
        IReadOnlyCollection<Trigger> currentlyInside,
        IReadOnlyDictionary<int, int> priorityBoosts)
    {
        if (currentlyInside.Count == 0)
            return;

        var winner = Priority.Prioritize(currentlyInside, priorityBoosts).First();
        ScheduleAutoPlay(winner.Booth);
    }

    private void ScheduleAutoPlay(Booth booth)
    {
        if (string.IsNullOrWhiteSpace(booth.AudioUrl))
            return;

        if (CurrentStoreId == booth.Id &&
            string.Equals(CurrentAudioUrl, booth.AudioUrl, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CurrentStoreId = booth.Id;
        CurrentAudioUrl = booth.AudioUrl;
        PlayCount++;
    }
}

sealed class MockGeofenceEventEngine
{
    private readonly HashSet<int> _insideTargetIds = new();

    public int EntryCount { get; private set; }
    public int ExitCount { get; private set; }
    public int? LastEnteredStoreId { get; private set; }
    public int? LastExitedStoreId { get; private set; }

    public void ResetInsideState()
    {
        _insideTargetIds.Clear();
    }

    public void Evaluate(IEnumerable<Trigger> currentlyInside)
    {
        var current = currentlyInside.ToList();
        var currentIds = current.Select(t => t.Booth.Id).ToHashSet();
        var exitedIds = _insideTargetIds
            .Where(id => !currentIds.Contains(id))
            .OrderBy(id => id)
            .ToList();

        foreach (var exitedId in exitedIds)
        {
            _insideTargetIds.Remove(exitedId);
            ExitCount++;
            LastExitedStoreId = exitedId;
        }

        foreach (var trigger in current)
        {
            if (!_insideTargetIds.Add(trigger.Booth.Id))
                continue;

            EntryCount++;
            LastEnteredStoreId = trigger.Booth.Id;
        }
    }
}

sealed class MockTourCompletionNotifier
{
    private int? _pendingFinalExitStoreId;

    public int AlertCount { get; private set; }
    public bool IsTourActive { get; private set; } = true;

    public void MarkFinalStopReached(int finalStoreId)
    {
        _pendingFinalExitStoreId = finalStoreId;
    }

    public void HandleGeofenceExit(int exitedStoreId)
    {
        if (_pendingFinalExitStoreId != exitedStoreId || !IsTourActive)
            return;

        AlertCount++;
        IsTourActive = false;
        _pendingFinalExitStoreId = null;
    }
}
