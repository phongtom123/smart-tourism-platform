using MySqlConnector;
using VinhKhanh.Data;
using VinhKhanh.Dtos;

namespace VinhKhanh.Services
{
    public class AdminService
    {
        private readonly MySqlDbContext _db;

        public AdminService(MySqlDbContext db)
        {
            _db = db;
        }

        public async Task<AdminSummaryDto> GetSummaryAsync()
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            const string paidStoreSubquery = @"
                SELECT latest_invoice.idGianHang
                FROM hoadongianhang latest_invoice
                INNER JOIN (
                    SELECT idGianHang, MAX(idHoaDonGianHang) AS latestId
                    FROM hoadongianhang
                    GROUP BY idGianHang
                ) last_invoice ON last_invoice.latestId = latest_invoice.idHoaDonGianHang
                WHERE latest_invoice.trangThai = 'da_thanh_toan'
            ";

            return new AdminSummaryDto
            {
                TongGianHang = await ExecuteCountAsync(conn, "SELECT COUNT(*) FROM gianhang;"),
                TongChuQuanLy = await ExecuteCountAsync(conn, "SELECT COUNT(*) FROM chu_quan_ly;"),
                TongThietBi = await ExecuteCountAsync(conn, "SELECT COUNT(*) FROM thietbi;"),
                ThietBiDangHoatDong = await ExecuteCountAsync(conn, "SELECT COUNT(*) FROM thietbi WHERE trangThai = 'hoat_dong';"),

                PaidStores = await ExecuteCountAsync(conn,
                    "SELECT COUNT(*) FROM gianhang gh INNER JOIN (" + paidStoreSubquery + ") paid ON paid.idGianHang = gh.idGianHang;"),
                ActiveOwners = await ExecuteCountAsync(conn,
                    "SELECT COUNT(DISTINCT cql.idChuQuanLy) FROM gianhang gh INNER JOIN (" + paidStoreSubquery + ") paid ON paid.idGianHang = gh.idGianHang INNER JOIN chu_quan_ly cql ON cql.idChuQuanLy = gh.idChuQuanLy INNER JOIN taikhoan tk ON tk.idTaiKhoan = cql.idTaiKhoan WHERE tk.tinhTrang = 'hoat_dong';"),
                PaidStoreFoods = await ExecuteCountAsync(conn,
                    "SELECT COUNT(*) FROM monan ma INNER JOIN (" + paidStoreSubquery + ") paid ON paid.idGianHang = ma.idGianHang;"),
                PendingRequests = await ExecuteCountAsync(conn,
                    "SELECT COUNT(*) FROM yeucaugianhang WHERE trangThai = 'cho_duyet';"),
                DevicesWithActiveToken = await ExecuteCountAsync(conn, @"
                    SELECT COUNT(*)
                    FROM phien_vao_app pva
                    INNER JOIN (
                        SELECT maThietBi, MAX(id) AS latestId
                        FROM phien_vao_app
                        GROUP BY maThietBi
                    ) latest ON latest.latestId = pva.id
                    WHERE pva.trangThai = 'hieu_luc' AND pva.hetHanLuc >= NOW();"),
                OnlineDevices = await ExecuteCountAsync(conn,
                    "SELECT COUNT(*) FROM thietbi WHERE lanCuoiHoatDong IS NOT NULL AND lanCuoiHoatDong >= NOW() - INTERVAL 60 SECOND;")
            };
        }

        public async Task<List<AdminStoreDto>> GetStoresAsync()
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            const string sql = @"
                SELECT
                    gh.idGianHang,
                    gh.ten,
                    gh.diaChi,
                    gh.tinhTrang,
                    gh.luotTruyCap,
                    (
                        SELECT hgg.duongDan
                        FROM hinhanhgianhang hgg
                        WHERE hgg.idGianHang = gh.idGianHang
                        ORDER BY hgg.idHinhAnh
                        LIMIT 1
                    ) AS hinhAnh,
                    cql.idChuQuanLy,
                    cql.hoTen AS tenChuQuanLy,
                    tk.email AS emailChuQuanLy,
                    tk.username AS usernameChuQuanLy
                FROM gianhang gh
                LEFT JOIN chu_quan_ly cql ON cql.idChuQuanLy = gh.idChuQuanLy
                LEFT JOIN taikhoan tk ON tk.idTaiKhoan = cql.idTaiKhoan
                ORDER BY gh.idGianHang;";

            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var list = new List<AdminStoreDto>();
            while (await reader.ReadAsync())
            {
                list.Add(new AdminStoreDto
                {
                    IdGianHang = reader.GetInt32("idGianHang"),
                    Ten = reader["ten"]?.ToString() ?? string.Empty,
                    DiaChi = reader["diaChi"]?.ToString(),
                    TinhTrang = reader["tinhTrang"]?.ToString(),
                    HinhAnh = NormalizeImagePathForWeb(reader["hinhAnh"]?.ToString()),
                    LuotTruyCap = reader["luotTruyCap"] == DBNull.Value ? 0 : Convert.ToInt32(reader["luotTruyCap"]),
                    IdChuQuanLy = reader["idChuQuanLy"] == DBNull.Value ? null : Convert.ToInt32(reader["idChuQuanLy"]),
                    TenChuQuanLy = reader["tenChuQuanLy"]?.ToString(),
                    EmailChuQuanLy = reader["emailChuQuanLy"]?.ToString(),
                    UsernameChuQuanLy = reader["usernameChuQuanLy"]?.ToString()
                });
            }

