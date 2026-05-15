namespace VinhKhanh.Dtos
{
    public class LoginResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int? IdTaiKhoan { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? LoaiTaiKhoan { get; set; }
        public int? IdAdmin { get; set; }
        public int? IdChuQuanLy { get; set; }
        public string? HoTen { get; set; }
    }
}
