using System.Net.Http.Json;
using MauiApp1.Models;
using MauiApp1.Utils;

namespace MauiApp1.Services
{
    public class ApiService
    {
        private const string DeviceHeaderName = "X-Device-Id";

        private readonly HttpClient _httpClient;
        private readonly ClientDeviceIdentityService _clientDeviceIdentityService;
        private long _lastRequestAtTicks;

        public ApiService(HttpClient httpClient, ClientDeviceIdentityService clientDeviceIdentityService)
        {
            _httpClient = httpClient;
            _clientDeviceIdentityService = clientDeviceIdentityService;

            var deviceId = _clientDeviceIdentityService.GetOrCreateClientDeviceId();
            if (!_httpClient.DefaultRequestHeaders.Contains(DeviceHeaderName))
                _httpClient.DefaultRequestHeaders.Add(DeviceHeaderName, deviceId);
        }

        public DateTime LastRequestAtUtc => new DateTime(Interlocked.Read(ref _lastRequestAtTicks), DateTimeKind.Utc);

        internal void MarkRequestSent() => Interlocked.Exchange(ref _lastRequestAtTicks, DateTime.UtcNow.Ticks);

        public async Task<AppDataResponse> GetAppDataAsync(string lang = "vi")
        {
            var url = BuildApiUrl($"api/gianhang/appdata?lang={lang}");
            MarkRequestSent();
            var result = await _httpClient.GetFromJsonAsync<AppDataResponse>(url);
            return result ?? new AppDataResponse();
        }

        public async Task<LoginResult> LoginAsync(string username, string password)
        {
            MarkRequestSent();
            var response = await _httpClient.PostAsJsonAsync(BuildApiUrl("api/auth/login"), new
            {
                Username = username,
                MatKhau = password
            });

            LoginResult? result = null;

            try
            {
                result = await response.Content.ReadFromJsonAsync<LoginResult>();
            }
            catch
            {
            }

            if (result != null)
                return result;

            return new LoginResult
            {
                Success = response.IsSuccessStatusCode,
                Message = response.ReasonPhrase ?? "Đăng nhập thất bại."
            };
        }

        public async Task<List<GianHang>> GetAllGianHangsAsync(string lang = "vi")
        {
            var data = await GetAppDataAsync(lang);
            return data.GianHangs;
        }

