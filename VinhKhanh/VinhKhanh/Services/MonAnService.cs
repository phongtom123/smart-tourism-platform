using MySqlConnector;
using VinhKhanh.Data;
using VinhKhanh.Dtos;

namespace VinhKhanh.Services
{
    public class MonAnService
    {
        private readonly MySqlDbContext _db;

        public MonAnService(MySqlDbContext db)
        {
            _db = db;
        }

        public async Task<List<MonAnDto>> GetByGianHangAsync(int idGianHang, string lang = "vi")
        {
            var list = new List<MonAnDto>();

            using var conn = _db.GetConnection();
            await conn.OpenAsync();

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
                WHERE ma.idGianHang = @idGianHang
                  AND ma.tinhTrang = 'con_ban'
                GROUP BY
                    ma.idMonAn,
                    ma.idGianHang,
                    ma.ten,
                    mann.ten,
                    ma.donGia,
                    mann.moTa,
                    ma.tinhTrang
                ORDER BY ma.idMonAn;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@lang", lang);
            cmd.Parameters.AddWithValue("@idGianHang", idGianHang);

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

        private static string? NormalizeImagePathForWeb(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            var cleanPath = path.Trim().TrimStart('/');

            if (!cleanPath.StartsWith("images/", StringComparison.OrdinalIgnoreCase) &&
                !cleanPath.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase) &&
                !cleanPath.StartsWith("content/", StringComparison.OrdinalIgnoreCase))
            {
                cleanPath = "images/" + cleanPath;
            }

            return "/" + cleanPath;
        }
    }
}
