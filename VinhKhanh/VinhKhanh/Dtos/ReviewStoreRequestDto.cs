namespace VinhKhanh.Dtos
{
    public class ReviewStoreRequestDto
    {
        public string TrangThaiYeuCau { get; set; } = "da_duyet";
        public string? GhiChuXuLy { get; set; }
        public decimal? PhiHangThang { get; set; }
        public double? Lat { get; set; }
        public double? Lon { get; set; }
    }
}
