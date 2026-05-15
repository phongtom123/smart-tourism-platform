using System;
using Microsoft.Maui.Controls;

namespace MauiApp1.Main;

public class MainPage : ContentPage
{
    int _count = 0;
    readonly Label _counter;

    public MainPage(string username)
    {
        Title = "Test App";

        var title = new Label
        {
            Text = $"yo bro! Xin chào {username}",
            FontSize = 24,
            HorizontalOptions = LayoutOptions.Center
        };

        var time = new Label
        {
            Text = $"Time: {DateTime.Now:HH:mm:ss}",
            FontSize = 16,
            HorizontalOptions = LayoutOptions.Center
        };

        _counter = new Label
        {
            Text = "Counter: 0",
            FontSize = 20,
            HorizontalOptions = LayoutOptions.Center
        };

        var btn = new Button
        {
            Text = "Bấm để +1111"
        };

        btn.Clicked += (_, __) =>
        {
            _count++;
            _counter.Text = $"Counter: {_count}";
        };

        Content = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 16,
            Children = { title, time, btn, _counter }
        };
    }
}