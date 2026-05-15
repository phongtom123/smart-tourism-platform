using SQLite;

namespace MauiApp1.Models
{
    [Table("GianHang")]
    public class GianHangLocal
    {
        [PrimaryKey]
        public int IdGianHang { get; set; }

        public string Ten { get; set; } = "";
        public string? MoTa { get; set; }
        public string? DiaChi { get; set; }

        public double Lat { get; set; }
        public double Lon { get; set; }

        public string? ImageUrl { get; set; }
        public string? AudioURL { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}