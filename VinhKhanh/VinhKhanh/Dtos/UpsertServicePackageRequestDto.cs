namespace VinhKhanh.Dtos
{
    public class UpsertServicePackageRequestDto
    {
        public string Ten { get; set; } = string.Empty;
        public string? MoTa { get; set; }
        public decimal Gia { get; set; }
        public int ThoiHanNgay { get; set; }
        public string TrangThai { get; set; } = "hoat_dong";
    }
}
