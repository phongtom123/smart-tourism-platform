using SQLite;

namespace MauiApp1.Models
{
    [Table("POI")]
    public class PoiLocal
    {
        [PrimaryKey]
        public int IdPoi { get; set; }

        public int IdGianHang { get; set; }

        public double Lat { get; set; }
        public double Lon { get; set; }

        public double RadiusMeters { get; set; }
    }
}