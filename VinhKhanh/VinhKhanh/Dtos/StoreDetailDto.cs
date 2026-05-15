namespace VinhKhanh.Dtos
{
    public class StoreDetailDto
    {
        public int IdGianHang { get; set; }
        public string Ten { get; set; } = string.Empty;
        public string? DiaChi { get; set; }
        public string? MoTa { get; set; }
        public string? HinhAnh { get; set; }
        public double? Lat { get; set; }
        public double? Lon { get; set; }
        public decimal VongBo { get; set; }
        public string? TinhTrang { get; set; }
        public decimal PhiHangThang { get; set; }
        public DateTime NgayDangKy { get; set; }
        public DateTime? ThoiGianCapNhat { get; set; }
        public int? IdChuQuanLy { get; set; }
        public string? TenChuQuanLy { get; set; }
        public string? EmailChuQuanLy { get; set; }
        public string? UsernameChuQuanLy { get; set; }
    }
}
