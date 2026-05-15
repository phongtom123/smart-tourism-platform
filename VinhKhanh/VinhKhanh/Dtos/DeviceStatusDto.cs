namespace VinhKhanh.Dtos
{
    public class DeviceStatusDto
    {
        public bool Found { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? MaThietBi { get; set; }
        public bool DaKichHoat { get; set; }
        public DateTime? ThoiGianKichHoat { get; set; }
        public DateTime? LanCuoiHoatDong { get; set; }
        public string? TrangThai { get; set; }
    }
}
