namespace VinhKhanh.Dtos
{
    public class AdminPoiMapItemDto
    {
        public int IdGianHang { get; set; }
        public string Ten { get; set; } = string.Empty;
        public string? DiaChi { get; set; }
        public decimal? Lat { get; set; }
        public decimal? Lon { get; set; }
        public decimal VongBo { get; set; } = 10m;
        public int LuotTruyCap { get; set; }
        public string? TinhTrang { get; set; }
        public decimal PhiHangThang { get; set; }
        public string? TenChuQuanLy { get; set; }
        public string? UsernameChuQuanLy { get; set; }
        public string? EmailChuQuanLy { get; set; }
        public string? HinhAnh { get; set; }
        public Dictionary<string, int>? DailyVisits { get; set; }
    }
}
