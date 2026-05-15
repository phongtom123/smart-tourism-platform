namespace MauiApp1.Models
{
    public class GianHang
    {
        public int IdGianHang { get; set; }
        public string Ten { get; set; } = string.Empty;
        public string? DiaChi { get; set; }
        public string? MoTa { get; set; }
        public string? AudioURL { get; set; }
        public string? HinhAnh { get; set; }
        public string? HinhAnhChinh { get; set; }
        public List<string> HinhAnhPhu { get; set; } = new();
        public double? Lat { get; set; }
        public double? Lon { get; set; }
        public decimal PhiHangThang { get; set; }
        public string? TinhTrang { get; set; }
        public List<MonAn> MonAns { get; set; } = new();

        public string? HinhAnhFullUrl
        {
            get
            {
                var imagePath = !string.IsNullOrWhiteSpace(HinhAnhChinh)
                    ? HinhAnhChinh
                    : HinhAnh;

                if (string.IsNullOrWhiteSpace(imagePath))
                    return null;

                return global::MauiApp1.Utils.BackendUrlResolver.BuildUrl(imagePath);
            }
        }

        public string? AudioFullUrl
        {
            get
            {
                if (string.IsNullOrWhiteSpace(AudioURL))
                    return null;

                return global::MauiApp1.Utils.BackendUrlResolver.BuildUrl(AudioURL);
            }
        }
    }
}
