using MySqlConnector;
using VinhKhanh.Data;
using VinhKhanh.Dtos;

namespace VinhKhanh.Services
{
    public class OwnerService
    {
        private readonly MySqlDbContext _db;

        public OwnerService(MySqlDbContext db)
        {
            _db = db;
        }

        public async Task<List<OwnerStoreDto>> GetStoresByAccountAsync(int idTaiKhoan)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            const string sql = @"
                SELECT
                    gh.idGianHang,
                    gh.ten,
                    gh.diaChi,
                    gh.lat,
                    gh.lon,
                    gh.tinhTrang,
                    gh.luotTruyCap,
                    (
                        SELECT hgg.duongDan
                        FROM hinhanhgianhang hgg
                        WHERE hgg.idGianHang = gh.idGianHang
                        ORDER BY hgg.idHinhAnh
                        LIMIT 1
                    ) AS hinhAnh,
                    gh.phiHangThang,
                    gh.ngayDangKy,
                    gh.thoiGianCapNhat
                FROM gianhang gh
                INNER JOIN chu_quan_ly cql ON cql.idChuQuanLy = gh.idChuQuanLy
                WHERE cql.idTaiKhoan = @idTaiKhoan
                ORDER BY gh.idGianHang;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@idTaiKhoan", idTaiKhoan);

            using var reader = await cmd.ExecuteReaderAsync();
            var list = new List<OwnerStoreDto>();

            while (await reader.ReadAsync())
            {
                list.Add(new OwnerStoreDto
                {
                    IdGianHang = reader.GetInt32("idGianHang"),
                    Ten = reader["ten"]?.ToString() ?? string.Empty,
                    DiaChi = reader["diaChi"]?.ToString(),
                    Lat = reader["lat"] == DBNull.Value ? null : Convert.ToDouble(reader["lat"]),
                    Lon = reader["lon"] == DBNull.Value ? null : Convert.ToDouble(reader["lon"]),
                    TinhTrang = reader["tinhTrang"]?.ToString(),
                    HinhAnh = NormalizeImagePathForWeb(reader["hinhAnh"]?.ToString()),
                    LuotTruyCap = reader["luotTruyCap"] == DBNull.Value ? 0 : Convert.ToInt32(reader["luotTruyCap"]),
                    PhiHangThang = reader.GetDecimal("phiHangThang"),
                    NgayDangKy = Convert.ToDateTime(reader["ngayDangKy"]),
                    ThoiGianCapNhat = reader["thoiGianCapNhat"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(reader["thoiGianCapNhat"])
                });
            }

            return list;
        }

        public async Task<OwnerStoreDto> CreateStoreAsync(int idTaiKhoan, UpsertStoreRequestDto request, StoreManagementService storeManagementService)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            const string sql = @"
                SELECT idChuQuanLy
                FROM chu_quan_ly
                WHERE idTaiKhoan = @idTaiKhoan
                LIMIT 1;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@idTaiKhoan", idTaiKhoan);
            var result = await cmd.ExecuteScalarAsync();

            if (result == null || result == DBNull.Value)
                throw new InvalidOperationException("Tai khoan khong co ho so chu quan ly.");

            var ownerId = Convert.ToInt32(result);
            return await storeManagementService.CreateStoreAsync(request, ownerId);
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
