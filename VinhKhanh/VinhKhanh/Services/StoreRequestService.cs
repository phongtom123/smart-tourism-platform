using MySqlConnector;
using VinhKhanh.Data;
using VinhKhanh.Dtos;

namespace VinhKhanh.Services
{
    public class StoreRequestService
    {
        private static readonly HashSet<string> RequestStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "cho_duyet",
            "cho_thanh_toan",
            "tu_choi"
        };

        private readonly MySqlDbContext _db;

        public StoreRequestService(MySqlDbContext db)
        {
            _db = db;
        }

        public async Task<List<StoreRequestDto>> GetRequestsAsync(string? status = null)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            await EnsureStoreRequestTableAsync(conn);

            var normalizedStatus = NormalizeOptionalRequestStatus(status);
            var sql = BuildRequestQuery(includeStatusFilter: !string.IsNullOrWhiteSpace(normalizedStatus), filterByOwner: false);

            using var cmd = new MySqlCommand(sql, conn);
            if (!string.IsNullOrWhiteSpace(normalizedStatus))
            {
                cmd.Parameters.AddWithValue("@trangThai", normalizedStatus);
            }

            using var reader = await cmd.ExecuteReaderAsync();
            var items = new List<StoreRequestDto>();
            while (await reader.ReadAsync())
            {
                items.Add(MapStoreRequest(reader));
            }