        public async Task<List<ServicePackageOption>> GetServicePackagesAsync()
        {
            try
            {
                MarkRequestSent();
                var result = await _httpClient.GetFromJsonAsync<List<ServicePackageOption>>(BuildApiUrl("api/access/packages"));
                return result ?? new List<ServicePackageOption>();
            }
            catch (Exception ex) when (IsNetworkException(ex))
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] Khong tai duoc danh sach goi dich vu: {BuildNetworkErrorMessage(ex)}");
                return new List<ServicePackageOption>();
            }
        }

        public async Task<GianHang?> GetGianHangDetailAsync(int idGianHang, string lang = "vi")
        {
            var data = await GetAppDataAsync(lang);
            return data.GianHangs.FirstOrDefault(x => x.IdGianHang == idGianHang);
        }

        public async Task<List<GianHang>> GetNearbyGianHangsAsync(
            double lat,
            double lon,
            string lang = "vi",
            double radiusMeters = 100)
        {
            var data = await GetAppDataAsync(lang);

            return data.GianHangs
                .Where(x => x.Lat.HasValue && x.Lon.HasValue)
                .Select(x => new
                {
                    GianHang = x,
                    Distance = CalculateDistanceMeters(lat, lon, x.Lat!.Value, x.Lon!.Value)
                })
                .Where(x => x.Distance <= radiusMeters)
                .OrderBy(x => x.Distance)
                .Select(x => x.GianHang)
                .ToList();
        }

        public async Task<QrScanResult> ScanQrAsync(string qrRaw, int? idGoi = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(qrRaw))
                {
                    return new QrScanResult
                    {
                        Success = false,
                        Message = "QR rong, vui long thu lai."
                    };
                }

                var accessToken = ExtractAccessToken(qrRaw);
                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    var validation = await ActivateTokenAsync(accessToken, qrRaw);
                    return new QrScanResult
                    {
                        Success = validation.Success,
                        Message = validation.Success
                            ? "Kich hoat token tu QR thanh cong."
                            : validation.Message,
                        AccessToken = accessToken,
                        MaThietBi = validation.MaThietBi,
                        HetHanLuc = validation.HetHanLuc,
                        BatDauLuc = validation.BatDauLuc,
                        TrangThai = validation.TrangThai,
                        IdGoi = validation.IdGoi,
                        TenGoi = validation.TenGoi,
                        SoNgayHieuLuc = validation.SoNgayHieuLuc,
                        EmailStatusMessage = validation.Message
                    };
                }

                return new QrScanResult
                {
                    Success = false,
                    Message = "QR khong chua token dang nhap hop le."
                };
            }
            catch (Exception ex) when (IsNetworkException(ex))
            {
                return new QrScanResult
                {
                    Success = false,
                    Message = BuildNetworkErrorMessage(ex)
                };
            }
        }

        public async Task<PackageAccessRegistrationResult> RegisterPackageAccessAsync(string email, int idGoi, bool bypassPayment, bool sendEmail = true)
        {
            try
            {
                MarkRequestSent();
                var response = await _httpClient.PostAsJsonAsync(BuildApiUrl("api/access/package/register"), new
                {
                    Email = email,
                    IdGoi = idGoi,
                    BypassPayment = bypassPayment,
                    SendEmail = sendEmail,
                    ClientDeviceId = _clientDeviceIdentityService.GetOrCreateClientDeviceId()
                });

                PackageAccessRegistrationResult? result = null;

                try
                {
                    result = await response.Content.ReadFromJsonAsync<PackageAccessRegistrationResult>();
                }
                catch
                {
                }

                return result ?? new PackageAccessRegistrationResult
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.ReasonPhrase ?? "Khong dang ky duoc goi dich vu."
                };
            }
            catch (Exception ex) when (IsNetworkException(ex))
            {
                return new PackageAccessRegistrationResult
                {
                    Success = false,
                    Message = BuildNetworkErrorMessage(ex)
                };
            }
        }

        public async Task<PackagePaymentResult> CreatePackagePaymentAsync(string email, int idGoi, bool sendEmail = false)
        {
            try
            {
                MarkRequestSent();
                var response = await _httpClient.PostAsJsonAsync(BuildApiUrl("api/access/package/payment"), new
                {
                    Email = email,
                    IdGoi = idGoi,
                    SendEmail = sendEmail,
                    ClientDeviceId = _clientDeviceIdentityService.GetOrCreateClientDeviceId()
                });

                PackagePaymentResult? result = null;

                try
                {
                    result = await response.Content.ReadFromJsonAsync<PackagePaymentResult>();
                }
                catch
                {
                }

                return result ?? new PackagePaymentResult
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.ReasonPhrase ?? "Khong tao duoc ma QR thanh toan."
                };
            }
            catch (Exception ex) when (IsNetworkException(ex))
            {
                return new PackagePaymentResult
                {
                    Success = false,
                    Message = BuildNetworkErrorMessage(ex)
                };
            }
        }

        public async Task<PackagePaymentResult> GetPackagePaymentStatusAsync(string paymentReference)
        {
            if (string.IsNullOrWhiteSpace(paymentReference))
            {
                return new PackagePaymentResult
                {
                    Success = false,
                    Message = "Thieu ma thanh toan."
                };
            }

            try
            {
                MarkRequestSent();
                var url = BuildApiUrl($"api/access/package/payment/{Uri.EscapeDataString(paymentReference)}");
                var response = await _httpClient.GetAsync(url);

                PackagePaymentResult? result = null;

                try
                {
                    result = await response.Content.ReadFromJsonAsync<PackagePaymentResult>();
                }
                catch
                {
                }

                return result ?? new PackagePaymentResult
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.ReasonPhrase ?? "Khong kiem tra duoc thanh toan."
                };
            }
            catch (Exception ex) when (IsNetworkException(ex))
            {
                return new PackagePaymentResult
                {
                    Success = false,
                    Message = BuildNetworkErrorMessage(ex)
                };
            }
        }

        public async Task<ValidateAccessResult> ValidateAccessAsync(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return new ValidateAccessResult
                {
                    IsValid = false,
                    Message = "Thieu access token."
                };
            }

            try
            {
                var clientDeviceId = _clientDeviceIdentityService.GetOrCreateClientDeviceId();
                MarkRequestSent();
                var response = await _httpClient.PostAsJsonAsync(BuildApiUrl("api/access/validate"), new
                {
                    AccessToken = accessToken,
                    ClientDeviceId = clientDeviceId
                });

                ValidateAccessResult? result = null;

                try
                {
                    result = await response.Content.ReadFromJsonAsync<ValidateAccessResult>();
                }
                catch
                {
                }

                if (result != null)
                    return result;

                return new ValidateAccessResult
                {
                    IsValid = false,
                    IsNetworkError = !response.IsSuccessStatusCode,
                    Message = response.ReasonPhrase ?? "Khong validate duoc access token."
                };
            }
            catch (Exception ex) when (IsNetworkException(ex))
            {
                return new ValidateAccessResult
                {
                    IsValid = false,
                    IsNetworkError = true,
                    Message = BuildNetworkErrorMessage(ex)
                };
            }
        }

        public async Task<HeartbeatResult> SendHeartbeatAsync(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                return new HeartbeatResult { Success = false, Message = "Thieu access token." };

            try
            {
                var clientDeviceId = _clientDeviceIdentityService.GetOrCreateClientDeviceId();
                var metadata = _clientDeviceIdentityService.GetDeviceMetadata();
                MarkRequestSent();
                var response = await _httpClient.PostAsJsonAsync(BuildApiUrl("api/device/heartbeat"), new
                {
                    MaThietBi = clientDeviceId,
                    AccessToken = accessToken,
                    metadata.Platform,
                    metadata.Model,
                    metadata.Manufacturer,
                    metadata.AppVersion
                });

                HeartbeatResult? result = null;
                try
                {
                    result = await response.Content.ReadFromJsonAsync<HeartbeatResult>();
                }
                catch
                {
                }

                return result ?? new HeartbeatResult
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.ReasonPhrase ?? string.Empty,
                    MustRevalidate = response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                };
            }
            catch (Exception ex) when (IsNetworkException(ex))
            {
                return new HeartbeatResult
                {
                    Success = false,
                    Message = BuildNetworkErrorMessage(ex)
                };
            }
        }

        public async Task<bool> RecordPoiVisitAsync(int idGianHang)
        {
            try
            {
                MarkRequestSent();
                var url = BuildApiUrl($"api/poi/{idGianHang}/visit");
                var response = await _httpClient.PostAsync(url, null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error recording POI visit: {ex}");
                return false;
            }
        }

        public async Task<List<TourSummary>> GetActiveToursAsync(int? langId = null)
        {
            try
            {
                MarkRequestSent();
                var path = langId.HasValue
                    ? $"api/tour?lang={langId.Value}"
                    : "api/tour";
                var result = await _httpClient.GetFromJsonAsync<List<TourSummary>>(BuildApiUrl(path));
                return result ?? new List<TourSummary>();
            }
            catch (Exception ex) when (IsNetworkException(ex))
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] Tour list error: {BuildNetworkErrorMessage(ex)}");
                return new List<TourSummary>();
            }
        }

        public async Task<TourDetail?> GetTourDetailAsync(int idTour)
        {
            try
            {
                MarkRequestSent();
                return await _httpClient.GetFromJsonAsync<TourDetail>(BuildApiUrl($"api/tour/{idTour}"));
            }
            catch (Exception ex) when (IsNetworkException(ex))
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] Tour detail error: {BuildNetworkErrorMessage(ex)}");
                return null;
            }
        }

        public async Task<TourProgress?> GetTourProgressAsync(int idTour)
        {
            try
            {
                MarkRequestSent();
                return await _httpClient.GetFromJsonAsync<TourProgress>(BuildApiUrl($"api/tour/{idTour}/progress"));
            }
            catch (Exception ex) when (IsNetworkException(ex))
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] Tour progress error: {BuildNetworkErrorMessage(ex)}");
                return null;
            }
        }

        public async Task<AdvanceTourResult?> AdvanceTourAsync(int idTour, int idGianHangVuaDen)
        {
            try
            {
                MarkRequestSent();
                var response = await _httpClient.PostAsJsonAsync(BuildApiUrl($"api/tour/{idTour}/advance"), new
                {
                    IdTour = idTour,
                    IdGianHangVuaDen = idGianHangVuaDen
                });

                return await response.Content.ReadFromJsonAsync<AdvanceTourResult>();
            }
            catch (Exception ex) when (IsNetworkException(ex))
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] Tour advance error: {BuildNetworkErrorMessage(ex)}");
                return null;
            }
        }

        public async Task<TourRoute> GetTourRouteAsync(double fromLat, double fromLon, double toLat, double toLon)
        {
            var fallback = new TourRoute
            {
                Success = true,
                IsFallback = true,
                Provider = "local",
                Points =
                {
                    new RoutePoint { Lat = fromLat, Lon = fromLon },
                    new RoutePoint { Lat = toLat, Lon = toLon }
                }
            };

            if (!IsValidCoordinate(fromLat, fromLon) || !IsValidCoordinate(toLat, toLon))
                return fallback;

            try
            {
                MarkRequestSent();
                var path =
                    $"api/tour/route?fromLat={Uri.EscapeDataString(fromLat.ToString(System.Globalization.CultureInfo.InvariantCulture))}" +
                    $"&fromLon={Uri.EscapeDataString(fromLon.ToString(System.Globalization.CultureInfo.InvariantCulture))}" +
                    $"&toLat={Uri.EscapeDataString(toLat.ToString(System.Globalization.CultureInfo.InvariantCulture))}" +
                    $"&toLon={Uri.EscapeDataString(toLon.ToString(System.Globalization.CultureInfo.InvariantCulture))}";

                var result = await _httpClient.GetFromJsonAsync<TourRoute>(BuildApiUrl(path));
                return result is { Points.Count: > 1 } ? result : fallback;
            }
            catch (Exception ex) when (IsNetworkException(ex))
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] Tour route error: {BuildNetworkErrorMessage(ex)}");
                return fallback;
            }
        }

        public async Task<ActivateTokenResult> ActivateTokenAsync(string accessToken, string? qrRaw = null)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return new ActivateTokenResult
                {
                    Success = false,
                    Message = "Thieu access token de kich hoat."
                };
            }

            try
            {
                MarkRequestSent();
                var response = await _httpClient.PostAsJsonAsync(BuildApiUrl("api/access/token/activate"), new
                {
                    AccessToken = accessToken,
                    ClientDeviceId = _clientDeviceIdentityService.GetOrCreateClientDeviceId(),
                    QrRaw = qrRaw ?? string.Empty
                });

                ActivateTokenResult? result = null;

                try
                {
                    result = await response.Content.ReadFromJsonAsync<ActivateTokenResult>();
                }
                catch
                {
                }

                return result ?? new ActivateTokenResult
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.ReasonPhrase ?? "Khong kich hoat duoc access token."
                };
            }
            catch (Exception ex) when (IsNetworkException(ex))
            {
                return new ActivateTokenResult
                {
                    Success = false,
                    Message = BuildNetworkErrorMessage(ex)
                };
            }
        }

        private static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000.0;
            var dLat = DegreesToRadians(lat2 - lat1);
            var dLon = DegreesToRadians(lon2 - lon1);

            var a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        private static bool IsValidCoordinate(double lat, double lon)
        {
            return !double.IsNaN(lat) &&
                   !double.IsNaN(lon) &&
                   !double.IsInfinity(lat) &&
                   !double.IsInfinity(lon) &&
                   lat is >= -90 and <= 90 &&
                   lon is >= -180 and <= 180;
        }

        private static string BuildApiUrl(string relativePath)
        {
            return BackendUrlResolver.BuildUrl(relativePath);
        }

        private static string? ExtractDeviceCode(string rawValue)
        {
            var normalized = rawValue.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return null;

            if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            {
                var query = ParseQueryString(uri.Query);
                foreach (var key in new[] { "maThietBi", "mathietbi", "deviceCode", "device", "code" })
                {
                    if (query.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                        return value.Trim();
                }

                var lastSegment = uri.Segments.LastOrDefault()?.Trim('/');
                if (!string.IsNullOrWhiteSpace(lastSegment))
                    return lastSegment;
            }

            return normalized;
        }

        private static string? ExtractAccessToken(string rawValue)
        {
            var normalized = rawValue.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return null;

            if (normalized.StartsWith("TEST-", StringComparison.OrdinalIgnoreCase))
                return normalized;

            if (IsLikelyAccessToken(normalized))
                return normalized;

            if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            {
                var query = ParseQueryString(uri.Query);
                foreach (var key in new[] { "accessToken", "access_token", "token" })
                {
                    if (query.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                        return value.Trim();
                }
            }

            return null;
        }

        private static bool IsNetworkException(Exception ex)
        {
            return ex is HttpRequestException ||
                   ex is TaskCanceledException ||
                   ex.InnerException is not null && IsNetworkException(ex.InnerException);
        }

        private static string BuildNetworkErrorMessage(Exception ex)
        {
            var baseUrl = BackendUrlResolver.GetBaseUrl();
            var detail = ex.InnerException?.Message ?? ex.Message;
            return $"Khong ket noi duoc backend ({baseUrl}). Hay chay project VinhKhanh API va thu lai. Chi tiet: {detail}";
        }

        private static bool IsLikelyAccessToken(string value)
        {
            return value.Length is >= 32 and <= 128 &&
                   value.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
        }

        private static Dictionary<string, string> ParseQueryString(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(query))
                return result;

            foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=', 2);
                var key = Uri.UnescapeDataString(pair[0]);
                var value = pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : string.Empty;
                if (!string.IsNullOrWhiteSpace(key))
                    result[key] = value;
            }

            return result;
        }
    }

    public class LoginResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? IdTaiKhoan { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? LoaiTaiKhoan { get; set; }
        public int? IdAdmin { get; set; }
        public int? IdChuQuanLy { get; set; }
        public string? HoTen { get; set; }
    }

    public class QrScanResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? MaThietBi { get; set; }
        public string? AccessToken { get; set; }
        public DateTime? BatDauLuc { get; set; }
        public DateTime? HetHanLuc { get; set; }
        public string? TrangThai { get; set; }
        public int? IdGoi { get; set; }
        public string? TenGoi { get; set; }
        public int? SoNgayHieuLuc { get; set; }
        public string? QrTokenPayload { get; set; }
        public bool EmailSent { get; set; }
        public string? EmailStatusMessage { get; set; }
        public int? IdHoaDon { get; set; }
    }

    public class ValidateAccessResult
    {
        public bool IsValid { get; set; }
        public bool IsNetworkError { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? MaThietBi { get; set; }
        public DateTime? BatDauLuc { get; set; }
        public DateTime? HetHanLuc { get; set; }
        public string? TrangThai { get; set; }
    }

    public class ActivateTokenResult : QrScanResult
    {
    }

    public class PackageAccessRegistrationResult : QrScanResult
    {
    }

    public class PackagePaymentResult : PackageAccessRegistrationResult
    {
        public string? PaymentReference { get; set; }
        public string? PaymentQrPayload { get; set; }
        public string? PaymentContent { get; set; }
        public decimal Amount { get; set; }
        public string? BankBin { get; set; }
        public string? BankAccountNo { get; set; }
        public string? BankAccountName { get; set; }
        public DateTime? PaymentCreatedAt { get; set; }
        public DateTime? PaymentPaidAt { get; set; }
        public string? PaymentStatus { get; set; }
    }

    public class HeartbeatResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime ServerTimeUtc { get; set; }
        public bool MustRevalidate { get; set; }
    }
}
