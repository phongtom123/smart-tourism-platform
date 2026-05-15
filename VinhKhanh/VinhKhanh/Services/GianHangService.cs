using Microsoft.Extensions.Logging;
using MySqlConnector;
using VinhKhanh.Data;
using VinhKhanh.Dtos;

namespace VinhKhanh.Services
{
    public sealed class VisitRecordResult
    {
        public bool Success { get; set; }
        public bool StoreExists { get; set; }
        public bool Counted { get; set; }
    }

    public class GianHangService
    {
        private readonly MySqlDbContext _db;
        private readonly GoogleTtsService _ttsService;
        private readonly ILogger<GianHangService> _logger;

        public GianHangService(MySqlDbContext db, GoogleTtsService ttsService, ILogger<GianHangService> logger)
        {
            _db = db;
            _ttsService = ttsService;
            _logger = logger;
        }

        public async Task<AppDataDto> GetAppDataAsync(string lang = "vi")
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var gianHangs = await GetAllGianHangDetailsAsync(conn, lang);
            var hinhAnhGianHang = await GetAllHinhAnhGianHangAsync(conn);
            var monAns = await GetAllMonAnAsync(conn, lang);

            foreach (var gianHang in gianHangs)
            {
                if (hinhAnhGianHang.TryGetValue(gianHang.IdGianHang, out var images))
                {
                    gianHang.HinhAnhPhu = images;
                    gianHang.HinhAnhChinh = images.FirstOrDefault();
                }

                gianHang.MonAns = monAns
                    .Where(x => x.IdGianHang == gianHang.IdGianHang)
                    .ToList();
            }

            return new AppDataDto
            {
                GianHangs = gianHangs
            };
        }

