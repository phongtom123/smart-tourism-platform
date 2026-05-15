namespace VinhKhanh.Dtos
{
    public class AdminInvoiceDto
    {
        public int IdHoaDonGianHang { get; set; }
        public int IdGianHang { get; set; }
        public decimal TongTien { get; set; }
        public DateTime? NgayHetHan { get; set; }
        public string? TrangThai { get; set; }
        public string? GhiChu { get; set; }
        public DateTime? NgayTao { get; set; }
        public string? TenGianHang { get; set; }
        public string? DiaChi { get; set; }
        public decimal PhiHangThang { get; set; }
        public string? TinhTrangGianHang { get; set; }
        public int? IdChuQuanLy { get; set; }
        public int? IdTaiKhoanChuQuanLy { get; set; }
        public string? HoTenChuQuanLy { get; set; }
        public string? UsernameChuQuanLy { get; set; }
        public string? EmailChuQuanLy { get; set; }
    }

    public class AdminInvoiceStatusDto
    {
        public int IdHoaDonGianHang { get; set; }
        public string TrangThai { get; set; } = string.Empty;
    }
}
