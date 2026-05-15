namespace VinhKhanh.Dtos
{
    public class AdminDeviceDto
    {
        public int IdThietBi { get; set; }
        public string MaThietBi { get; set; } = string.Empty;
        public bool DaKichHoat { get; set; }
        public DateTime? ThoiGianKichHoat { get; set; }
        public DateTime? LanCuoiHoatDong { get; set; }
        public string TrangThai { get; set; } = string.Empty;
        public string LoaiThietBi { get; set; } = "app_client";
        public string? Platform { get; set; }
        public string? Model { get; set; }
        public string? Manufacturer { get; set; }
        public string? AppVersion { get; set; }
        public int? IdTaiKhoan { get; set; }
        public string? TenChuSoHuu { get; set; }
        public string? EmailChuSoHuu { get; set; }
    }

    public class UpdateDeviceStatusRequestDto
    {
        public string TrangThai { get; set; } = string.Empty;
    }
}
