namespace VinhKhanh.Dtos
{
    public class GianHangDto
    {
        public int IdGianHang { get; set; }
        public string Ten { get; set; } = string.Empty;
        public string? DiaChi { get; set; }
        public string? MoTa { get; set; }
        public string? AudioURL { get; set; }
        public string? HinhAnh { get; set; }
        public double? Lat { get; set; }
        public double? Lon { get; set; }
        public decimal PhiHangThang { get; set; }
        public string? TinhTrang { get; set; }
    }
}
