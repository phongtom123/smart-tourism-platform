using Microsoft.Maui.Controls.Shapes;

namespace MauiApp1.Controls;

public sealed class MapActionButton : ContentView
{
    private readonly Border _frame;
    private readonly View _icon;
    private readonly ActivityIndicator _indicator;
    private bool _isButtonEnabled = true;
    private bool _isBusy;

    public MapActionButton(View icon, double widthRequest = 54, double heightRequest = 54)
    {
        _icon = icon;
        _indicator = new ActivityIndicator
        {
            IsVisible = false,
            IsRunning = false,
            Color = Color.FromArgb("#DC2626"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 18,
            HeightRequest = 18
        };

        var content = new Grid
        {
            Children = { _icon, _indicator }
        };

        _frame = new Border
        {
            StrokeThickness = 1,
            Stroke = new SolidColorBrush(Color.FromArgb("#FED7AA")),
            BackgroundColor = Color.FromArgb("#FFF7ED"),
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            WidthRequest = widthRequest,
            HeightRequest = heightRequest,
            Padding = 0,
            Content = content,
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.12f,
                Radius = 18,
                Offset = new Point(0, 7)
            }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, __) =>
        {
            if (!_isButtonEnabled || _isBusy)
                return;

            Clicked?.Invoke(this, EventArgs.Empty);
        };
        _frame.GestureRecognizers.Add(tap);

        Content = _frame;
    }

    public event EventHandler? Clicked;

    public bool IsButtonEnabled
    {
        get => _isButtonEnabled;
        set
        {
            _isButtonEnabled = value;
            UpdateVisualState();
        }
    }

    public void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        _icon.IsVisible = !_isBusy;
        _indicator.IsVisible = _isBusy;
        _indicator.IsRunning = _isBusy;
        _frame.Opacity = _isButtonEnabled ? 1 : 0.58;
    }
}
