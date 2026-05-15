using System.Text.Json.Serialization;

namespace MauiApp1.Models;

public class TourSummary
{
    [JsonPropertyName("idTour")]
    public int IdTour { get; set; }

    [JsonPropertyName("ten")]
    public string Ten { get; set; } = string.Empty;

    [JsonPropertyName("moTa")]
    public string? MoTa { get; set; }

    [JsonPropertyName("idNgonNgu")]
    public int? IdNgonNgu { get; set; }

    [JsonPropertyName("doDaiPhutDeXuat")]
    public int? DoDaiPhutDeXuat { get; set; }

    [JsonPropertyName("anhBia")]
    public string? AnhBia { get; set; }

    [JsonPropertyName("danhMuc")]
    public string? DanhMuc { get; set; }

    [JsonPropertyName("tinhTrang")]
    public string? TinhTrang { get; set; }

    [JsonPropertyName("soStop")]
    public int SoStop { get; set; }
}

public class TourStop
{
    [JsonPropertyName("idTourDiem")]
    public int IdTourDiem { get; set; }

    [JsonPropertyName("idTour")]
    public int IdTour { get; set; }

    [JsonPropertyName("idGianHang")]
    public int IdGianHang { get; set; }

    [JsonPropertyName("thuTu")]
    public int ThuTu { get; set; }

    [JsonPropertyName("audioIntroUrl")]
    public string? AudioIntroUrl { get; set; }

    [JsonPropertyName("thoiGianDeXuatPhut")]
    public int? ThoiGianDeXuatPhut { get; set; }

    [JsonPropertyName("ghiChu")]
    public string? GhiChu { get; set; }

    [JsonPropertyName("tenGianHang")]
    public string? TenGianHang { get; set; }

    [JsonPropertyName("lat")]
    public double? Lat { get; set; }

    [JsonPropertyName("lon")]
    public double? Lon { get; set; }

    [JsonPropertyName("audioMacDinhUrl")]
    public string? AudioMacDinhUrl { get; set; }

    [JsonPropertyName("isAvailable")]
    public bool IsAvailable { get; set; }

    [JsonPropertyName("gianHangTinhTrang")]
    public string? GianHangTinhTrang { get; set; }
}

public class TourDetail
{
    [JsonPropertyName("tour")]
    public TourSummary Tour { get; set; } = new();

    [JsonPropertyName("danhSachStop")]
    public List<TourStop> DanhSachStop { get; set; } = new();
}

public class TourProgress
{
    [JsonPropertyName("idTour")]
    public int IdTour { get; set; }

    [JsonPropertyName("maThietBi")]
    public string MaThietBi { get; set; } = string.Empty;

    [JsonPropertyName("stepHienTai")]
    public int StepHienTai { get; set; }

    [JsonPropertyName("startedAt")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("isCompleted")]
    public bool IsCompleted { get; set; }
}

public class AdvanceTourResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("stepKeTiep")]
    public int? StepKeTiep { get; set; }

    [JsonPropertyName("idGianHangKeTiep")]
    public int? IdGianHangKeTiep { get; set; }

    [JsonPropertyName("isCompleted")]
    public bool IsCompleted { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class RoutePoint
{
    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lon")]
    public double Lon { get; set; }
}

public class TourRoute
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("isFallback")]
    public bool IsFallback { get; set; }

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("points")]
    public List<RoutePoint> Points { get; set; } = new();
}
