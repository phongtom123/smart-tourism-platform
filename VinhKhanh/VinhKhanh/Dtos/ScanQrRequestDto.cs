namespace VinhKhanh.Dtos
{
    public class ScanQrRequestDto
    {
        public string MaThietBi { get; set; } = string.Empty;
        public string QrRaw { get; set; } = string.Empty;
        public int? IdGoi { get; set; }
    }
}
