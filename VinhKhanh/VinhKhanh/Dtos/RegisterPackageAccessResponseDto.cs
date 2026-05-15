namespace VinhKhanh.Dtos
{
    public class RegisterPackageAccessResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? MaThietBi { get; set; }
        public int? IdGoi { get; set; }
        public string? TenGoi { get; set; }
        public int? SoNgayHieuLuc { get; set; }
        public string? AccessToken { get; set; }
        public DateTime? BatDauLuc { get; set; }
        public DateTime? HetHanLuc { get; set; }
        public string? TrangThai { get; set; }
        public string? QrTokenPayload { get; set; }
        public bool EmailSent { get; set; }
        public string? EmailStatusMessage { get; set; }
        public int? IdHoaDon { get; set; }
    }
}
