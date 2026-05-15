namespace VinhKhanh.Dtos
{
    public class AdminSummaryDto
    {
        public int TongGianHang { get; set; }
        public int TongChuQuanLy { get; set; }
        public int TongThietBi { get; set; }
        public int ThietBiDangHoatDong { get; set; }

        // Dashboard - paid metrics
        public int PaidStores { get; set; }
        public int ActiveOwners { get; set; }
        public int PaidStoreFoods { get; set; }
        public int PendingRequests { get; set; }
        public int DevicesWithActiveToken { get; set; }
        public int OnlineDevices { get; set; }
    }
}
