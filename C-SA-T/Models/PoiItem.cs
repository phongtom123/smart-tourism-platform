using System.Text.Json.Serialization;

namespace MauiApp1.Models
{
    public class PoiItem
    {
        [JsonPropertyName("id")]
        public int IDChiNhanh { get; set; }

        [JsonPropertyName("ten")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("lat")]
        public double Latitude { get; set; }

        [JsonPropertyName("lon")]
        public double Longitude { get; set; }

        public string Subtitle { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public string[] MenuNames { get; set; } = Array.Empty<string>();
        public string SearchText { get; set; } = string.Empty;
    }
}
