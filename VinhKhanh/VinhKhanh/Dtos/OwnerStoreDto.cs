namespace VinhKhanh.Dtos
{
    public class OwnerStoreDto
    {
        public int IdGianHang { get; set; }
        public string Ten { get; set; } = string.Empty;
        public string? DiaChi { get; set; }
        public double? Lat { get; set; }
        public double? Lon { get; set; }
        public decimal VongBo { get; set; }
        public string? TinhTrang { get; set; }
        public string? HinhAnh { get; set; }
        public int LuotTruyCap { get; set; }
        public decimal PhiHangThang { get; set; }
        public DateTime NgayDangKy { get; set; }
        public DateTime? ThoiGianCapNhat { get; set; }
    }
}
