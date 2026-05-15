namespace MauiApp1.Models
{
    public class MonAn
    {
        public int IdMonAn { get; set; }
        public int IdGianHang { get; set; }
        public string Ten { get; set; } = string.Empty;
        public decimal DonGia { get; set; }
        public string? MoTa { get; set; }
        public string? TinhTrang { get; set; }
        public string? HinhAnh { get; set; }

        public string TenMon
        {
            get => Ten;
            set => Ten = value;
        }

        public string ThongTinMon
        {
            get => MoTa ?? string.Empty;
            set => MoTa = value;
        }

        public string? HinhAnhFullUrl
        {
            get
            {
                if (string.IsNullOrWhiteSpace(HinhAnh))
                    return null;

                return global::MauiApp1.Utils.BackendUrlResolver.BuildUrl(HinhAnh);
            }
        }
    }
}
