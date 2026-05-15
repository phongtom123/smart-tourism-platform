namespace VinhKhanh.Dtos
{
    public class CreateAdminAccountRequestDto
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MatKhau { get; set; } = string.Empty;
        public string HoTen { get; set; } = string.Empty;
        public string LoaiTaiKhoan { get; set; } = "chu_quan_ly";
        public int? IdLienKet { get; set; }
        public string TinhTrang { get; set; } = "hoat_dong";
    }
}
