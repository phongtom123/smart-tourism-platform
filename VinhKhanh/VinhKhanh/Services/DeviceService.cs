using MySqlConnector;
using VinhKhanh.Data;
using VinhKhanh.Dtos;

namespace VinhKhanh.Services
{
    public class DeviceService
    {
        private readonly MySqlDbContext _db;

        public DeviceService(MySqlDbContext db)
        {
            _db = db;
        }

        public async Task<ActivateDeviceResponseDto> ActivateAsync(ActivateDeviceRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.MaKichHoat))
            {
                return new ActivateDeviceResponseDto
                {
                    Success = false,
                    Message = "Thiếu mã kích hoạt."
                };
            }

            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var selectSql = string.IsNullOrWhiteSpace(request.MaThietBi)
                ? @"
                SELECT idThietBi, maThietBi, maKichHoat, daKichHoat, thoiGianKichHoat, trangThai
                FROM thietbi
                WHERE maKichHoat = @maKichHoat
                LIMIT 1;"
                : @"
                SELECT idThietBi, maThietBi, maKichHoat, daKichHoat, thoiGianKichHoat, trangThai
                FROM thietbi
                WHERE maThietBi = @maThietBi
                LIMIT 1;";

            using var selectCmd = new MySqlCommand(selectSql, conn);
            if (string.IsNullOrWhiteSpace(request.MaThietBi))
            {
                selectCmd.Parameters.AddWithValue("@maKichHoat", request.MaKichHoat);
            }
            else
            {
                selectCmd.Parameters.AddWithValue("@maThietBi", request.MaThietBi);
            }

            using var reader = await selectCmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return new ActivateDeviceResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy thiết bị phù hợp với mã kích hoạt."
                };
            }

            var maKichHoat = reader["maKichHoat"]?.ToString();
            var daKichHoat = Convert.ToBoolean(reader["daKichHoat"]);
            DateTime? thoiGianKichHoat = reader["thoiGianKichHoat"] == DBNull.Value
                ? null
                : Convert.ToDateTime(reader["thoiGianKichHoat"]);
            var trangThai = reader["trangThai"]?.ToString();
            var maThietBi = reader["maThietBi"]?.ToString();

            await reader.CloseAsync();

            if (!string.Equals(maKichHoat, request.MaKichHoat, StringComparison.Ordinal))
            {
                return new ActivateDeviceResponseDto
                {
                    Success = false,
                    Message = "Mã kích hoạt không đúng.",
                    MaThietBi = maThietBi,
                    DaKichHoat = daKichHoat,
                    ThoiGianKichHoat = thoiGianKichHoat,
                    TrangThai = trangThai
                };
            }

            const string updateSql = @"
                UPDATE thietbi
                SET daKichHoat = 1,
                    thoiGianKichHoat = COALESCE(thoiGianKichHoat, NOW()),
                    lanCuoiHoatDong = NOW(),
                    trangThai = 'hoat_dong'
                WHERE maThietBi = @maThietBi;";

            using var updateCmd = new MySqlCommand(updateSql, conn);
            updateCmd.Parameters.AddWithValue("@maThietBi", maThietBi);
            await updateCmd.ExecuteNonQueryAsync();

            const string refreshSql = @"
                SELECT maThietBi, daKichHoat, thoiGianKichHoat, trangThai
                FROM thietbi
                WHERE maThietBi = @maThietBi
                LIMIT 1;";

            using var refreshCmd = new MySqlCommand(refreshSql, conn);
            refreshCmd.Parameters.AddWithValue("@maThietBi", maThietBi);

            using var refreshed = await refreshCmd.ExecuteReaderAsync();
            await refreshed.ReadAsync();

            return new ActivateDeviceResponseDto
            {
                Success = true,
                Message = "Kích hoạt thiết bị thành công.",
                MaThietBi = refreshed["maThietBi"]?.ToString(),
                DaKichHoat = Convert.ToBoolean(refreshed["daKichHoat"]),
                ThoiGianKichHoat = refreshed["thoiGianKichHoat"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(refreshed["thoiGianKichHoat"]),
                TrangThai = refreshed["trangThai"]?.ToString()
            };
        }

        public async Task<bool> TouchByCodeAsync(
            string maThietBi,
            string? platform = null,
            string? model = null,
            string? manufacturer = null,
            string? appVersion = null)
        {
            if (string.IsNullOrWhiteSpace(maThietBi))
                return false;

            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            const string sql = @"
                UPDATE thietbi
                SET lanCuoiHoatDong = NOW(),
                    platform     = COALESCE(@platform,     platform),
                    model        = COALESCE(@model,        model),
                    manufacturer = COALESCE(@manufacturer, manufacturer),
                    appVersion   = COALESCE(@appVersion,   appVersion)
                WHERE maThietBi = @maThietBi;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@maThietBi", maThietBi);
            cmd.Parameters.AddWithValue("@platform",     (object?)NullIfBlank(platform)     ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@model",        (object?)NullIfBlank(model)        ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@manufacturer", (object?)NullIfBlank(manufacturer) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@appVersion",   (object?)NullIfBlank(appVersion)   ?? DBNull.Value);

            var affected = await cmd.ExecuteNonQueryAsync();
            return affected > 0;
        }

        public async Task<int> TouchBatchAsync(IReadOnlyCollection<string> maThietBiList, CancellationToken ct = default)
        {
            if (maThietBiList.Count == 0)
                return 0;

            // Xây IN clause với tham số đánh số: @p0, @p1, ...
            var paramNames = maThietBiList.Select((_, i) => $"@p{i}").ToList();
            var inClause = string.Join(", ", paramNames);
            var sql = $"UPDATE thietbi SET lanCuoiHoatDong = NOW() WHERE maThietBi IN ({inClause});";

            using var conn = _db.GetConnection();
            await conn.OpenAsync(ct);

            using var cmd = new MySqlCommand(sql, conn);
            var values = maThietBiList.ToList();
            for (var i = 0; i < values.Count; i++)
                cmd.Parameters.AddWithValue($"@p{i}", values[i]);

            return await cmd.ExecuteNonQueryAsync(ct);
        }

        private static string? NullIfBlank(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        public async Task<DeviceStatusDto> GetStatusAsync(string maThietBi)
        {
            if (string.IsNullOrWhiteSpace(maThietBi))
            {
                return new DeviceStatusDto
                {
                    Found = false,
                    Message = "Thiếu mã thiết bị."
                };
            }

            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            const string sql = @"
                SELECT maThietBi, daKichHoat, thoiGianKichHoat, lanCuoiHoatDong, trangThai
                FROM thietbi
                WHERE maThietBi = @maThietBi
                LIMIT 1;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@maThietBi", maThietBi);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return new DeviceStatusDto
                {
                    Found = false,
                    Message = "Không tìm thấy thiết bị."
                };
            }

            return new DeviceStatusDto
            {
                Found = true,
                Message = "Lấy trạng thái thiết bị thành công.",
                MaThietBi = reader["maThietBi"]?.ToString(),
                DaKichHoat = Convert.ToBoolean(reader["daKichHoat"]),
                ThoiGianKichHoat = reader["thoiGianKichHoat"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(reader["thoiGianKichHoat"]),
                LanCuoiHoatDong = reader["lanCuoiHoatDong"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(reader["lanCuoiHoatDong"]),
                TrangThai = reader["trangThai"]?.ToString()
            };
        }
    }
}
