namespace VinhKhanh.Dtos
{
    public class ValidateAccessResponseDto
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? MaThietBi { get; set; }
        public DateTime? BatDauLuc { get; set; }
        public DateTime? HetHanLuc { get; set; }
        public string? TrangThai { get; set; }
    }
}
