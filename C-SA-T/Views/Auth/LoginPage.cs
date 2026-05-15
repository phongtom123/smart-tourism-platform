using System;
using Microsoft.Maui.Controls;
using MauiApp1.Services;
using MauiApp1.Views.Maps;
using Microsoft.Extensions.DependencyInjection;

namespace MauiApp1.Views.Auth;

public class LoginPage : ContentPage
{
    private readonly ApiService _apiService;
    private readonly IServiceProvider _serviceProvider;
    private readonly LocalizationService _loc;

    private Entry _usernameEntry;
    private Entry _passwordEntry;
    private Label _statusLabel;
    private Button _loginButton;
    private Button _registerButton;
    private Label _titleLabel;
    private bool _isLoggingIn;

    public LoginPage(ApiService apiService, IServiceProvider serviceProvider, LocalizationService localizationService)
    {
        _apiService = apiService;
        _serviceProvider = serviceProvider;
        _loc = localizationService;

        _titleLabel = new Label
        {
            FontSize = 24,
            HorizontalOptions = LayoutOptions.Center
        };

        _usernameEntry = new Entry();

        _passwordEntry = new Entry
        {
            IsPassword = true
        };

        _loginButton = new Button
        {
            BackgroundColor = Colors.Blue,
            TextColor = Colors.White
        };
        _loginButton.Clicked += OnLoginClicked;

        _registerButton = new Button();
        _registerButton.Clicked += async (_, __) =>
        {
            await DisplayAlertAsync(_loc.Get("alert_notice"), _loc.Get("register_todo"), _loc.Get("alert_ok"));
        };

        _statusLabel = new Label
        {
            Text = "",
            TextColor = Colors.Red,
            HorizontalOptions = LayoutOptions.Center
        };

        Content = new VerticalStackLayout
        {
            Padding = 30,
            Spacing = 15,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                _titleLabel,
                _usernameEntry,
                _passwordEntry,
                _loginButton,
                _registerButton,
                _statusLabel
            }
        };

        localizationService.LanguageChanged += () => MainThread.BeginInvokeOnMainThread(UpdateLocalizedText);
        UpdateLocalizedText();
    }

    private void UpdateLocalizedText()
    {
        Title = _loc.Get("login_title");
        _titleLabel.Text = _loc.Get("login_title");
        _usernameEntry.Placeholder = _loc.Get("login_username_hint");
        _passwordEntry.Placeholder = _loc.Get("login_password_hint");
        _loginButton.Text = _loc.Get("login_btn");
        _registerButton.Text = _loc.Get("register_btn");
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        if (_isLoggingIn)
            return;

        string username = _usernameEntry.Text?.Trim() ?? "";
        string password = _passwordEntry.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _statusLabel.Text = _loc.Get("login_empty_fields");
            return;
        }

        _isLoggingIn = true;
        _loginButton.IsEnabled = false;
        _statusLabel.Text = _loc.Get("login_loading");

        try
        {
            var result = await _apiService.LoginAsync(username, password);

            if (result.Success)
            {
                _statusLabel.Text = _loc.Get("login_success");
                var poiMapPage = _serviceProvider.GetRequiredService<PoiMapPage>();
                await Navigation.PushAsync(poiMapPage);
            }
            else
            {
                _statusLabel.Text = string.IsNullOrWhiteSpace(result.Message)
                    ? _loc.Get("login_wrong_creds")
                    : result.Message;
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = _loc.Get("login_api_error");
            await DisplayAlertAsync(_loc.Get("alert_error"), ex.Message, _loc.Get("alert_ok"));
        }
        finally
        {
            _isLoggingIn = false;
            _loginButton.IsEnabled = true;
        }
    }
}
