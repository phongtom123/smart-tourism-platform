namespace MauiApp1.Models
{
    public class TaiKhoan
    {
        public int IDTaiKhoan { get; set; }
        public string Email { get; set; } = string.Empty;
        public string MatKhau { get; set; } = string.Empty;
        public string TenNguoiDung { get; set; } = string.Empty;
        public string VaiTro { get; set; } = string.Empty;
        public string TinhTrangDangKy { get; set; } = string.Empty;
        public DateTime ThoiGianDangKy { get; set; }
    }
}