            return list;
        }

        public async Task<List<OwnerOptionDto>> GetOwnersAsync()
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            const string sql = @"
                SELECT
                    cql.idChuQuanLy,
                    cql.idTaiKhoan,
                    cql.hoTen,
                    tk.username,
                    tk.email
                FROM chu_quan_ly cql
                INNER JOIN taikhoan tk ON tk.idTaiKhoan = cql.idTaiKhoan
                WHERE tk.loaiTaiKhoan = 'chu_quan_ly'
                ORDER BY cql.idChuQuanLy;";

            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var list = new List<OwnerOptionDto>();
            while (await reader.ReadAsync())
            {
                list.Add(new OwnerOptionDto
                {
                    IdChuQuanLy = reader.GetInt32("idChuQuanLy"),
                    IdTaiKhoan = reader.GetInt32("idTaiKhoan"),
                    HoTen = reader["hoTen"]?.ToString(),
                    Username = reader["username"]?.ToString(),
                    Email = reader["email"]?.ToString()
                });
            }

            return list;
        }

        public async Task<List<AdminAccountDto>> GetAccountsAsync()
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            const string sql = @"
                SELECT
                    tk.idTaiKhoan,
                    tk.username,
                    tk.email,
                    tk.loaiTaiKhoan,
                    tk.tinhTrang,
                    tk.tinhTrangDangKy,
                    tk.ngayTao,
                    ad.idAdmin,
                    cql.idChuQuanLy,
                    COALESCE(ad.hoTen, cql.hoTen) AS hoTen
                FROM taikhoan tk
                LEFT JOIN admin ad ON ad.idTaiKhoan = tk.idTaiKhoan
                LEFT JOIN chu_quan_ly cql ON cql.idTaiKhoan = tk.idTaiKhoan
                WHERE tk.loaiTaiKhoan IN ('admin', 'chu_quan_ly')
                ORDER BY
                    CASE tk.loaiTaiKhoan
                        WHEN 'admin' THEN 0
                        WHEN 'chu_quan_ly' THEN 1
                        ELSE 2
                    END,
                    tk.ngayTao DESC,
                    tk.idTaiKhoan;";

            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var list = new List<AdminAccountDto>();
            while (await reader.ReadAsync())
            {
                list.Add(new AdminAccountDto
                {
                    IdTaiKhoan = reader.GetInt32("idTaiKhoan"),
                    Username = reader["username"]?.ToString() ?? string.Empty,
                    Email = reader["email"]?.ToString() ?? string.Empty,
                    LoaiTaiKhoan = reader["loaiTaiKhoan"]?.ToString() ?? string.Empty,
                    TinhTrang = reader["tinhTrang"]?.ToString(),
                    TinhTrangDangKy = reader["tinhTrangDangKy"]?.ToString(),
                    NgayTao = Convert.ToDateTime(reader["ngayTao"]),
                    HoTen = reader["hoTen"]?.ToString(),
                    IdAdmin = reader["idAdmin"] == DBNull.Value ? null : Convert.ToInt32(reader["idAdmin"]),
                    IdChuQuanLy = reader["idChuQuanLy"] == DBNull.Value ? null : Convert.ToInt32(reader["idChuQuanLy"])
                });
            }

            return list;
        }

        public async Task<int?> GetOwnerIdByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return null;

            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            const string sql = @"
                SELECT cql.idChuQuanLy
                FROM chu_quan_ly cql
                INNER JOIN taikhoan tk ON tk.idTaiKhoan = cql.idTaiKhoan
                WHERE tk.email = @email
                LIMIT 1;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@email", email.Trim());

            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? null : Convert.ToInt32(result);
        }

        public async Task<AdminAccountDto> CreateAccountAsync(CreateAdminAccountRequestDto request)
        {
            ValidateCreateAccountRequest(request);

            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            await using var transaction = await conn.BeginTransactionAsync();

            var username = request.Username.Trim();
            var email = request.Email.Trim();
            var hoTen = request.HoTen.Trim();
            var matKhau = request.MatKhau;
            var loaiTaiKhoan = NormalizeAccountRole(request.LoaiTaiKhoan);
            var tinhTrang = NormalizeAccountStatus(request.TinhTrang);

            if (await AccountFieldExistsAsync(conn, transaction, "email", email))
                throw new ArgumentException("Email da ton tai trong he thong.");

            if (await AccountFieldExistsAsync(conn, transaction, "username", username))
                throw new ArgumentException("Tai khoan dang nhap da ton tai trong he thong.");

            if (request.IdLienKet.HasValue)
            {
                await EnsureRoleLinkIdAvailableAsync(conn, transaction, loaiTaiKhoan, request.IdLienKet.Value);
            }

            const string insertAccountSql = @"
                INSERT INTO taikhoan (email, matKhau, username, loaiTaiKhoan, tinhTrang, tinhTrangDangKy)
                VALUES (@email, @matKhau, @username, @loaiTaiKhoan, @tinhTrang, 'da_duyet');
                SELECT LAST_INSERT_ID();";

            using var insertAccountCmd = new MySqlCommand(insertAccountSql, conn, transaction);
            insertAccountCmd.Parameters.AddWithValue("@email", email);
            insertAccountCmd.Parameters.AddWithValue("@matKhau", matKhau);
            insertAccountCmd.Parameters.AddWithValue("@username", username);
            insertAccountCmd.Parameters.AddWithValue("@loaiTaiKhoan", loaiTaiKhoan);
            insertAccountCmd.Parameters.AddWithValue("@tinhTrang", tinhTrang);

            var insertedAccountId = Convert.ToInt32(await insertAccountCmd.ExecuteScalarAsync());

            if (loaiTaiKhoan == "admin")
            {
                const string insertAdminSql = @"
                    INSERT INTO admin (idAdmin, idTaiKhoan, hoTen)
                    VALUES (@idAdmin, @idTaiKhoan, @hoTen);";

                using var insertAdminCmd = new MySqlCommand(insertAdminSql, conn, transaction);
                insertAdminCmd.Parameters.AddWithValue("@idAdmin", request.IdLienKet.HasValue ? request.IdLienKet.Value : DBNull.Value);
                insertAdminCmd.Parameters.AddWithValue("@idTaiKhoan", insertedAccountId);
                insertAdminCmd.Parameters.AddWithValue("@hoTen", hoTen);
                await insertAdminCmd.ExecuteNonQueryAsync();
            }
            else
            {
                const string insertOwnerSql = @"
                    INSERT INTO chu_quan_ly (idChuQuanLy, idTaiKhoan, hoTen)
                    VALUES (@idChuQuanLy, @idTaiKhoan, @hoTen);";

                using var insertOwnerCmd = new MySqlCommand(insertOwnerSql, conn, transaction);
                insertOwnerCmd.Parameters.AddWithValue("@idChuQuanLy", request.IdLienKet.HasValue ? request.IdLienKet.Value : DBNull.Value);
                insertOwnerCmd.Parameters.AddWithValue("@idTaiKhoan", insertedAccountId);
                insertOwnerCmd.Parameters.AddWithValue("@hoTen", hoTen);
                await insertOwnerCmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return await GetAccountByIdAsync(insertedAccountId) ?? throw new InvalidOperationException("Khong tao duoc tai khoan moi.");
        }

        public async Task<OperationResultDto> UpdateAccountStatusAsync(int currentAdminAccountId, int targetAccountId, string tinhTrang)
        {
            if (targetAccountId <= 0)
                throw new ArgumentException("ID tai khoan khong hop le.");

            var normalizedStatus = NormalizeAccountStatus(tinhTrang);
            if (currentAdminAccountId == targetAccountId && normalizedStatus == "khoa")
                throw new InvalidOperationException("Khong the khoa chinh tai khoan admin dang dang nhap.");

            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            await using var transaction = await conn.BeginTransactionAsync();

            const string getAccountSql = @"
                SELECT tk.idTaiKhoan, tk.loaiTaiKhoan, cql.idChuQuanLy
                FROM taikhoan tk
                LEFT JOIN chu_quan_ly cql ON cql.idTaiKhoan = tk.idTaiKhoan
                WHERE tk.idTaiKhoan = @idTaiKhoan
                LIMIT 1;";

            string loaiTaiKhoan;
            int? idChuQuanLy = null;

            using (var getAccountCmd = new MySqlCommand(getAccountSql, conn, transaction))
            {
                getAccountCmd.Parameters.AddWithValue("@idTaiKhoan", targetAccountId);
                using var reader = await getAccountCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return new OperationResultDto
                    {
                        Success = false,
                        Message = "Khong tim thay tai khoan can cap nhat."
                    };
                }

                loaiTaiKhoan = reader["loaiTaiKhoan"]?.ToString() ?? string.Empty;
                idChuQuanLy = reader["idChuQuanLy"] == DBNull.Value ? null : Convert.ToInt32(reader["idChuQuanLy"]);
            }

            const string updateAccountSql = @"
                UPDATE taikhoan
                SET tinhTrang = @tinhTrang
                WHERE idTaiKhoan = @idTaiKhoan;";

            using (var updateAccountCmd = new MySqlCommand(updateAccountSql, conn, transaction))
            {
                updateAccountCmd.Parameters.AddWithValue("@tinhTrang", normalizedStatus);
                updateAccountCmd.Parameters.AddWithValue("@idTaiKhoan", targetAccountId);
                await updateAccountCmd.ExecuteNonQueryAsync();
            }

            var affectedStoreCount = 0;
            if (loaiTaiKhoan == "chu_quan_ly" && idChuQuanLy.HasValue && normalizedStatus == "khoa")
            {
                const string updateStoresSql = @"
                    UPDATE gianhang
                    SET tinhTrang = CASE
                        WHEN tinhTrang = 'dong_cua' THEN 'dong_cua'
                        ELSE 'tam_ngung'
                    END,
                    thoiGianCapNhat = NOW()
                    WHERE idChuQuanLy = @idChuQuanLy;";

                using var updateStoresCmd = new MySqlCommand(updateStoresSql, conn, transaction);
                updateStoresCmd.Parameters.AddWithValue("@idChuQuanLy", idChuQuanLy.Value);
                affectedStoreCount = await updateStoresCmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            return new OperationResultDto
            {
                Success = true,
                Message = loaiTaiKhoan == "chu_quan_ly" && normalizedStatus == "khoa"
                    ? $"Cap nhat tinh trang tai khoan thanh cong. Da tam ngung {affectedStoreCount} gian hang cua chu quan ly nay."
                    : "Cap nhat tinh trang tai khoan thanh cong."
            };
        }

        public async Task<List<AdminServicePackageDto>> GetServicePackagesAsync()
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            const string sql = @"
                SELECT idGoi, ten, moTa, gia, thoiHanNgay, trangThai, ngayTao
                FROM goidichvu
                ORDER BY idGoi;";

            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var list = new List<AdminServicePackageDto>();
            while (await reader.ReadAsync())
            {
                list.Add(new AdminServicePackageDto
                {
                    IdGoi = reader.GetInt32("idGoi"),
                    Ten = reader["ten"]?.ToString() ?? string.Empty,
                    MoTa = reader["moTa"] == DBNull.Value ? null : reader["moTa"]?.ToString(),
                    Gia = reader["gia"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["gia"]),
                    ThoiHanNgay = reader["thoiHanNgay"] == DBNull.Value ? 0 : Convert.ToInt32(reader["thoiHanNgay"]),
                    TrangThai = reader["trangThai"]?.ToString() ?? "hoat_dong",
                    NgayTao = Convert.ToDateTime(reader["ngayTao"])
                });
            }

            return list;
        }

        public async Task<AdminServicePackageDto> CreateServicePackageAsync(UpsertServicePackageRequestDto request)
        {
            ValidateServicePackageRequest(request);

            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            await EnsureServicePackageNameAvailableAsync(conn, request.Ten.Trim());

            const string sql = @"
                INSERT INTO goidichvu (ten, moTa, gia, thoiHanNgay, trangThai)
                VALUES (@ten, @moTa, @gia, @thoiHanNgay, @trangThai);
                SELECT LAST_INSERT_ID();";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ten", request.Ten.Trim());
            cmd.Parameters.AddWithValue("@moTa", string.IsNullOrWhiteSpace(request.MoTa) ? DBNull.Value : request.MoTa.Trim());
            cmd.Parameters.AddWithValue("@gia", request.Gia);
            cmd.Parameters.AddWithValue("@thoiHanNgay", request.ThoiHanNgay);
            cmd.Parameters.AddWithValue("@trangThai", NormalizeServicePackageStatus(request.TrangThai));

            var insertedId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return await GetServicePackageByIdAsync(insertedId) ?? throw new InvalidOperationException("Khong tao duoc goi dich vu.");
        }

        public async Task<AdminServicePackageDto?> UpdateServicePackageAsync(int idGoi, UpsertServicePackageRequestDto request)
        {
            ValidateServicePackageRequest(request);

            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            await EnsureServicePackageNameAvailableAsync(conn, request.Ten.Trim(), idGoi);

            const string sql = @"
                UPDATE goidichvu
                SET ten = @ten,
                    moTa = @moTa,
                    gia = @gia,
                    thoiHanNgay = @thoiHanNgay,
                    trangThai = @trangThai
                WHERE idGoi = @idGoi;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@idGoi", idGoi);
            cmd.Parameters.AddWithValue("@ten", request.Ten.Trim());
            cmd.Parameters.AddWithValue("@moTa", string.IsNullOrWhiteSpace(request.MoTa) ? DBNull.Value : request.MoTa.Trim());
            cmd.Parameters.AddWithValue("@gia", request.Gia);
            cmd.Parameters.AddWithValue("@thoiHanNgay", request.ThoiHanNgay);
            cmd.Parameters.AddWithValue("@trangThai", NormalizeServicePackageStatus(request.TrangThai));

            var affectedRows = await cmd.ExecuteNonQueryAsync();
            if (affectedRows <= 0)
                return null;

            return await GetServicePackageByIdAsync(idGoi);
        }

        public async Task<OperationResultDto> UpdateServicePackageStatusAsync(int idGoi, string trangThai)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            const string sql = @"
                UPDATE goidichvu
                SET trangThai = @trangThai
                WHERE idGoi = @idGoi;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@idGoi", idGoi);
            cmd.Parameters.AddWithValue("@trangThai", NormalizeServicePackageStatus(trangThai));

            var affectedRows = await cmd.ExecuteNonQueryAsync();
            return new OperationResultDto
            {
                Success = affectedRows > 0,
                Message = affectedRows > 0
                    ? "Cap nhat trang thai goi dich vu thanh cong."
                    : "Khong tim thay goi dich vu."
            };
        }

        private async Task<AdminServicePackageDto?> GetServicePackageByIdAsync(int idGoi)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            const string sql = @"
                SELECT idGoi, ten, moTa, gia, thoiHanNgay, trangThai, ngayTao
                FROM goidichvu
                WHERE idGoi = @idGoi
                LIMIT 1;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@idGoi", idGoi);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return new AdminServicePackageDto
            {
                IdGoi = reader.GetInt32("idGoi"),
                Ten = reader["ten"]?.ToString() ?? string.Empty,
                MoTa = reader["moTa"] == DBNull.Value ? null : reader["moTa"]?.ToString(),
                Gia = reader["gia"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["gia"]),
                ThoiHanNgay = reader["thoiHanNgay"] == DBNull.Value ? 0 : Convert.ToInt32(reader["thoiHanNgay"]),
                TrangThai = reader["trangThai"]?.ToString() ?? "hoat_dong",
                NgayTao = Convert.ToDateTime(reader["ngayTao"])
            };
        }

        private static void ValidateServicePackageRequest(UpsertServicePackageRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Ten))
                throw new ArgumentException("Ten goi dich vu khong duoc de trong.");

            if (request.Gia < 0)
                throw new ArgumentException("Gia goi dich vu khong hop le.");

            if (request.ThoiHanNgay <= 0)
                throw new ArgumentException("Thoi han ngay phai lon hon 0.");

            NormalizeServicePackageStatus(request.TrangThai);
        }

        private static async Task EnsureServicePackageNameAvailableAsync(MySqlConnection conn, string packageName, int? excludeId = null)
        {
            const string sql = @"
                SELECT 1
                FROM goidichvu
                WHERE ten = @ten
                  AND (@excludeId IS NULL OR idGoi <> @excludeId)
                LIMIT 1;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ten", packageName);
            cmd.Parameters.AddWithValue("@excludeId", excludeId.HasValue ? excludeId.Value : DBNull.Value);

            if (await cmd.ExecuteScalarAsync() != null)
                throw new ArgumentException("Ten goi dich vu da ton tai trong he thong.");
        }

        private static string NormalizeServicePackageStatus(string? status)
        {
            var normalized = (status ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "hoat_dong" => normalized,
                "tam_ngung" => normalized,
                "ngung_ap_dung" => normalized,
                _ => throw new ArgumentException("Trang thai goi dich vu khong hop le.")
            };
        }

        private static async Task<int> ExecuteCountAsync(MySqlConnection conn, string sql)
        {
            using var cmd = new MySqlCommand(sql, conn);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        private async Task<AdminAccountDto?> GetAccountByIdAsync(int idTaiKhoan)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            const string sql = @"
                SELECT
                    tk.idTaiKhoan,
                    tk.username,
                    tk.email,
                    tk.loaiTaiKhoan,
                    tk.tinhTrang,
                    tk.tinhTrangDangKy,
                    tk.ngayTao,
                    ad.idAdmin,
                    cql.idChuQuanLy,
                    COALESCE(ad.hoTen, cql.hoTen) AS hoTen
                FROM taikhoan tk
                LEFT JOIN admin ad ON ad.idTaiKhoan = tk.idTaiKhoan
                LEFT JOIN chu_quan_ly cql ON cql.idTaiKhoan = tk.idTaiKhoan
                WHERE tk.idTaiKhoan = @idTaiKhoan
                LIMIT 1;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@idTaiKhoan", idTaiKhoan);
            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return new AdminAccountDto
            {
                IdTaiKhoan = reader.GetInt32("idTaiKhoan"),
                Username = reader["username"]?.ToString() ?? string.Empty,
                Email = reader["email"]?.ToString() ?? string.Empty,
                LoaiTaiKhoan = reader["loaiTaiKhoan"]?.ToString() ?? string.Empty,
                TinhTrang = reader["tinhTrang"]?.ToString(),
                TinhTrangDangKy = reader["tinhTrangDangKy"]?.ToString(),
                NgayTao = Convert.ToDateTime(reader["ngayTao"]),
                HoTen = reader["hoTen"]?.ToString(),
                IdAdmin = reader["idAdmin"] == DBNull.Value ? null : Convert.ToInt32(reader["idAdmin"]),
                IdChuQuanLy = reader["idChuQuanLy"] == DBNull.Value ? null : Convert.ToInt32(reader["idChuQuanLy"])
            };
        }

        private static void ValidateCreateAccountRequest(CreateAdminAccountRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Username))
                throw new ArgumentException("Tai khoan dang nhap khong duoc de trong.");

            if (string.IsNullOrWhiteSpace(request.Email))
                throw new ArgumentException("Email khong duoc de trong.");

            if (string.IsNullOrWhiteSpace(request.MatKhau))
                throw new ArgumentException("Mat khau khong duoc de trong.");

            if (string.IsNullOrWhiteSpace(request.HoTen))
                throw new ArgumentException("Ten nguoi dung khong duoc de trong.");

            if (request.IdLienKet.HasValue && request.IdLienKet.Value <= 0)
                throw new ArgumentException("ID lien ket phai lon hon 0.");

            NormalizeAccountRole(request.LoaiTaiKhoan);
            NormalizeAccountStatus(request.TinhTrang);
        }

        private static string NormalizeAccountRole(string? role)
        {
            var normalized = (role ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "admin" => normalized,
                "chu_quan_ly" => normalized,
                _ => throw new ArgumentException("Loai tai khoan khong hop le.")
            };
        }

        private static string NormalizeAccountStatus(string? status)
        {
            var normalized = (status ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "hoat_dong" => normalized,
                "khoa" => normalized,
                _ => throw new ArgumentException("Tinh trang tai khoan khong hop le.")
            };
        }

        private static async Task<bool> AccountFieldExistsAsync(MySqlConnection conn, MySqlTransaction transaction, string fieldName, string value)
        {
            var sql = $"SELECT 1 FROM taikhoan WHERE {fieldName} = @value LIMIT 1;";
            using var cmd = new MySqlCommand(sql, conn, transaction);
            cmd.Parameters.AddWithValue("@value", value);
            return await cmd.ExecuteScalarAsync() != null;
        }

        private static async Task EnsureRoleLinkIdAvailableAsync(MySqlConnection conn, MySqlTransaction transaction, string role, int idLienKet)
        {
            var (tableName, columnName, displayName) = role == "admin"
                ? ("admin", "idAdmin", "ID admin")
                : ("chu_quan_ly", "idChuQuanLy", "ID chu quan ly");

            var sql = $"SELECT 1 FROM {tableName} WHERE {columnName} = @id LIMIT 1;";
            using var cmd = new MySqlCommand(sql, conn, transaction);
            cmd.Parameters.AddWithValue("@id", idLienKet);

            if (await cmd.ExecuteScalarAsync() != null)
                throw new ArgumentException(displayName + " da ton tai trong he thong.");
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

        public async Task<List<AdminDeviceDto>> GetDevicesAsync(string? loai = null)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var filter = string.Empty;
            var validLoai = new[] { "app_client", "portal_web", "hardware" };
            var applyFilter = !string.IsNullOrWhiteSpace(loai)
                              && !string.Equals(loai, "all", StringComparison.OrdinalIgnoreCase)
                              && validLoai.Contains(loai);
            if (applyFilter)
                filter = "WHERE tb.loaiThietBi = @loai";

            var sql = $@"
                SELECT
                    tb.idThietBi,
                    tb.maThietBi,
                    tb.daKichHoat,
                    tb.thoiGianKichHoat,
                    tb.lanCuoiHoatDong,
                    tb.trangThai,
                    tb.loaiThietBi,
                    tb.platform,
                    tb.model,
                    tb.manufacturer,
                    tb.appVersion,
                    tb.idTaiKhoan,
                    COALESCE(ad.hoTen, cql.hoTen) AS tenChuSoHuu,
                    tk.email AS emailChuSoHuu
                FROM thietbi tb
                LEFT JOIN taikhoan tk ON tk.idTaiKhoan = tb.idTaiKhoan
                LEFT JOIN admin ad ON ad.idTaiKhoan = tk.idTaiKhoan
                LEFT JOIN chu_quan_ly cql ON cql.idTaiKhoan = tk.idTaiKhoan
                {filter}
                ORDER BY tb.idThietBi DESC;";

            using var cmd = new MySqlCommand(sql, conn);
            if (applyFilter)
                cmd.Parameters.AddWithValue("@loai", loai);
            using var reader = await cmd.ExecuteReaderAsync();

            var list = new List<AdminDeviceDto>();
            while (await reader.ReadAsync())
            {
                list.Add(new AdminDeviceDto
                {
                    IdThietBi = reader.GetInt32("idThietBi"),
                    MaThietBi = reader["maThietBi"]?.ToString() ?? string.Empty,
                    DaKichHoat = Convert.ToBoolean(reader["daKichHoat"]),
                    ThoiGianKichHoat = reader["thoiGianKichHoat"] == DBNull.Value ? null : Convert.ToDateTime(reader["thoiGianKichHoat"]),
                    LanCuoiHoatDong = reader["lanCuoiHoatDong"] == DBNull.Value ? null : Convert.ToDateTime(reader["lanCuoiHoatDong"]),
                    TrangThai = reader["trangThai"]?.ToString() ?? string.Empty,
                    LoaiThietBi = reader["loaiThietBi"]?.ToString() ?? "app_client",
                    Platform = reader["platform"] == DBNull.Value ? null : reader["platform"]?.ToString(),
                    Model = reader["model"] == DBNull.Value ? null : reader["model"]?.ToString(),
                    Manufacturer = reader["manufacturer"] == DBNull.Value ? null : reader["manufacturer"]?.ToString(),
                    AppVersion = reader["appVersion"] == DBNull.Value ? null : reader["appVersion"]?.ToString(),
                    IdTaiKhoan = reader["idTaiKhoan"] == DBNull.Value ? null : Convert.ToInt32(reader["idTaiKhoan"]),
                    TenChuSoHuu = reader["tenChuSoHuu"]?.ToString(),
                    EmailChuSoHuu = reader["emailChuSoHuu"]?.ToString()
                });
            }

            return list;
        }

        public async Task<OperationResultDto> UpdateDeviceStatusAsync(string maThietBi, string trangThai)
        {
            var validStatuses = new[] { "hoat_dong", "khoa", "cho_kich_hoat" };
            if (!validStatuses.Contains(trangThai))
                return new OperationResultDto { Success = false, Message = "Trạng thái không hợp lệ." };

            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            const string sql = @"UPDATE thietbi SET trangThai = @trangThai WHERE maThietBi = @maThietBi;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@trangThai", trangThai);
            cmd.Parameters.AddWithValue("@maThietBi", maThietBi);

            var affected = await cmd.ExecuteNonQueryAsync();
            if (affected == 0)
                return new OperationResultDto { Success = false, Message = "Không tìm thấy thiết bị." };

            return new OperationResultDto { Success = true, Message = "Cập nhật trạng thái thiết bị thành công." };
        }

        public async Task<List<AdminPoiMapItemDto>> GetPoiMapAsync(int? idTaiKhoanOwner)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var sql = @"
                SELECT
                    gh.idGianHang,
                    gh.ten,
                    gh.diaChi,
                    gh.lat,
                    gh.lon,
                    gh.vongBo,
                    gh.luotTruyCap,
                    gh.tinhTrang,
                    gh.phiHangThang,
                    cql.hoTen AS tenChuQuanLy,
                    tk.username AS usernameChuQuanLy,
                    tk.email AS emailChuQuanLy,
                    (
                        SELECT hgg.duongDan
                        FROM hinhanhgianhang hgg
                        WHERE hgg.idGianHang = gh.idGianHang
                        ORDER BY hgg.idHinhAnh
                        LIMIT 1
                    ) AS hinhAnh
                FROM gianhang gh
                LEFT JOIN chu_quan_ly cql ON cql.idChuQuanLy = gh.idChuQuanLy
                LEFT JOIN taikhoan tk ON tk.idTaiKhoan = cql.idTaiKhoan
                WHERE gh.lat IS NOT NULL
                  AND gh.lon IS NOT NULL
                  AND gh.lat BETWEEN -90 AND 90
                  AND gh.lon BETWEEN -180 AND 180";

            if (idTaiKhoanOwner.HasValue)
                sql += " AND cql.idTaiKhoan = @idTaiKhoanOwner";

            sql += @"
                ORDER BY
                    CASE WHEN gh.tinhTrang = 'dang_hoat_dong' THEN 0 ELSE 1 END,
                    gh.phiHangThang DESC,
                    gh.idGianHang ASC;";

            using var cmd = new MySqlCommand(sql, conn);
            if (idTaiKhoanOwner.HasValue)
                cmd.Parameters.AddWithValue("@idTaiKhoanOwner", idTaiKhoanOwner.Value);

            using var reader = await cmd.ExecuteReaderAsync();
            var items = new List<AdminPoiMapItemDto>();
            while (await reader.ReadAsync())
            {
                items.Add(new AdminPoiMapItemDto
                {
                    IdGianHang = reader.GetInt32("idGianHang"),
                    Ten = reader["ten"]?.ToString() ?? string.Empty,
                    DiaChi = reader["diaChi"] == DBNull.Value ? null : reader["diaChi"]?.ToString(),
                    Lat = reader["lat"] == DBNull.Value ? null : Convert.ToDecimal(reader["lat"]),
                    Lon = reader["lon"] == DBNull.Value ? null : Convert.ToDecimal(reader["lon"]),
                    VongBo = reader["vongBo"] == DBNull.Value ? 10m : Convert.ToDecimal(reader["vongBo"]),
                    LuotTruyCap = reader["luotTruyCap"] == DBNull.Value ? 0 : Convert.ToInt32(reader["luotTruyCap"]),
                    TinhTrang = reader["tinhTrang"]?.ToString(),
                    PhiHangThang = reader["phiHangThang"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["phiHangThang"]),
                    TenChuQuanLy = reader["tenChuQuanLy"]?.ToString(),
                    UsernameChuQuanLy = reader["usernameChuQuanLy"]?.ToString(),
                    EmailChuQuanLy = reader["emailChuQuanLy"]?.ToString(),
                    HinhAnh = reader["hinhAnh"] == DBNull.Value ? null : reader["hinhAnh"]?.ToString()
                });
            }
            reader.Close();

            if (items.Count == 0)
                return items;

            var ids = items.Select(p => p.IdGianHang).ToList();
            var placeholders = string.Join(",", ids.Select((_, i) => "@id" + i));

            var visitsSql = $@"
                SELECT idGianHang, ngay, soLuot
                FROM luot_truy_cap_ngay
                WHERE idGianHang IN ({placeholders})
                  AND ngay >= DATE_SUB(CURDATE(), INTERVAL 1 YEAR)
                ORDER BY idGianHang, ngay;";

            using var visitsCmd = new MySqlCommand(visitsSql, conn);
            for (var i = 0; i < ids.Count; i++)
                visitsCmd.Parameters.AddWithValue("@id" + i, ids[i]);

            var byId = items.ToDictionary(p => p.IdGianHang);
            using var visitsReader = await visitsCmd.ExecuteReaderAsync();
            while (await visitsReader.ReadAsync())
            {
                var idStore = visitsReader.GetInt32("idGianHang");
                if (!byId.TryGetValue(idStore, out var poi))
                    continue;
                poi.DailyVisits ??= new Dictionary<string, int>();
                var dateKey = Convert.ToDateTime(visitsReader["ngay"]).ToString("yyyy-MM-dd");
                poi.DailyVisits[dateKey] = visitsReader["soLuot"] == DBNull.Value ? 0 : Convert.ToInt32(visitsReader["soLuot"]);
            }

            return items;
        }

        public async Task<Dictionary<string, int>> GetStoreDailyVisitsAsync(int idGianHang)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            const string sql = @"
                SELECT ngay, soLuot
                FROM luot_truy_cap_ngay
                WHERE idGianHang = @id
                  AND ngay >= DATE_SUB(CURDATE(), INTERVAL 1 YEAR)
                ORDER BY ngay;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", idGianHang);

            var data = new Dictionary<string, int>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var dateKey = Convert.ToDateTime(reader["ngay"]).ToString("yyyy-MM-dd");
                data[dateKey] = reader["soLuot"] == DBNull.Value ? 0 : Convert.ToInt32(reader["soLuot"]);
            }

            return data;
        }
    }
}
