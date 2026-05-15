using MySqlConnector;
using VinhKhanh.Data;
using VinhKhanh.Dtos;

namespace VinhKhanh.Services
{
    public class TourService
    {
        private readonly MySqlDbContext _db;

        public TourService(MySqlDbContext db)
        {
            _db = db;
        }

        private static int? ToNullableInt(object? value) =>
            value == null || value == DBNull.Value ? null : Convert.ToInt32(value);

        private static double? ToNullableDouble(object? value) =>
            value == null || value == DBNull.Value ? null : Convert.ToDouble(value);

        private static DateTime? ToNullableDate(object? value) =>
            value == null || value == DBNull.Value ? null : Convert.ToDateTime(value);

        private static string? ToNullableString(object? value) =>
            value == null || value == DBNull.Value ? null : value.ToString();

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

        public async Task<List<TourDto>> GetActiveToursAsync(int? idNgonNgu = null, CancellationToken ct = default)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync(ct);

            const string sql = @"
                SELECT t.idTour, t.ten, t.moTa, t.idNgonNgu, t.doDaiPhutDeXuat, t.anhBia, t.danhMuc, t.tinhTrang,
                       (SELECT COUNT(*) FROM tour_diem td WHERE td.idTour = t.idTour) AS soStop
                FROM tour t
                WHERE t.tinhTrang = 'hoat_dong'
                  AND (@idNgonNgu IS NULL OR t.idNgonNgu IS NULL OR t.idNgonNgu = @idNgonNgu)
                ORDER BY t.ngayCapNhat DESC;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@idNgonNgu", (object?)idNgonNgu ?? DBNull.Value);

