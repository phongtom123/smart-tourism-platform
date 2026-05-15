namespace VinhKhanh.Dtos
{
    public class CreateStoreRequestDto
    {
        public string Ten { get; set; } = string.Empty;
        public string? DiaChi { get; set; }
        public string? MoTa { get; set; }
        public string NgonNguMoTa { get; set; } = "vi";
        public double? Lat { get; set; }
        public double? Lon { get; set; }
        public decimal PhiHangThang { get; set; }
        public string TinhTrang { get; set; } = "dang_hoat_dong";
    }
}
