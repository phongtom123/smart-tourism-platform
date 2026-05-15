namespace VinhKhanh.Dtos
{
    public class AdminAccountDto
    {
        public int IdTaiKhoan { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string LoaiTaiKhoan { get; set; } = string.Empty;
        public string? TinhTrang { get; set; }
        public string? TinhTrangDangKy { get; set; }
        public DateTime NgayTao { get; set; }
        public string? HoTen { get; set; }
        public int? IdAdmin { get; set; }
        public int? IdChuQuanLy { get; set; }
    }
}
