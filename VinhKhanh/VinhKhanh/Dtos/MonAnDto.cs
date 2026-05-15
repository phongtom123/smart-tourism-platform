namespace VinhKhanh.Dtos
{
    public class MonAnDto
    {
        public int IdMonAn { get; set; }
        public int IdGianHang { get; set; }
        public string Ten { get; set; } = string.Empty;
        public decimal DonGia { get; set; }
        public string? MoTa { get; set; }
        public string? TinhTrang { get; set; }
        public string? HinhAnh { get; set; }
    }
}