            return items;
        }

        public async Task<List<StoreRequestDto>> GetRequestsByOwnerAsync(int idTaiKhoan)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            await EnsureStoreRequestTableAsync(conn);

            using var cmd = new MySqlCommand(BuildRequestQuery(includeStatusFilter: false, filterByOwner: true), conn);
            cmd.Parameters.AddWithValue("@idTaiKhoan", idTaiKhoan);

            using var reader = await cmd.ExecuteReaderAsync();
            var items = new List<StoreRequestDto>();
            while (await reader.ReadAsync())
            {
                items.Add(MapStoreRequest(reader));
            }

            return items;
        }

        public async Task<StoreRequestDto> CreateRequestAsync(int idTaiKhoan, CreateStoreRequestDto request)
        {
            ValidateCreateRequest(request);

            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            await EnsureStoreRequestTableAsync(conn);
            await EnsureStoreNameAvailableAsync(conn, request.Ten);

            var ownerId = await ResolveOwnerIdAsync(conn, idTaiKhoan);
            if (ownerId <= 0)
                throw new InvalidOperationException("Tai khoan khong co ho so chu quan ly.");

            const string sql = @"
                INSERT INTO yeucaugianhang
                (
                    idChuQuanLy,
                    tenDeNghi,
                    diaChiDeNghi,
                    ghiChuGui,
                    trangThai,
                    ngayGui
                )
                VALUES
                (
                    @idChuQuanLy,
                    @tenDeNghi,
                    @diaChiDeNghi,
                    @ghiChuGui,
                    'cho_duyet',
                    NOW()
                );
                SELECT LAST_INSERT_ID();";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@idChuQuanLy", ownerId);
            cmd.Parameters.AddWithValue("@tenDeNghi", request.Ten.Trim());
            cmd.Parameters.AddWithValue("@diaChiDeNghi", string.IsNullOrWhiteSpace(request.DiaChi) ? DBNull.Value : request.DiaChi.Trim());
            cmd.Parameters.AddWithValue("@ghiChuGui", string.IsNullOrWhiteSpace(request.MoTa) ? DBNull.Value : request.MoTa.Trim());

            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return await GetRequestByIdAsync(newId, conn)
                ?? throw new InvalidOperationException("Khong the tai yeu cau vua tao.");
        }

        public async Task<StoreRequestDto?> ReviewRequestAsync(int idYeuCau, int reviewerAccountId, ReviewStoreRequestDto request)
        {
            _ = reviewerAccountId;

            var requestStatus = NormalizeReviewDecision(request.TrangThaiYeuCau);
            var reviewedFee = NormalizeReviewedFee(request.PhiHangThang, requestStatus);
            var reviewedCoordinates = NormalizeReviewedCoordinates(request.Lat, request.Lon, requestStatus);

            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            await EnsureStoreRequestTableAsync(conn);

            await using var transaction = await conn.BeginTransactionAsync();
            var currentRequest = await GetEditableRequestRowAsync(idYeuCau, conn, transaction);
            if (currentRequest == null)
            {
                await transaction.RollbackAsync();
                return null;
            }

            if (!string.Equals(currentRequest.TrangThai, "cho_duyet", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Yeu cau nay da duoc xu ly truoc do.");

            int? createdStoreId = null;
            if (string.Equals(requestStatus, "cho_thanh_toan", StringComparison.OrdinalIgnoreCase))
            {
                createdStoreId = await CreateStoreFromRequestAsync(
                    currentRequest,
                    reviewedFee!.Value,
                    reviewedCoordinates!.Lat,
                    reviewedCoordinates.Lon,
                    conn,
                    transaction
                );
            }

            const string updateSql = @"
                UPDATE yeucaugianhang
                SET trangThai = @trangThai,
                    idGianHang = COALESCE(@idGianHang, idGianHang),
                    ngayXuLy = NOW()
                WHERE idYeuCau = @idYeuCau;";

            using (var updateCmd = new MySqlCommand(updateSql, conn, transaction))
            {
                updateCmd.Parameters.AddWithValue("@trangThai", requestStatus);
                updateCmd.Parameters.AddWithValue("@idGianHang", createdStoreId.HasValue ? createdStoreId.Value : DBNull.Value);
                updateCmd.Parameters.AddWithValue("@idYeuCau", idYeuCau);
                await updateCmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return await GetRequestByIdAsync(idYeuCau, conn);
        }

        private static string BuildRequestQuery(bool includeStatusFilter, bool filterByOwner)
        {
            var sql = @"
                SELECT
                    ycg.idYeuCau,
                    'them_gian_hang' AS loaiYeuCau,
                    ycg.idChuQuanLy,
                    cql.idTaiKhoan AS idTaiKhoanChuQuanLy,
                    cql.hoTen AS hoTenChuQuanLy,
                    tk.username AS usernameChuQuanLy,
                    tk.email AS emailChuQuanLy,
                    ycg.tenDeNghi AS tenGianHang,
                    ycg.diaChiDeNghi AS diaChi,
                    ycg.ghiChuGui AS moTa,
                    'vi' AS ngonNguMoTa,
                    gh.lat AS lat,
                    gh.lon AS lon,
                    gh.phiHangThang AS phiHangThang,
                    COALESCE(gh.tinhTrang, 'dang_hoat_dong') AS tinhTrangDeXuat,
                    ycg.trangThai AS trangThaiYeuCau,
                    NULL AS ghiChuXuLy,
                    NULL AS idTaiKhoanXuLy,
                    NULL AS tenNguoiXuLy,
                    ycg.idGianHang,
                    ycg.ngayGui AS ngayTao,
                    ycg.ngayXuLy AS thoiGianXuLy
                FROM yeucaugianhang ycg
                INNER JOIN chu_quan_ly cql ON cql.idChuQuanLy = ycg.idChuQuanLy
                INNER JOIN taikhoan tk ON tk.idTaiKhoan = cql.idTaiKhoan
                LEFT JOIN gianhang gh ON gh.idGianHang = ycg.idGianHang";

            var conditions = new List<string>();
            if (includeStatusFilter)
            {
                conditions.Add("ycg.trangThai = @trangThai");
            }

            if (filterByOwner)
            {
                conditions.Add("cql.idTaiKhoan = @idTaiKhoan");
            }

            if (conditions.Count > 0)
            {
                sql += Environment.NewLine + " WHERE " + string.Join(" AND ", conditions);
            }

            sql += @"
                ORDER BY
                    CASE ycg.trangThai
                        WHEN 'cho_duyet' THEN 0
                        WHEN 'cho_thanh_toan' THEN 1
                        WHEN 'da_duyet' THEN 2
                        ELSE 3
                    END,
                    ycg.ngayGui DESC,
                    ycg.idYeuCau DESC;";

            return sql;
        }

        private static StoreRequestDto MapStoreRequest(MySqlDataReader reader)
        {
            return new StoreRequestDto
            {
                IdYeuCau = reader.GetInt32("idYeuCau"),
                LoaiYeuCau = reader["loaiYeuCau"]?.ToString() ?? "them_gian_hang",
                IdChuQuanLy = reader.GetInt32("idChuQuanLy"),
                IdTaiKhoanChuQuanLy = reader.GetInt32("idTaiKhoanChuQuanLy"),
                HoTenChuQuanLy = reader["hoTenChuQuanLy"] == DBNull.Value ? null : reader["hoTenChuQuanLy"]?.ToString(),
                UsernameChuQuanLy = reader["usernameChuQuanLy"] == DBNull.Value ? null : reader["usernameChuQuanLy"]?.ToString(),
                EmailChuQuanLy = reader["emailChuQuanLy"] == DBNull.Value ? null : reader["emailChuQuanLy"]?.ToString(),
                TenGianHang = reader["tenGianHang"]?.ToString() ?? string.Empty,
                DiaChi = reader["diaChi"] == DBNull.Value ? null : reader["diaChi"]?.ToString(),
                MoTa = reader["moTa"] == DBNull.Value ? null : reader["moTa"]?.ToString(),
                NgonNguMoTa = reader["ngonNguMoTa"]?.ToString() ?? "vi",
                Lat = reader["lat"] == DBNull.Value ? null : Convert.ToDouble(reader["lat"]),
                Lon = reader["lon"] == DBNull.Value ? null : Convert.ToDouble(reader["lon"]),
                PhiHangThang = reader["phiHangThang"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["phiHangThang"]),
                TinhTrangDeXuat = reader["tinhTrangDeXuat"]?.ToString() ?? "dang_hoat_dong",
                TrangThaiYeuCau = reader["trangThaiYeuCau"]?.ToString() ?? "cho_duyet",
                GhiChuXuLy = reader["ghiChuXuLy"] == DBNull.Value ? null : reader["ghiChuXuLy"]?.ToString(),
                IdTaiKhoanXuLy = reader["idTaiKhoanXuLy"] == DBNull.Value ? null : Convert.ToInt32(reader["idTaiKhoanXuLy"]),
                TenNguoiXuLy = reader["tenNguoiXuLy"] == DBNull.Value ? null : reader["tenNguoiXuLy"]?.ToString(),
                IdGianHang = reader["idGianHang"] == DBNull.Value ? null : Convert.ToInt32(reader["idGianHang"]),
                NgayTao = Convert.ToDateTime(reader["ngayTao"]),
                ThoiGianXuLy = reader["thoiGianXuLy"] == DBNull.Value ? null : Convert.ToDateTime(reader["thoiGianXuLy"])
            };
        }

        private async Task<StoreRequestDto?> GetRequestByIdAsync(int idYeuCau, MySqlConnection conn)
        {
            var sql = BuildRequestQuery(includeStatusFilter: false, filterByOwner: false)
                .Replace(" ORDER BY", " WHERE ycg.idYeuCau = @idYeuCau ORDER BY", StringComparison.Ordinal);

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@idYeuCau", idYeuCau);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return MapStoreRequest(reader);
        }

        private async Task<EditableStoreRequestRow?> GetEditableRequestRowAsync(int idYeuCau, MySqlConnection conn, MySqlTransaction transaction)
        {
            const string sql = @"
                SELECT
                    idYeuCau,
                    idChuQuanLy,
                    tenDeNghi,
                    diaChiDeNghi,
                    ghiChuGui,
                    trangThai
                FROM yeucaugianhang
                WHERE idYeuCau = @idYeuCau
                LIMIT 1
                FOR UPDATE;";

            using var cmd = new MySqlCommand(sql, conn, transaction);
            cmd.Parameters.AddWithValue("@idYeuCau", idYeuCau);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return new EditableStoreRequestRow
            {
                IdYeuCau = reader.GetInt32("idYeuCau"),
                IdChuQuanLy = reader.GetInt32("idChuQuanLy"),
                TenDeNghi = reader["tenDeNghi"]?.ToString() ?? string.Empty,
                DiaChiDeNghi = reader["diaChiDeNghi"] == DBNull.Value ? null : reader["diaChiDeNghi"]?.ToString(),
                GhiChuGui = reader["ghiChuGui"] == DBNull.Value ? null : reader["ghiChuGui"]?.ToString(),
                TrangThai = reader["trangThai"]?.ToString() ?? "cho_duyet"
            };
        }

        private async Task<int> ResolveOwnerIdAsync(MySqlConnection conn, int idTaiKhoan)
        {
            const string sql = @"
                SELECT idChuQuanLy
                FROM chu_quan_ly
                WHERE idTaiKhoan = @idTaiKhoan
                LIMIT 1;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@idTaiKhoan", idTaiKhoan);
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        private static async Task<int> CreateStoreFromRequestAsync(
            EditableStoreRequestRow request,
            decimal phiHangThang,
            double lat,
            double lon,
            MySqlConnection conn,
            MySqlTransaction transaction)
        {
            await EnsureStoreNameAvailableAsync(conn, request.TenDeNghi, transaction: transaction);

            const string sql = @"
                INSERT INTO gianhang
                (
                    idChuQuanLy,
                    ten,
                    diaChi,
                    lat,
                    lon,
                    tinhTrang,
                    phiHangThang,
                    ngayDangKy,
                    thoiGianCapNhat
                )
                VALUES
                (
                    @idChuQuanLy,
                    @ten,
                    @diaChi,
                    @lat,
                    @lon,
                    'tam_ngung',
                    @phiHangThang,
                    NOW(),
                    NOW()
                );
                SELECT LAST_INSERT_ID();";

            using var cmd = new MySqlCommand(sql, conn, transaction);
            cmd.Parameters.AddWithValue("@idChuQuanLy", request.IdChuQuanLy);
            cmd.Parameters.AddWithValue("@ten", request.TenDeNghi);
            cmd.Parameters.AddWithValue("@diaChi", (object?)request.DiaChiDeNghi ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@lat", lat);
            cmd.Parameters.AddWithValue("@lon", lon);
            cmd.Parameters.AddWithValue("@phiHangThang", phiHangThang);

            var storeId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            const string invoiceSql = @"
                INSERT INTO hoadongianhang (idGianHang, tongTien, ngayHetHan, trangThai, ghiChu, ngayTao)
                VALUES (@storeId, @tongTien, DATE_ADD(NOW(), INTERVAL 1 MONTH), 'chua_thanh_toan', 'Phí duy trì tháng đầu tiên', NOW());";

            using var invoiceCmd = new MySqlCommand(invoiceSql, conn, transaction);
            invoiceCmd.Parameters.AddWithValue("@storeId", storeId);
            invoiceCmd.Parameters.AddWithValue("@tongTien", phiHangThang);
            await invoiceCmd.ExecuteNonQueryAsync();

            return storeId;
        }

        private static async Task EnsureStoreNameAvailableAsync(MySqlConnection conn, string storeName, MySqlTransaction? transaction = null)
        {
            const string sql = @"
                SELECT COUNT(*)
                FROM gianhang
                WHERE LOWER(TRIM(ten)) = LOWER(TRIM(@ten));";

            using var cmd = new MySqlCommand(sql, conn, transaction);
            cmd.Parameters.AddWithValue("@ten", storeName.Trim());

            var duplicateCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            if (duplicateCount > 0)
                throw new ArgumentException("Ten gian hang da ton tai. Vui long chon ten khac.");
        }

        private static void ValidateCreateRequest(CreateStoreRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Ten))
                throw new ArgumentException("Ten gian hang khong duoc rong.");
        }

        private static string NormalizeOptionalRequestStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status) || string.Equals(status.Trim(), "all", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            var normalized = status.Trim().ToLowerInvariant();
            if (!RequestStatuses.Contains(normalized))
                throw new ArgumentException("Trang thai yeu cau khong hop le.");

            return normalized;
        }

        private static string NormalizeReviewDecision(string? status)
        {
            var normalized = string.IsNullOrWhiteSpace(status)
                ? "cho_thanh_toan"
                : status.Trim().ToLowerInvariant();

            if (!string.Equals(normalized, "cho_thanh_toan", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalized, "tu_choi", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Chi ho tro duyet hoac tu choi yeu cau.");
            }

            return normalized;
        }

        private static decimal? NormalizeReviewedFee(decimal? phiHangThang, string requestStatus)
        {
            if (!string.Equals(requestStatus, "cho_thanh_toan", StringComparison.OrdinalIgnoreCase))
                return phiHangThang;

            if (!phiHangThang.HasValue)
                throw new ArgumentException("Admin phai nhap phi hang thang truoc khi phe duyet.");

            if (phiHangThang.Value < 0)
                throw new ArgumentException("Phi hang thang khong hop le.");

            return phiHangThang.Value;
        }

        private static ReviewedCoordinates? NormalizeReviewedCoordinates(double? lat, double? lon, string requestStatus)
        {
            if (!string.Equals(requestStatus, "cho_thanh_toan", StringComparison.OrdinalIgnoreCase))
                return null;

            if (!lat.HasValue || !lon.HasValue)
                throw new ArgumentException("Admin phai nhap day du vi do va kinh do truoc khi phe duyet.");

            return new ReviewedCoordinates
            {
                Lat = lat.Value,
                Lon = lon.Value
            };
        }

        private static async Task EnsureStoreRequestTableAsync(MySqlConnection conn)
        {
            const string sql = @"
                CREATE TABLE IF NOT EXISTS yeucaugianhang (
                    idYeuCau INT NOT NULL AUTO_INCREMENT,
                    idChuQuanLy INT NOT NULL,
                    tenDeNghi VARCHAR(150) NOT NULL,
                    diaChiDeNghi VARCHAR(255) DEFAULT NULL,
                    ghiChuGui TEXT DEFAULT NULL,
                    trangThai ENUM('cho_duyet','da_duyet','tu_choi','cho_thanh_toan') NOT NULL DEFAULT 'cho_duyet',
                    idGianHang INT DEFAULT NULL,
                    ngayGui DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP(),
                    ngayXuLy DATETIME DEFAULT NULL,
                    PRIMARY KEY (idYeuCau),
                    KEY idx_yeucaugianhang_owner (idChuQuanLy),
                    KEY idx_yeucaugianhang_status (trangThai),
                    KEY idx_yeucaugianhang_store (idGianHang),
                    CONSTRAINT fk_yeucaugianhang_owner
                        FOREIGN KEY (idChuQuanLy) REFERENCES chu_quan_ly (idChuQuanLy)
                        ON DELETE CASCADE ON UPDATE CASCADE,
                    CONSTRAINT fk_yeucaugianhang_store
                        FOREIGN KEY (idGianHang) REFERENCES gianhang (idGianHang)
                        ON DELETE SET NULL ON UPDATE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

            using var cmd = new MySqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        private sealed class EditableStoreRequestRow
        {
            public int IdYeuCau { get; set; }
            public int IdChuQuanLy { get; set; }
            public string TenDeNghi { get; set; } = string.Empty;
            public string? DiaChiDeNghi { get; set; }
            public string? GhiChuGui { get; set; }
            public string TrangThai { get; set; } = "cho_duyet";
        }

        private sealed class ReviewedCoordinates
        {
            public double Lat { get; set; }
            public double Lon { get; set; }
        }
    }
}
