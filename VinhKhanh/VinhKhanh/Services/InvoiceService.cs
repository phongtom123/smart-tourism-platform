using MySqlConnector;
using VinhKhanh.Data;
using VinhKhanh.Dtos;

namespace VinhKhanh.Services
{
    public class InvoiceService
    {
        private readonly MySqlDbContext _db;

        public InvoiceService(MySqlDbContext db)
        {
            _db = db;
        }

        public async Task<List<AdminInvoiceDto>> ListAsync(int? idTaiKhoanOwner)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var sql = @"
                SELECT
                    hdgh.idHoaDonGianHang,
                    hdgh.idGianHang,
                    hdgh.tongTien,
                    hdgh.ngayHetHan,
                    hdgh.trangThai,
                    hdgh.ghiChu,
                    hdgh.ngayTao,
                    gh.ten AS tenGianHang,
                    gh.diaChi,
                    gh.phiHangThang,
                    gh.tinhTrang AS tinhTrangGianHang,
                    gh.idChuQuanLy,
                    cql.idTaiKhoan AS idTaiKhoanChuQuanLy,
                    cql.hoTen AS hoTenChuQuanLy,
                    tk.username AS usernameChuQuanLy,
                    tk.email AS emailChuQuanLy
                FROM hoadongianhang hdgh
                INNER JOIN gianhang gh ON gh.idGianHang = hdgh.idGianHang
                LEFT JOIN chu_quan_ly cql ON cql.idChuQuanLy = gh.idChuQuanLy
                LEFT JOIN taikhoan tk ON tk.idTaiKhoan = cql.idTaiKhoan";

            if (idTaiKhoanOwner.HasValue)
                sql += " WHERE cql.idTaiKhoan = @idTaiKhoanOwner";

            sql += @"
                ORDER BY
                    CASE hdgh.trangThai
                        WHEN 'chua_thanh_toan' THEN 0
                        WHEN 'qua_han' THEN 1
                        WHEN 'da_thanh_toan' THEN 2
                        ELSE 3
                    END,
                    hdgh.ngayTao DESC,
                    hdgh.idHoaDonGianHang DESC;";

            using var cmd = new MySqlCommand(sql, conn);
            if (idTaiKhoanOwner.HasValue)
                cmd.Parameters.AddWithValue("@idTaiKhoanOwner", idTaiKhoanOwner.Value);

            using var reader = await cmd.ExecuteReaderAsync();
            var list = new List<AdminInvoiceDto>();
            while (await reader.ReadAsync())
            {
                list.Add(MapRow(reader));
            }

            return list;
        }

        public async Task<AdminInvoiceDto?> GetByIdAsync(int idHoaDon, int? idTaiKhoanOwner)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var sql = @"
                SELECT
                    hdgh.idHoaDonGianHang,
                    hdgh.idGianHang,
                    hdgh.tongTien,
                    hdgh.ngayHetHan,
                    hdgh.trangThai,
                    hdgh.ghiChu,
                    hdgh.ngayTao,
                    gh.ten AS tenGianHang,
                    gh.diaChi,
                    gh.phiHangThang,
                    gh.tinhTrang AS tinhTrangGianHang,
                    gh.idChuQuanLy,
                    cql.idTaiKhoan AS idTaiKhoanChuQuanLy,
                    cql.hoTen AS hoTenChuQuanLy,
                    tk.username AS usernameChuQuanLy,
                    tk.email AS emailChuQuanLy
                FROM hoadongianhang hdgh
                INNER JOIN gianhang gh ON gh.idGianHang = hdgh.idGianHang
                LEFT JOIN chu_quan_ly cql ON cql.idChuQuanLy = gh.idChuQuanLy
                LEFT JOIN taikhoan tk ON tk.idTaiKhoan = cql.idTaiKhoan
                WHERE hdgh.idHoaDonGianHang = @id";

            if (idTaiKhoanOwner.HasValue)
                sql += " AND cql.idTaiKhoan = @idTaiKhoanOwner";

            sql += " LIMIT 1;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", idHoaDon);
            if (idTaiKhoanOwner.HasValue)
                cmd.Parameters.AddWithValue("@idTaiKhoanOwner", idTaiKhoanOwner.Value);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return MapRow(reader);
        }

        public async Task<AdminInvoiceStatusDto?> GetStatusAsync(int idHoaDon)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            const string sql = @"
                SELECT idHoaDonGianHang, trangThai
                FROM hoadongianhang
                WHERE idHoaDonGianHang = @id
                LIMIT 1;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", idHoaDon);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return new AdminInvoiceStatusDto
            {
                IdHoaDonGianHang = reader.GetInt32("idHoaDonGianHang"),
                TrangThai = reader["trangThai"]?.ToString() ?? "chua_thanh_toan"
            };
        }

        public async Task<bool> MarkPaidAsync(int idHoaDon)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                using var updateCmd = new MySqlCommand(
                    "UPDATE hoadongianhang SET trangThai = 'da_thanh_toan' WHERE idHoaDonGianHang = @id AND trangThai IN ('chua_thanh_toan', 'qua_han');",
                    conn, transaction);
                updateCmd.Parameters.AddWithValue("@id", idHoaDon);
                var rows = await updateCmd.ExecuteNonQueryAsync();

                if (rows == 0)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                using var reactivateCmd = new MySqlCommand(@"
                    UPDATE gianhang gh
                    INNER JOIN hoadongianhang hdgh ON hdgh.idGianHang = gh.idGianHang
                    SET gh.tinhTrang = 'dang_hoat_dong', gh.thoiGianCapNhat = NOW()
                    WHERE hdgh.idHoaDonGianHang = @id
                      AND gh.tinhTrang = 'tam_ngung';", conn, transaction);
                reactivateCmd.Parameters.AddWithValue("@id", idHoaDon);
                await reactivateCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static AdminInvoiceDto MapRow(MySqlDataReader reader)
        {
            return new AdminInvoiceDto
            {
                IdHoaDonGianHang = reader.GetInt32("idHoaDonGianHang"),
                IdGianHang = reader.GetInt32("idGianHang"),
                TongTien = reader["tongTien"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["tongTien"]),
                NgayHetHan = reader["ngayHetHan"] == DBNull.Value ? null : Convert.ToDateTime(reader["ngayHetHan"]),
                TrangThai = reader["trangThai"]?.ToString(),
                GhiChu = reader["ghiChu"] == DBNull.Value ? null : reader["ghiChu"]?.ToString(),
                NgayTao = reader["ngayTao"] == DBNull.Value ? null : Convert.ToDateTime(reader["ngayTao"]),
                TenGianHang = reader["tenGianHang"]?.ToString(),
                DiaChi = reader["diaChi"] == DBNull.Value ? null : reader["diaChi"]?.ToString(),
                PhiHangThang = reader["phiHangThang"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["phiHangThang"]),
                TinhTrangGianHang = reader["tinhTrangGianHang"]?.ToString(),
                IdChuQuanLy = reader["idChuQuanLy"] == DBNull.Value ? null : Convert.ToInt32(reader["idChuQuanLy"]),
                IdTaiKhoanChuQuanLy = reader["idTaiKhoanChuQuanLy"] == DBNull.Value ? null : Convert.ToInt32(reader["idTaiKhoanChuQuanLy"]),
                HoTenChuQuanLy = reader["hoTenChuQuanLy"]?.ToString(),
                UsernameChuQuanLy = reader["usernameChuQuanLy"]?.ToString(),
                EmailChuQuanLy = reader["emailChuQuanLy"]?.ToString()
            };
        }
    }
}
