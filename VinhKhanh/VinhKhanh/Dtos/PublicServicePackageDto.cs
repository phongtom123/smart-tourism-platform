namespace VinhKhanh.Dtos
{
    public class PublicServicePackageDto
    {
        public int IdGoi { get; set; }
        public string Ten { get; set; } = string.Empty;
        public string? MoTa { get; set; }
        public decimal Gia { get; set; }
        public int ThoiHanNgay { get; set; }
    }
}
