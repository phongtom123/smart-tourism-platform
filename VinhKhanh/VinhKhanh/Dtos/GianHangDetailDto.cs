namespace VinhKhanh.Dtos
{
    public class GianHangDetailDto
    {
        public int IdGianHang { get; set; }
        public string Ten { get; set; } = string.Empty;
        public string? DiaChi { get; set; }
        public string? MoTa { get; set; }
        public string? AudioURL { get; set; }
        public string? HinhAnhChinh { get; set; }
        public List<string> HinhAnhPhu { get; set; } = new();
        public double? Lat { get; set; }
        public double? Lon { get; set; }
        public decimal PhiHangThang { get; set; }
        public string? TinhTrang { get; set; }
        public List<MonAnDto> MonAns { get; set; } = new();
    }
}
