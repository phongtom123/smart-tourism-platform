namespace VinhKhanh.Dtos
{
    public class ActivateDeviceResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? MaThietBi { get; set; }
        public bool DaKichHoat { get; set; }
        public DateTime? ThoiGianKichHoat { get; set; }
        public string? TrangThai { get; set; }
    }
}
