using MauiApp1.Models;
using Microsoft.Maui.Controls.Shapes;

namespace MauiApp1.Controls;

public class BottomSheetView : ContentView
{
    readonly Grid _dragHandleArea;
    readonly Grid _sheetRoot;

    double _collapsedY;
    double _midY;
    double _expandedY;

    double _sheetStartY;
    bool _isDragging;

    public SheetState CurrentState { get; private set; } = SheetState.Collapsed;

    public event Action<bool>? DraggingChanged;
    public event Action<SheetState>? StateChanged;

    public BottomSheetView(View bodyContent)
    {
        _dragHandleArea = BuildDragHandle();
        _sheetRoot = BuildSheet(bodyContent);

        Content = _sheetRoot;

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnPanUpdated;
        _dragHandleArea.GestureRecognizers.Add(pan);
    }

    Grid BuildDragHandle()
    {
        var bar = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#D4D4D8"),
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
            HeightRequest = 5,
            WidthRequest = 48,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 10, 0, 10)
        };

        return new Grid
        {
            HeightRequest = 32,
            BackgroundColor = Colors.Transparent,
            Children = { bar }
        };
    }

    Grid BuildSheet(View bodyContent)
    {
        var contentGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            }
        };

        contentGrid.Add(_dragHandleArea);
        contentGrid.Add(bodyContent);
        Grid.SetRow(bodyContent, 1);

        var panel = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#FFF8F1"),
            StrokeShape = new RoundRectangle
            {
                CornerRadius = new CornerRadius(28, 28, 0, 0)
            },
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.12f,
                Radius = 18,
                Offset = new Point(0, -4)
            },
            Content = contentGrid
        };

        return new Grid
        {
            Children = { panel }
        };
    }

    public void InitializeSnapPoints(double collapsedY, double midY, double expandedY)
    {
        _collapsedY = collapsedY;
        _midY = midY;
        _expandedY = expandedY;

        TranslationY = _collapsedY;
        CurrentState = SheetState.Collapsed;
        StateChanged?.Invoke(CurrentState);
    }

    void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _isDragging = true;
                _sheetStartY = TranslationY;
                DraggingChanged?.Invoke(true);
                break;

            case GestureStatus.Running:
                var newY = _sheetStartY + e.TotalY;
                newY = Clamp(newY, _expandedY, _collapsedY);
                TranslationY = newY;
                break;

            case GestureStatus.Canceled:
            case GestureStatus.Completed:
                _ = EndDragAsync();
                break;
        }
    }

    async Task EndDragAsync()
    {
        if (!_isDragging) return;

        _isDragging = false;
        await SnapToNearestAsync();
        DraggingChanged?.Invoke(false);
    }

    public async Task SnapToNearestAsync()
    {
        var y = TranslationY;
        var target = FindNearestSnapPoint(y);

        await this.TranslateToAsync(0, target, 180, Easing.CubicOut);

        CurrentState =
            NearlyEqual(target, _expandedY) ? SheetState.Expanded :
            NearlyEqual(target, _midY) ? SheetState.Mid :
            SheetState.Collapsed;

        StateChanged?.Invoke(CurrentState);
    }

    public async Task SetStateAsync(SheetState state, bool animate = true)
    {
        var target =
            state == SheetState.Expanded ? _expandedY :
            state == SheetState.Mid ? _midY :
            _collapsedY;

        if (animate)
            await this.TranslateToAsync(0, target, 180, Easing.CubicOut);
        else
            TranslationY = target;

        CurrentState = state;
        StateChanged?.Invoke(CurrentState);
    }

    double FindNearestSnapPoint(double y)
    {
        var points = new[] { _collapsedY, _midY, _expandedY };
        return points.OrderBy(p => Math.Abs(p - y)).First();
    }

    static double Clamp(double value, double min, double max)
        => Math.Max(min, Math.Min(max, value));

    static bool NearlyEqual(double a, double b, double epsilon = 0.5)
        => Math.Abs(a - b) < epsilon;
}