        private async Task<List<GianHangDetailDto>> GetAllGianHangDetailsAsync(MySqlConnection conn, string lang)
        {
            var list = new List<GianHangDetailDto>();

            const string sql = @"
                SELECT 
                    gh.idGianHang,
                    COALESCE(ghnn.ten, gh.ten) AS ten,
                    gh.diaChi,
                    ghnn.moTa,
                    ghnn.audioURL,
                    gh.lat,
                    gh.lon,
                    gh.phiHangThang,
                    gh.tinhTrang
                FROM gianhang gh
                LEFT JOIN ngonngu nn 
                    ON nn.maNgonNgu = @lang
                LEFT JOIN gianhangngonngu ghnn 
                    ON ghnn.idGianHang = gh.idGianHang
                    AND ghnn.idNgonNgu = nn.idNgonNgu
                WHERE gh.tinhTrang = 'dang_hoat_dong'
                  AND gh.lat BETWEEN -90 AND 90
                  AND gh.lon BETWEEN -180 AND 180
                ORDER BY gh.idGianHang;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@lang", lang);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new GianHangDetailDto
                {
                    IdGianHang = reader.GetInt32("idGianHang"),
                    Ten = reader["ten"]?.ToString() ?? "",
                    DiaChi = reader["diaChi"]?.ToString(),
                    MoTa = reader["moTa"]?.ToString(),
                    AudioURL = reader["audioURL"]?.ToString(),
                    Lat = reader["lat"] == DBNull.Value ? null : Convert.ToDouble(reader["lat"]),
                    Lon = reader["lon"] == DBNull.Value ? null : Convert.ToDouble(reader["lon"]),
                    PhiHangThang = reader["phiHangThang"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["phiHangThang"]),
                    TinhTrang = reader["tinhTrang"]?.ToString()
                });
            }

            return list;
        }

        private async Task<Dictionary<int, List<string>>> GetAllHinhAnhGianHangAsync(MySqlConnection conn)
        {
            var dict = new Dictionary<int, List<string>>();

            const string sql = @"
                SELECT idGianHang, duongDan
                FROM hinhanhgianhang
                ORDER BY idGianHang, idHinhAnh;";

            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                int idGianHang = reader.GetInt32("idGianHang");
                string duongDan = reader["duongDan"]?.ToString() ?? "";

                if (!dict.ContainsKey(idGianHang))
                    dict[idGianHang] = new List<string>();

                if (!string.IsNullOrWhiteSpace(duongDan))
                {
                    var normalizedPath = NormalizeImagePathForWeb(duongDan);
                    dict[idGianHang].Add(normalizedPath);
                }
            }

            return dict;
        }

        private static string NormalizeImagePathForWeb(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            var cleanPath = path.TrimStart('/');

            if (!cleanPath.StartsWith("images/", StringComparison.OrdinalIgnoreCase) &&
                !cleanPath.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase) &&
                !cleanPath.StartsWith("content/", StringComparison.OrdinalIgnoreCase))
            {
                cleanPath = "images/" + cleanPath;
            }

            return "/" + cleanPath;
        }

        private async Task<List<MonAnDto>> GetAllMonAnAsync(MySqlConnection conn, string lang)
        {
            var list = new List<MonAnDto>();

            const string sql = @"
                SELECT 
                    ma.idMonAn,
                    ma.idGianHang,
                    COALESCE(mann.ten, ma.ten) AS ten,
                    ma.donGia,
                    mann.moTa,
                    ma.tinhTrang,
                    MIN(ham.duongDan) AS hinhAnh
                FROM monan ma
                LEFT JOIN ngonngu nn
                    ON nn.maNgonNgu = @lang
                LEFT JOIN monanngonngu mann
                    ON mann.idMonAn = ma.idMonAn
                    AND mann.idNgonNgu = nn.idNgonNgu
                LEFT JOIN hinhanhmonan ham
                    ON ham.idMonAn = ma.idMonAn
                WHERE ma.tinhTrang = 'con_ban'
                GROUP BY 
                    ma.idMonAn, ma.idGianHang, ma.ten, mann.ten, ma.donGia, mann.moTa, ma.tinhTrang
                ORDER BY ma.idGianHang, ma.idMonAn;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@lang", lang);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new MonAnDto
                {
                    IdMonAn = reader.GetInt32("idMonAn"),
                    IdGianHang = reader.GetInt32("idGianHang"),
                    Ten = reader["ten"]?.ToString() ?? "",
                    DonGia = reader.GetDecimal("donGia"),
                    MoTa = reader["moTa"]?.ToString(),
                    TinhTrang = reader["tinhTrang"]?.ToString(),
                    HinhAnh = NormalizeImagePathForWeb(reader["hinhAnh"]?.ToString())
                });
            }

            return list;
        }

        public async Task<List<GianHangDto>> GetAllAsync(string lang = "vi")
        {
            var data = await GetAppDataAsync(lang);

            return data.GianHangs.Select(x => new GianHangDto
            {
                IdGianHang = x.IdGianHang,
                Ten = x.Ten,
                DiaChi = x.DiaChi,
                MoTa = x.MoTa,
                AudioURL = x.AudioURL,
                HinhAnh = x.HinhAnhChinh,
                Lat = x.Lat,
                Lon = x.Lon,
                PhiHangThang = x.PhiHangThang,
                TinhTrang = x.TinhTrang
            }).ToList();
        }

        public async Task<GianHangDetailDto?> GetByIdAsync(int idGianHang, string lang = "vi")
        {
            var data = await GetAppDataAsync(lang);
            return data.GianHangs.FirstOrDefault(x => x.IdGianHang == idGianHang);
        }

        public async Task<List<GianHangDto>> GetNearbyAsync(double lat, double lon, double radiusMeters = 100, string lang = "vi")
        {
            var all = await GetAllAsync(lang);

            return all
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

        private static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000;
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

        public async Task<object?> GenerateAudioFromMoTaAsync(int idGianHang, string languageCode = "vi")
        {
            var normalizedLanguageCode = NormalizeLanguageCode(languageCode);

            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            const string selectSql = @"
                SELECT 
                    ghnn.id,
                    ghnn.idGianHang,
                    nn.maNgonNgu,
                    ghnn.ten,
                    ghnn.moTa,
                    ghnn.audioURL
                FROM gianhangngonngu ghnn
                INNER JOIN ngonngu nn ON ghnn.idNgonNgu = nn.idNgonNgu
                WHERE ghnn.idGianHang = @idGianHang
                  AND nn.maNgonNgu = @languageCode
                LIMIT 1;";

            using var cmd = new MySqlCommand(selectSql, conn);
            cmd.Parameters.AddWithValue("@idGianHang", idGianHang);
            cmd.Parameters.AddWithValue("@languageCode", normalizedLanguageCode);

            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            var ten = reader["ten"]?.ToString();
            var moTa = reader["moTa"]?.ToString();
            var oldAudioUrl = reader["audioURL"]?.ToString();

            await reader.CloseAsync();

            if (string.IsNullOrWhiteSpace(moTa))
                return null;

            // Filename có timestamp -> URL mới mỗi lần regen, app cache theo URL sẽ tự miss và tải lại.
            var versionToken = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var fileName = $"gianhang_{idGianHang}_{normalizedLanguageCode}_{versionToken}.mp3";
            var generatedUrl = await _ttsService.GenerateSpeechAsync(moTa, fileName, normalizedLanguageCode);
            var dbAudioUrl = generatedUrl.TrimStart('/');

            const string updateSql = @"
                UPDATE gianhangngonngu ghnn
                INNER JOIN ngonngu nn ON ghnn.idNgonNgu = nn.idNgonNgu
                SET ghnn.audioURL = @audioURL
                WHERE ghnn.idGianHang = @idGianHang
                  AND nn.maNgonNgu = @languageCode;";

            using var updateCmd = new MySqlCommand(updateSql, conn);
            updateCmd.Parameters.AddWithValue("@audioURL", dbAudioUrl);
            updateCmd.Parameters.AddWithValue("@idGianHang", idGianHang);
            updateCmd.Parameters.AddWithValue("@languageCode", normalizedLanguageCode);
            await updateCmd.ExecuteNonQueryAsync();

            // Xóa file mp3 cũ (chỉ sau khi DB đã chuyển sang URL mới) để tránh đầy đĩa.
            if (!string.IsNullOrWhiteSpace(oldAudioUrl) &&
                !string.Equals(oldAudioUrl, dbAudioUrl, StringComparison.OrdinalIgnoreCase))
            {
                _ttsService.DeleteAudioIfExists(oldAudioUrl);
            }

            return new
            {
                idGianHang,
                languageCode = normalizedLanguageCode,
                ten,
                moTa,
                audioURL = dbAudioUrl,
                isCached = false
            };
        }

        public async Task<object?> UpdateMoTaAndGenerateAudioAsync(int idGianHang, string languageCode, string moTa)
        {
            var normalizedLanguageCode = NormalizeLanguageCode(languageCode);

            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            const string upsertSql = @"
                INSERT INTO gianhangngonngu (idGianHang, idNgonNgu, ten, audioURL, moTa)
                SELECT
                    gh.idGianHang,
                    nn.idNgonNgu,
                    gh.ten,
                    NULL,
                    @moTa
                FROM gianhang gh
                INNER JOIN ngonngu nn ON nn.maNgonNgu = @languageCode
                WHERE gh.idGianHang = @idGianHang
                ON DUPLICATE KEY UPDATE
                    ten = VALUES(ten),
                    moTa = VALUES(moTa),
                    audioURL = NULL;";

            using var upsertCmd = new MySqlCommand(upsertSql, conn);
            upsertCmd.Parameters.AddWithValue("@moTa", moTa);
            upsertCmd.Parameters.AddWithValue("@idGianHang", idGianHang);
            upsertCmd.Parameters.AddWithValue("@languageCode", normalizedLanguageCode);

            var rows = await upsertCmd.ExecuteNonQueryAsync();
            if (rows <= 0)
                return null;

            return await GenerateAudioFromMoTaAsync(idGianHang, normalizedLanguageCode);
        }

        private static string NormalizeLanguageCode(string? languageCode)
        {
            return string.IsNullOrWhiteSpace(languageCode)
                ? "vi"
                : languageCode.Trim().ToLowerInvariant();
        }

        public async Task<VisitRecordResult> IncrementVisitCountAsync(int idGianHang, string? deviceId = null)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                using var transaction = await conn.BeginTransactionAsync();

                const string storeSql = @"
                    SELECT 1
                    FROM gianhang
                    WHERE idGianHang = @idGianHang
                    LIMIT 1;";

                using var storeCmd = new MySqlCommand(storeSql, conn, transaction);
                storeCmd.Parameters.AddWithValue("@idGianHang", idGianHang);
                var storeExists = await storeCmd.ExecuteScalarAsync();
                if (storeExists == null)
                {
                    await transaction.RollbackAsync();
                    return new VisitRecordResult
                    {
                        Success = false,
                        StoreExists = false,
                        Counted = false
                    };
                }

                var normalizedDeviceId = NormalizeDeviceId(deviceId);
                var shouldCountVisit = true;

                if (!string.IsNullOrWhiteSpace(normalizedDeviceId))
                {
                    const string dedupeSql = @"
                        INSERT IGNORE INTO luot_truy_cap_thiet_bi_ngay (idGianHang, maThietBi, ngay)
                        VALUES (@idGianHang, @maThietBi, CURDATE());";

                    using var dedupeCmd = new MySqlCommand(dedupeSql, conn, transaction);
                    dedupeCmd.Parameters.AddWithValue("@idGianHang", idGianHang);
                    dedupeCmd.Parameters.AddWithValue("@maThietBi", normalizedDeviceId);
                    shouldCountVisit = await dedupeCmd.ExecuteNonQueryAsync() > 0;
                }

                if (shouldCountVisit)
                {
                    const int VISIT_WEIGHT = 1; // +N mỗi lần thiết bị mới ghé trong ngày (đã qua dedupe).

                    const string sql1 = @"
                        UPDATE gianhang
                        SET luotTruyCap = luotTruyCap + @weight
                        WHERE idGianHang = @idGianHang";

                    using var cmd1 = new MySqlCommand(sql1, conn, transaction);
                    cmd1.Parameters.AddWithValue("@idGianHang", idGianHang);
                    cmd1.Parameters.AddWithValue("@weight", VISIT_WEIGHT);
                    await cmd1.ExecuteNonQueryAsync();

                    const string sql2 = @"
                        INSERT INTO luot_truy_cap_ngay (idGianHang, ngay, soLuot)
                        VALUES (@idGianHang, CURDATE(), @weight)
                        ON DUPLICATE KEY UPDATE soLuot = soLuot + @weight";
                    using var cmd2 = new MySqlCommand(sql2, conn, transaction);
                    cmd2.Parameters.AddWithValue("@idGianHang", idGianHang);
                    cmd2.Parameters.AddWithValue("@weight", VISIT_WEIGHT);
                    await cmd2.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();

                return new VisitRecordResult
                {
                    Success = true,
                    StoreExists = true,
                    Counted = shouldCountVisit
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IncrementVisitCountAsync failed for idGianHang={IdGianHang}, deviceId={DeviceId}", idGianHang, deviceId);
                return new VisitRecordResult
                {
                    Success = false,
                    StoreExists = true,
                    Counted = false
                };
            }
        }

        private static string? NormalizeDeviceId(string? deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                return null;

            var normalized = deviceId.Trim().ToUpperInvariant();
            if (normalized.Length > 100)
                normalized = normalized.Substring(0, 100);

            return normalized;
        }

        /// <summary>
        /// Flush 1 batch (idGianHang, maThietBi) duoc PoiVisitWorker gom lai. Voi moi
        /// gian hang trong batch: 1 INSERT IGNORE multi-row vao bang dedup ngay,
        /// 1 UPDATE gianhang.luotTruyCap += newCount, 1 UPSERT luot_truy_cap_ngay.
        /// Tat ca chay trong cung 1 transaction de tranh phan-failure.
        /// </summary>
        public async Task<int> FlushVisitBatchAsync(IEnumerable<PoiVisitItem> items, CancellationToken ct = default)
        {
            var grouped = items
                .Select(x => new { x.IdGianHang, Device = NormalizeDeviceId(x.MaThietBi) })
                .Where(x => !string.IsNullOrWhiteSpace(x.Device))
                .GroupBy(x => x.IdGianHang)
                .Select(g => new
                {
                    BoothId = g.Key,
                    Devices = g.Select(x => x.Device!).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                })
                .Where(g => g.Devices.Count > 0)
                .ToList();

            if (grouped.Count == 0)
                return 0;

            using var conn = _db.GetConnection();
            await conn.OpenAsync(ct);
            using var transaction = await conn.BeginTransactionAsync(ct);

            var totalCounted = 0;

            foreach (var group in grouped)
            {
                var paramNames = group.Devices.Select((_, i) => $"@d{i}").ToList();
                var valuesSql = string.Join(", ", paramNames.Select(p => $"(@booth, {p}, CURDATE())"));

                var insertSql = "INSERT IGNORE INTO luot_truy_cap_thiet_bi_ngay (idGianHang, maThietBi, ngay) VALUES " + valuesSql + ";";
                using var insertCmd = new MySqlCommand(insertSql, conn, transaction);
                insertCmd.Parameters.AddWithValue("@booth", group.BoothId);
                for (var i = 0; i < group.Devices.Count; i++)
                    insertCmd.Parameters.AddWithValue(paramNames[i], group.Devices[i]);

                var newlyInserted = await insertCmd.ExecuteNonQueryAsync(ct);
                if (newlyInserted <= 0)
                    continue;

                using var updateCmd = new MySqlCommand(
                    "UPDATE gianhang SET luotTruyCap = luotTruyCap + @count WHERE idGianHang = @booth;",
                    conn, transaction);
                updateCmd.Parameters.AddWithValue("@count", newlyInserted);
                updateCmd.Parameters.AddWithValue("@booth", group.BoothId);
                await updateCmd.ExecuteNonQueryAsync(ct);

                using var upsertCmd = new MySqlCommand(
                    @"INSERT INTO luot_truy_cap_ngay (idGianHang, ngay, soLuot)
                      VALUES (@booth, CURDATE(), @count)
                      ON DUPLICATE KEY UPDATE soLuot = soLuot + @count;",
                    conn, transaction);
                upsertCmd.Parameters.AddWithValue("@booth", group.BoothId);
                upsertCmd.Parameters.AddWithValue("@count", newlyInserted);
                await upsertCmd.ExecuteNonQueryAsync(ct);

                totalCounted += newlyInserted;
            }

            await transaction.CommitAsync(ct);
            return totalCounted;
        }
    }
}