            var list = new List<TourDto>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new TourDto
                {
                    IdTour = Convert.ToInt32(reader["idTour"]),
                    Ten = reader["ten"]?.ToString() ?? string.Empty,
                    MoTa = ToNullableString(reader["moTa"]),
                    IdNgonNgu = ToNullableInt(reader["idNgonNgu"]),
                    DoDaiPhutDeXuat = ToNullableInt(reader["doDaiPhutDeXuat"]),
                    AnhBia = ToNullableString(reader["anhBia"]),
                    DanhMuc = ToNullableString(reader["danhMuc"]),
                    TinhTrang = ToNullableString(reader["tinhTrang"]),
                    SoStop = Convert.ToInt32(reader["soStop"])
                });
            }
            return list;
        }

        public async Task<TourDetailDto?> GetTourDetailAsync(int idTour, CancellationToken ct = default)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync(ct);

            const string tourSql = @"
                SELECT idTour, ten, moTa, idNgonNgu, doDaiPhutDeXuat, anhBia, danhMuc, tinhTrang
                FROM tour WHERE idTour = @id LIMIT 1;";

            using var tourCmd = new MySqlCommand(tourSql, conn);
            tourCmd.Parameters.AddWithValue("@id", idTour);

            TourDetailDto? detail = null;
            using (var reader = await tourCmd.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync()) return null;
                detail = new TourDetailDto
                {
                    Tour = new TourDto
                    {
                        IdTour = Convert.ToInt32(reader["idTour"]),
                        Ten = reader["ten"]?.ToString() ?? string.Empty,
                        MoTa = ToNullableString(reader["moTa"]),
                        IdNgonNgu = ToNullableInt(reader["idNgonNgu"]),
                        DoDaiPhutDeXuat = ToNullableInt(reader["doDaiPhutDeXuat"]),
                        AnhBia = ToNullableString(reader["anhBia"]),
                        DanhMuc = ToNullableString(reader["danhMuc"]),
                        TinhTrang = ToNullableString(reader["tinhTrang"]),
                    }
                };
            }

            // audioURL nam o gianhangngonngu (per-language). Lay theo idNgonNgu cua tour,
            // fallback sang 'vi' (idNgonNgu=1) neu tour khong khai bao ngon ngu.
            var tourLangId = detail.Tour.IdNgonNgu ?? 1;
            const string stopsSql = @"
                SELECT td.idTourDiem, td.idTour, td.idGianHang, td.thuTu,
                       td.audioIntroUrl, td.thoiGianDeXuatPhut, td.ghiChu,
                       COALESCE(ghnn.ten, gh.ten) AS tenGianHang,
                       gh.lat, gh.lon, gh.tinhTrang AS ghTinhTrang,
                       (gh.tinhTrang = 'dang_hoat_dong') AS isAvailable,
                       ghnn.audioURL AS audioMacDinhUrl,
                       (
                           SELECT hgg.duongDan
                           FROM hinhanhgianhang hgg
                           WHERE hgg.idGianHang = gh.idGianHang
                           ORDER BY hgg.idHinhAnh
                           LIMIT 1
                       ) AS hinhAnh
                FROM tour_diem td
                INNER JOIN gianhang gh ON gh.idGianHang = td.idGianHang
                LEFT JOIN gianhangngonngu ghnn
                    ON ghnn.idGianHang = gh.idGianHang
                    AND ghnn.idNgonNgu = @langId
                WHERE td.idTour = @id
                ORDER BY td.thuTu ASC;";

            using var stopsCmd = new MySqlCommand(stopsSql, conn);
            stopsCmd.Parameters.AddWithValue("@id", idTour);
            stopsCmd.Parameters.AddWithValue("@langId", tourLangId);
            using var stopsReader = await stopsCmd.ExecuteReaderAsync();
            while (await stopsReader.ReadAsync())
            {
                detail!.DanhSachStop.Add(new TourDiemDto
                {
                    IdTourDiem = Convert.ToInt32(stopsReader["idTourDiem"]),
                    IdTour = Convert.ToInt32(stopsReader["idTour"]),
                    IdGianHang = Convert.ToInt32(stopsReader["idGianHang"]),
                    ThuTu = Convert.ToInt32(stopsReader["thuTu"]),
                    AudioIntroUrl = ToNullableString(stopsReader["audioIntroUrl"]),
                    ThoiGianDeXuatPhut = ToNullableInt(stopsReader["thoiGianDeXuatPhut"]),
                    GhiChu = ToNullableString(stopsReader["ghiChu"]),
                    TenGianHang = ToNullableString(stopsReader["tenGianHang"]),
                    Lat = ToNullableDouble(stopsReader["lat"]),
                    Lon = ToNullableDouble(stopsReader["lon"]),
                    AudioMacDinhUrl = ToNullableString(stopsReader["audioMacDinhUrl"]),
                    HinhAnh = NormalizeImagePathForWeb(ToNullableString(stopsReader["hinhAnh"])),
                    IsAvailable = Convert.ToInt32(stopsReader["isAvailable"]) == 1,
                    GianHangTinhTrang = ToNullableString(stopsReader["ghTinhTrang"]),
                });
            }
            detail.Tour.SoStop = detail.DanhSachStop.Count;
            return detail;
        }

        public async Task<TourTienDoDto?> GetTienDoAsync(int idTour, string maThietBi, CancellationToken ct = default)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync(ct);
            const string sql = @"
                SELECT idTour, maThietBi, stepHienTai, startedAt, completedAt
                FROM tour_tien_do
                WHERE idTour = @idTour AND maThietBi = @ma LIMIT 1;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@idTour", idTour);
            cmd.Parameters.AddWithValue("@ma", maThietBi);
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            return new TourTienDoDto
            {
                IdTour = Convert.ToInt32(reader["idTour"]),
                MaThietBi = reader["maThietBi"]?.ToString() ?? string.Empty,
                StepHienTai = Convert.ToInt32(reader["stepHienTai"]),
                StartedAt = ToNullableDate(reader["startedAt"]),
                CompletedAt = ToNullableDate(reader["completedAt"]),
            };
        }

        // ===== Admin CRUD (chi admin moi goi) =====

        public async Task<List<TourDto>> GetAllForAdminAsync(CancellationToken ct = default)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync(ct);
            const string sql = @"
                SELECT t.idTour, t.ten, t.moTa, t.idNgonNgu, t.doDaiPhutDeXuat, t.anhBia, t.danhMuc, t.tinhTrang,
                       (SELECT COUNT(*) FROM tour_diem td WHERE td.idTour = t.idTour) AS soStop
                FROM tour t
                ORDER BY t.ngayCapNhat DESC;";
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            var list = new List<TourDto>();
            while (await reader.ReadAsync())
            {
                list.Add(new TourDto
                {
                    IdTour = Convert.ToInt32(reader["idTour"]),
                    Ten = reader["ten"]?.ToString() ?? string.Empty,
                    MoTa = ToNullableString(reader["moTa"]),
                    IdNgonNgu = ToNullableInt(reader["idNgonNgu"]),
                    DoDaiPhutDeXuat = ToNullableInt(reader["doDaiPhutDeXuat"]),
                    AnhBia = ToNullableString(reader["anhBia"]),
                    DanhMuc = ToNullableString(reader["danhMuc"]),
                    TinhTrang = ToNullableString(reader["tinhTrang"]),
                    SoStop = Convert.ToInt32(reader["soStop"])
                });
            }
            return list;
        }

        public async Task<TourActionResultDto> CreateAsync(UpsertTourRequestDto request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Ten))
                return new TourActionResultDto { Success = false, Message = "Thieu ten tour." };

            using var conn = _db.GetConnection();
            await conn.OpenAsync(ct);
            using var tx = await conn.BeginTransactionAsync(ct);

            const string insertTourSql = @"
                INSERT INTO tour (ten, moTa, idNgonNgu, doDaiPhutDeXuat, anhBia, danhMuc, tinhTrang)
                VALUES (@ten, @moTa, @ngonNgu, @doDai, @anhBia, @danhMuc, @tinhTrang);
                SELECT LAST_INSERT_ID();";
            using var cmd = new MySqlCommand(insertTourSql, conn, tx);
            cmd.Parameters.AddWithValue("@ten", request.Ten.Trim());
            cmd.Parameters.AddWithValue("@moTa", (object?)request.MoTa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ngonNgu", (object?)request.IdNgonNgu ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@doDai", (object?)request.DoDaiPhutDeXuat ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@anhBia", (object?)request.AnhBia ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@danhMuc", (object?)request.DanhMuc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tinhTrang", request.TinhTrang ?? "hoat_dong");
            var idTour = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));

            await ReplaceStopsInternalAsync(conn, tx, idTour, request.DanhSachStop ?? new List<UpsertTourStopDto>(), ct);

            await tx.CommitAsync(ct);
            return new TourActionResultDto { Success = true, IdTour = idTour, Message = "Tao tour thanh cong." };
        }

        public async Task<TourActionResultDto> UpdateAsync(int idTour, UpsertTourRequestDto request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Ten))
                return new TourActionResultDto { Success = false, Message = "Thieu ten tour." };

            using var conn = _db.GetConnection();
            await conn.OpenAsync(ct);
            using var tx = await conn.BeginTransactionAsync(ct);

            const string updateSql = @"
                UPDATE tour SET ten=@ten, moTa=@moTa, idNgonNgu=@ngonNgu, doDaiPhutDeXuat=@doDai,
                                anhBia=@anhBia, danhMuc=@danhMuc, tinhTrang=@tinhTrang
                WHERE idTour=@id;";
            using var cmd = new MySqlCommand(updateSql, conn, tx);
            cmd.Parameters.AddWithValue("@id", idTour);
            cmd.Parameters.AddWithValue("@ten", request.Ten.Trim());
            cmd.Parameters.AddWithValue("@moTa", (object?)request.MoTa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ngonNgu", (object?)request.IdNgonNgu ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@doDai", (object?)request.DoDaiPhutDeXuat ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@anhBia", (object?)request.AnhBia ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@danhMuc", (object?)request.DanhMuc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tinhTrang", request.TinhTrang ?? "hoat_dong");
            var affected = await cmd.ExecuteNonQueryAsync(ct);
            if (affected == 0)
            {
                await tx.RollbackAsync(ct);
                return new TourActionResultDto { Success = false, Message = "Khong tim thay tour." };
            }

            await ReplaceStopsInternalAsync(conn, tx, idTour, request.DanhSachStop ?? new List<UpsertTourStopDto>(), ct);

            await tx.CommitAsync(ct);
            return new TourActionResultDto { Success = true, IdTour = idTour, Message = "Cap nhat tour thanh cong." };
        }

        public async Task<TourActionResultDto> DeleteAsync(int idTour, CancellationToken ct = default)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync(ct);
            const string sql = "DELETE FROM tour WHERE idTour = @id;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", idTour);
            var affected = await cmd.ExecuteNonQueryAsync(ct);
            return affected > 0
                ? new TourActionResultDto { Success = true, IdTour = idTour, Message = "Xoa tour thanh cong." }
                : new TourActionResultDto { Success = false, Message = "Khong tim thay tour." };
        }

        private async Task ReplaceStopsInternalAsync(MySqlConnection conn, System.Data.Common.DbTransaction tx,
            int idTour, List<UpsertTourStopDto> stops, CancellationToken ct)
        {
            using (var del = new MySqlCommand("DELETE FROM tour_diem WHERE idTour = @id;", conn, (MySqlTransaction)tx))
            {
                del.Parameters.AddWithValue("@id", idTour);
                await del.ExecuteNonQueryAsync(ct);
            }

            if (stops.Count == 0) return;

            // Sort theo thuTu de luu chuan
            var ordered = stops
                .Where(s => s.IdGianHang > 0)
                .OrderBy(s => s.ThuTu)
                .GroupBy(s => s.IdGianHang)
                .Select(g => g.First())
                .Select((s, idx) => new UpsertTourStopDto
                {
                    IdGianHang = s.IdGianHang,
                    ThuTu = idx + 1, // re-sequence 1..N
                    AudioIntroUrl = s.AudioIntroUrl,
                    ThoiGianDeXuatPhut = s.ThoiGianDeXuatPhut,
                    GhiChu = s.GhiChu,
                })
                .ToList();

            const string insertSql = @"
                INSERT INTO tour_diem (idTour, idGianHang, thuTu, audioIntroUrl, thoiGianDeXuatPhut, ghiChu)
                VALUES (@idTour, @idGh, @thuTu, @audio, @thoiGian, @ghiChu);";
            foreach (var s in ordered)
            {
                using var cmd = new MySqlCommand(insertSql, conn, (MySqlTransaction)tx);
                cmd.Parameters.AddWithValue("@idTour", idTour);
                cmd.Parameters.AddWithValue("@idGh", s.IdGianHang);
                cmd.Parameters.AddWithValue("@thuTu", s.ThuTu);
                cmd.Parameters.AddWithValue("@audio", (object?)s.AudioIntroUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@thoiGian", (object?)s.ThoiGianDeXuatPhut ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ghiChu", (object?)s.GhiChu ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }

        /// <summary>
        /// Khi du khách đến đúng gian hàng kế tiếp (hoặc nhảy tới stop xa hơn) -> advance.
        /// Trả về stop kế tiếp & cờ hoàn thành.
        /// </summary>
        public async Task<AdvanceTourResponseDto> AdvanceAsync(int idTour, string maThietBi, int idGianHangVuaDen, CancellationToken ct = default)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync(ct);
            using var tx = await conn.BeginTransactionAsync(ct);

            const string findStopSql = @"
                SELECT thuTu FROM tour_diem
                WHERE idTour = @idTour AND idGianHang = @idGh LIMIT 1;";
            using var findCmd = new MySqlCommand(findStopSql, conn, tx);
            findCmd.Parameters.AddWithValue("@idTour", idTour);
            findCmd.Parameters.AddWithValue("@idGh", idGianHangVuaDen);
            var thuTuObj = await findCmd.ExecuteScalarAsync(ct);
            if (thuTuObj is null || thuTuObj == DBNull.Value)
            {
                await tx.RollbackAsync(ct);
                return new AdvanceTourResponseDto { Success = false, Message = "Gian hang khong thuoc tour." };
            }
            var thuTuVuaDen = Convert.ToInt32(thuTuObj);

            const string countSql = "SELECT COUNT(*) FROM tour_diem WHERE idTour = @idTour;";
            using var countCmd = new MySqlCommand(countSql, conn, tx);
            countCmd.Parameters.AddWithValue("@idTour", idTour);
            var totalStops = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

            var isCompleted = thuTuVuaDen >= totalStops;
            var stepKeTiep = isCompleted ? totalStops : thuTuVuaDen + 1;

            // Find next AVAILABLE stop (skip paused booths) TRUOC khi upsert,
            // de neu khong con stop available thi danh dau hoan thanh luon.
            int? idGianHangKeTiep = null;
            if (!isCompleted)
            {
                const string nextSql = @"
                    SELECT td.idGianHang FROM tour_diem td
                    INNER JOIN gianhang gh ON gh.idGianHang = td.idGianHang
                    WHERE td.idTour = @idTour
                      AND td.thuTu >= @step
                      AND gh.tinhTrang = 'dang_hoat_dong'
                    ORDER BY td.thuTu ASC LIMIT 1;";
                using var nextCmd = new MySqlCommand(nextSql, conn, tx);
                nextCmd.Parameters.AddWithValue("@idTour", idTour);
                nextCmd.Parameters.AddWithValue("@step", stepKeTiep);
                var nextObj = await nextCmd.ExecuteScalarAsync(ct);
                if (nextObj is not null && nextObj != DBNull.Value)
                {
                    idGianHangKeTiep = Convert.ToInt32(nextObj);
                }
                else
                {
                    isCompleted = true;
                    stepKeTiep = totalStops;
                }
            }

            const string upsertSql = @"
                INSERT INTO tour_tien_do (idTour, maThietBi, stepHienTai, startedAt, completedAt)
                VALUES (@idTour, @ma, @step, NOW(), @completed)
                ON DUPLICATE KEY UPDATE
                    stepHienTai = GREATEST(stepHienTai, VALUES(stepHienTai)),
                    completedAt = COALESCE(completedAt, VALUES(completedAt));";
            using var upsertCmd = new MySqlCommand(upsertSql, conn, tx);
            upsertCmd.Parameters.AddWithValue("@idTour", idTour);
            upsertCmd.Parameters.AddWithValue("@ma", maThietBi);
            upsertCmd.Parameters.AddWithValue("@step", stepKeTiep);
            upsertCmd.Parameters.AddWithValue("@completed", isCompleted ? (object)DateTime.UtcNow : DBNull.Value);
            await upsertCmd.ExecuteNonQueryAsync(ct);

            await tx.CommitAsync(ct);

            return new AdvanceTourResponseDto
            {
                Success = true,
                StepKeTiep = stepKeTiep,
                IdGianHangKeTiep = idGianHangKeTiep,
                IsCompleted = isCompleted,
                Message = isCompleted ? "Hoan thanh tour." : "Da advance den stop ke tiep."
            };
        }
    }
}
