namespace VinhKhanh.Dtos
{
    public class UpsertStoreRequestDto
    {
        public string Ten { get; set; } = string.Empty;
        public string? DiaChi { get; set; }
        public double? Lat { get; set; }
        public double? Lon { get; set; }
        public decimal? VongBo { get; set; }
        public decimal PhiHangThang { get; set; }
        public string TinhTrang { get; set; } = "dang_hoat_dong";
        public int? IdChuQuanLy { get; set; }
        public string? EmailChuQuanLy { get; set; }
    }
}
