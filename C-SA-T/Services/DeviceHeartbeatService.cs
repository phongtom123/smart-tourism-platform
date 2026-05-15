namespace MauiApp1.Services;

public sealed class DeviceHeartbeatService : IDisposable
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    private readonly ApiService _apiService;
    private readonly AccessFlowService _accessFlowService;
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public DeviceHeartbeatService(ApiService apiService, AccessFlowService accessFlowService)
    {
        _apiService = apiService;
        _accessFlowService = accessFlowService;
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_cts is not null && !_cts.IsCancellationRequested)
                return;

            _cts = new CancellationTokenSource();
            _loopTask = Task.Run(() => RunLoopAsync(_cts.Token));
        }
    }

    public void Stop()
    {
        CancellationTokenSource? toCancel;
        lock (_gate)
        {
            toCancel = _cts;
            _cts = null;
        }

        try
        {
            toCancel?.Cancel();
            toCancel?.Dispose();
        }
        catch
        {
        }
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(HeartbeatInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    await TickAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Heartbeat] Tick error: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task TickAsync()
    {
        var sinceLastRequest = DateTime.UtcNow - _apiService.LastRequestAtUtc;
        if (sinceLastRequest < HeartbeatInterval)
            return;

        var token = await _accessFlowService.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
            return;

        if (token.StartsWith("TEST-", StringComparison.OrdinalIgnoreCase))
            return;

        await _apiService.SendHeartbeatAsync(token);
    }

    public void Dispose()
    {
        Stop();
    }
}
