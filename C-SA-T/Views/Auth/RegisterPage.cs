using MauiApp1.Services;
using Microsoft.Maui.Controls;

namespace MauiApp1.Views.Auth;

public class RegisterPage : ContentPage
{
    public RegisterPage()
    {
        Title = "Đăng ký";

        var title = new Label
        {
            Text = "Tạo tài khoản",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center
        };

        var usernameEntry = new Entry
        {
            Placeholder = "Tên người dùng"
        };

        var emailEntry = new Entry
        {
            Placeholder = "Email",
            Keyboard = Keyboard.Email
        };

        var passwordEntry = new Entry
        {
            Placeholder = "Mật khẩu",
            IsPassword = true
        };

        var confirmPasswordEntry = new Entry
        {
            Placeholder = "Nhập lại mật khẩu",
            IsPassword = true
        };

        var registerButton = new Button
        {
            Text = "Đăng ký",
            BackgroundColor = Colors.Blue,
            TextColor = Colors.White
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 16,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    title,
                    usernameEntry,
                    emailEntry,
                    passwordEntry,
                    confirmPasswordEntry,
                    registerButton
                }
            }
        };
    }
}
