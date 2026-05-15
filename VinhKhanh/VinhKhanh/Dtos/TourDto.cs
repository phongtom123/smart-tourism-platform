namespace VinhKhanh.Dtos
{
    public class TourDto
    {
        public int IdTour { get; set; }
        public string Ten { get; set; } = string.Empty;
        public string? MoTa { get; set; }
        public int? IdNgonNgu { get; set; }
        public int? DoDaiPhutDeXuat { get; set; }
        public string? AnhBia { get; set; }
        public string? DanhMuc { get; set; }
        public string? TinhTrang { get; set; }
        public int SoStop { get; set; }
    }

    public class TourDiemDto
    {
        public int IdTourDiem { get; set; }
        public int IdTour { get; set; }
        public int IdGianHang { get; set; }
        public int ThuTu { get; set; }
        public string? AudioIntroUrl { get; set; }
        public int? ThoiGianDeXuatPhut { get; set; }
        public string? GhiChu { get; set; }
        public string? TenGianHang { get; set; }
        public double? Lat { get; set; }
        public double? Lon { get; set; }
        public string? AudioMacDinhUrl { get; set; }
        public string? HinhAnh { get; set; }
        public bool IsAvailable { get; set; }            // gh.tinhTrang == 'dang_hoat_dong'
        public string? GianHangTinhTrang { get; set; }   // raw status de UI hien lable
    }

    public class TourDetailDto
    {
        public TourDto Tour { get; set; } = new();
        public List<TourDiemDto> DanhSachStop { get; set; } = new();
    }

    public class TourTienDoDto
    {
        public int IdTour { get; set; }
        public string MaThietBi { get; set; } = string.Empty;
        public int StepHienTai { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool IsCompleted => CompletedAt.HasValue;
    }

    public class AdvanceTourRequestDto
    {
        public int IdTour { get; set; }
        public int IdGianHangVuaDen { get; set; }
    }

    public class AdvanceTourResponseDto
    {
        public bool Success { get; set; }
        public int? StepKeTiep { get; set; }
        public int? IdGianHangKeTiep { get; set; }
        public bool IsCompleted { get; set; }
        public string? Message { get; set; }
    }

    public class UpsertTourRequestDto
    {
        public string Ten { get; set; } = string.Empty;
        public string? MoTa { get; set; }
        public int? IdNgonNgu { get; set; }
        public int? DoDaiPhutDeXuat { get; set; }
        public string? AnhBia { get; set; }
        public string? DanhMuc { get; set; }
        public string? TinhTrang { get; set; }
        public List<UpsertTourStopDto> DanhSachStop { get; set; } = new();
    }

    public class UpsertTourStopDto
    {
        public int IdGianHang { get; set; }
        public int ThuTu { get; set; }
        public string? AudioIntroUrl { get; set; }
        public int? ThoiGianDeXuatPhut { get; set; }
        public string? GhiChu { get; set; }
    }

    public class TourActionResultDto
    {
        public bool Success { get; set; }
        public int? IdTour { get; set; }
        public string? Message { get; set; }
    }

    public class RoutePointDto
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
    }

    public class TourRouteDto
    {
        public bool Success { get; set; }
        public bool IsFallback { get; set; }
        public string? Provider { get; set; }
        public string? Status { get; set; }
        public string? Message { get; set; }
        public List<RoutePointDto> Points { get; set; } = new();
    }
}
