namespace MauiApp1.Controls;

public sealed class AppRefreshView : RefreshView, IDisposable
{
    private readonly Func<Task> _refreshAction;
    private IDispatcherTimer? _autoRefreshTimer;
    private bool _isRunningRefresh;

    public AppRefreshView(View content, Func<Task> refreshAction)
    {
        _refreshAction = refreshAction;
        Content = content;
        RefreshColor = Color.FromArgb("#F97316");
        Refreshing += OnRefreshing;
    }

    public async Task RefreshAsync(bool showSpinner = false)
    {
        if (_isRunningRefresh)
        {
            if (showSpinner)
                IsRefreshing = false;
            return;
        }

        _isRunningRefresh = true;
        if (showSpinner)
            IsRefreshing = true;

        try
        {
            await _refreshAction();
        }
        finally
        {
            IsRefreshing = false;
            _isRunningRefresh = false;
        }
    }

    public void StartAutoRefresh(IDispatcher dispatcher, TimeSpan interval)
    {
        StopAutoRefresh();

        _autoRefreshTimer = dispatcher.CreateTimer();
        _autoRefreshTimer.Interval = interval;
        _autoRefreshTimer.IsRepeating = true;
        _autoRefreshTimer.Tick += OnAutoRefreshTick;
        _autoRefreshTimer.Start();
    }

    public void StopAutoRefresh()
    {
        if (_autoRefreshTimer is null)
            return;

        _autoRefreshTimer.Stop();
        _autoRefreshTimer.Tick -= OnAutoRefreshTick;
        _autoRefreshTimer = null;
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await RefreshAsync(showSpinner: true);
    }

    private async void OnAutoRefreshTick(object? sender, EventArgs e)
    {
        await RefreshAsync(showSpinner: false);
    }

    public void Dispose()
    {
        Refreshing -= OnRefreshing;
        StopAutoRefresh();
    }
}
