using Microsoft.Maui.Controls.Maps;

namespace MauiApp1.Models;

public class StyledPin : Pin
{
    public double Rating { get; set; } = 4.9;
    public string? ImagePath { get; set; }
}
