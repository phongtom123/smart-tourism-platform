using MySqlConnector;
using VinhKhanh.Data;

namespace VinhKhanh.Services
{
    public class AccountAccessService
    {
        private readonly MySqlDbContext _db;

        public AccountAccessService(MySqlDbContext db)
        {
            _db = db;
        }

        public async Task<bool> IsAdminAsync(int idTaiKhoan)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            const string sql = @"
                SELECT 1
                FROM taikhoan tk
                INNER JOIN admin ad ON ad.idTaiKhoan = tk.idTaiKhoan
                WHERE tk.idTaiKhoan = @idTaiKhoan
                  AND tk.loaiTaiKhoan = 'admin'
                  AND tk.tinhTrang = 'hoat_dong'
                LIMIT 1;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@idTaiKhoan", idTaiKhoan);
            return await cmd.ExecuteScalarAsync() != null;
        }

        public async Task<bool> IsOwnerAsync(int idTaiKhoan)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            const string sql = @"
                SELECT 1
                FROM taikhoan tk
                INNER JOIN chu_quan_ly cql ON cql.idTaiKhoan = tk.idTaiKhoan
                WHERE tk.idTaiKhoan = @idTaiKhoan
                  AND tk.loaiTaiKhoan = 'chu_quan_ly'
                  AND tk.tinhTrang = 'hoat_dong'
                LIMIT 1;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@idTaiKhoan", idTaiKhoan);
            return await cmd.ExecuteScalarAsync() != null;
        }

        public async Task<bool> IsStoreOwnedByAccountAsync(int idTaiKhoan, int idGianHang)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            const string sql = @"
                SELECT 1
                FROM gianhang gh
                INNER JOIN chu_quan_ly cql ON cql.idChuQuanLy = gh.idChuQuanLy
                WHERE gh.idGianHang = @idGianHang
                  AND cql.idTaiKhoan = @idTaiKhoan
                LIMIT 1;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@idGianHang", idGianHang);
            cmd.Parameters.AddWithValue("@idTaiKhoan", idTaiKhoan);
            return await cmd.ExecuteScalarAsync() != null;
        }

        public async Task<bool> IsFoodOwnedByAccountAsync(int idTaiKhoan, int idMonAn)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            const string sql = @"
                SELECT 1
                FROM monan ma
                INNER JOIN gianhang gh ON gh.idGianHang = ma.idGianHang
                INNER JOIN chu_quan_ly cql ON cql.idChuQuanLy = gh.idChuQuanLy
                WHERE ma.idMonAn = @idMonAn
                  AND cql.idTaiKhoan = @idTaiKhoan
                LIMIT 1;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@idMonAn", idMonAn);
            cmd.Parameters.AddWithValue("@idTaiKhoan", idTaiKhoan);
            return await cmd.ExecuteScalarAsync() != null;
        }
    }
}
