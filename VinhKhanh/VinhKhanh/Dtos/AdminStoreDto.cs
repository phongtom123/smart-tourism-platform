namespace VinhKhanh.Dtos
{
    public class AdminStoreDto
    {
        public int IdGianHang { get; set; }
        public string Ten { get; set; } = string.Empty;
        public string? DiaChi { get; set; }
        public string? TinhTrang { get; set; }
        public string? HinhAnh { get; set; }
        public int LuotTruyCap { get; set; }
        public int? IdChuQuanLy { get; set; }
        public string? TenChuQuanLy { get; set; }
        public string? EmailChuQuanLy { get; set; }
        public string? UsernameChuQuanLy { get; set; }
    }
}
