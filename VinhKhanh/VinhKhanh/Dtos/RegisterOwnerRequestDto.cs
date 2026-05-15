namespace VinhKhanh.Dtos
{
    public class RegisterOwnerRequestDto
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MatKhau { get; set; } = string.Empty;
        public string HoTen { get; set; } = string.Empty;
    }
}
