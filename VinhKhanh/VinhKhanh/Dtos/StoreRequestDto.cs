namespace VinhKhanh.Dtos
{
    public class StoreRequestDto
    {
        public int IdYeuCau { get; set; }
        public string LoaiYeuCau { get; set; } = "them_gian_hang";
        public int IdChuQuanLy { get; set; }
        public int IdTaiKhoanChuQuanLy { get; set; }
        public string? HoTenChuQuanLy { get; set; }
        public string? UsernameChuQuanLy { get; set; }
        public string? EmailChuQuanLy { get; set; }
        public string TenGianHang { get; set; } = string.Empty;
        public string? DiaChi { get; set; }
        public string? MoTa { get; set; }
        public string NgonNguMoTa { get; set; } = "vi";
        public double? Lat { get; set; }
        public double? Lon { get; set; }
        public decimal PhiHangThang { get; set; }
        public string TinhTrangDeXuat { get; set; } = "dang_hoat_dong";
        public string TrangThaiYeuCau { get; set; } = "cho_duyet";
        public string? GhiChuXuLy { get; set; }
        public int? IdTaiKhoanXuLy { get; set; }
        public string? TenNguoiXuLy { get; set; }
        public int? IdGianHang { get; set; }
        public DateTime NgayTao { get; set; }
        public DateTime? ThoiGianXuLy { get; set; }
    }
}
