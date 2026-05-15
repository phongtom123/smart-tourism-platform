using MySqlConnector;
using VinhKhanh.Data;
using VinhKhanh.Dtos;

namespace VinhKhanh.Services
{
    public class AuthService
    {
        private readonly MySqlDbContext _db;

        public AuthService(MySqlDbContext db)
        {
            _db = db;
        }

        public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
        {
            var account = !string.IsNullOrWhiteSpace(request.Username)
                ? request.Username.Trim()
                : request.Email.Trim();

            if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(request.MatKhau))
            {
                return new LoginResponseDto
                {
                    Success = false,
                    Message = "Sai tài khoản hoặc mật khẩu."
                };
            }

            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            const string sql = @"
                SELECT
                    tk.idTaiKhoan,
                    tk.username,
                    tk.email,
                    tk.loaiTaiKhoan,
                    ad.idAdmin,
                    cql.idChuQuanLy,
                    COALESCE(ad.hoTen, cql.hoTen) AS hoTen
                FROM taikhoan tk
                LEFT JOIN admin ad ON ad.idTaiKhoan = tk.idTaiKhoan
                LEFT JOIN chu_quan_ly cql ON cql.idTaiKhoan = tk.idTaiKhoan
                WHERE (tk.username = @account OR tk.email = @account)
                  AND tk.matKhau = @matKhau
                  AND tk.tinhTrang = 'hoat_dong'
                LIMIT 1;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@account", account);
            cmd.Parameters.AddWithValue("@matKhau", request.MatKhau);

            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new LoginResponseDto
                {
                    Success = true,
                    Message = "Đăng nhập thành công.",
                    IdTaiKhoan = reader.GetInt32("idTaiKhoan"),
                    Username = reader["username"]?.ToString(),
                    Email = reader["email"]?.ToString(),
                    LoaiTaiKhoan = reader["loaiTaiKhoan"]?.ToString(),
                    IdAdmin = reader["idAdmin"] == DBNull.Value ? null : Convert.ToInt32(reader["idAdmin"]),
                    IdChuQuanLy = reader["idChuQuanLy"] == DBNull.Value ? null : Convert.ToInt32(reader["idChuQuanLy"]),
                    HoTen = reader["hoTen"]?.ToString()
                };
            }

            return new LoginResponseDto
            {
                Success = false,
                Message = "Sai tài khoản hoặc mật khẩu."
            };
        }

        public async Task<OperationResultDto> RegisterOwnerAsync(RegisterOwnerRequestDto request)
        {
            ValidateRegisterOwnerRequest(request);

            var username = request.Username.Trim();
            var email = request.Email.Trim();
            var hoTen = request.HoTen.Trim();
            var matKhau = request.MatKhau;

            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            await using var transaction = await conn.BeginTransactionAsync();

            try
            {
                if (await AccountFieldExistsAsync(conn, transaction, "username", username))
                    throw new ArgumentException("Ten dang nhap da ton tai trong he thong.");

                if (await AccountFieldExistsAsync(conn, transaction, "email", email))
                    throw new ArgumentException("Email da ton tai trong he thong.");

                const string insertAccountSql = @"
                    INSERT INTO taikhoan (email, matKhau, username, loaiTaiKhoan, tinhTrang, tinhTrangDangKy)
                    VALUES (@email, @matKhau, @username, 'chu_quan_ly', 'hoat_dong', 'da_duyet');
                    SELECT LAST_INSERT_ID();";

                using var insertAccountCmd = new MySqlCommand(insertAccountSql, conn, transaction);
                insertAccountCmd.Parameters.AddWithValue("@email", email);
                insertAccountCmd.Parameters.AddWithValue("@matKhau", matKhau);
                insertAccountCmd.Parameters.AddWithValue("@username", username);

                var insertedAccountId = Convert.ToInt32(await insertAccountCmd.ExecuteScalarAsync());

                const string insertOwnerSql = @"
                    INSERT INTO chu_quan_ly (idTaiKhoan, hoTen)
                    VALUES (@idTaiKhoan, @hoTen);";

                using var insertOwnerCmd = new MySqlCommand(insertOwnerSql, conn, transaction);
                insertOwnerCmd.Parameters.AddWithValue("@idTaiKhoan", insertedAccountId);
                insertOwnerCmd.Parameters.AddWithValue("@hoTen", hoTen);
                await insertOwnerCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();

                return new OperationResultDto
                {
                    Success = true,
                    Message = "Dang ky tai khoan chu quan ly thanh cong. Ban co the dang nhap ngay bay gio."
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static void ValidateRegisterOwnerRequest(RegisterOwnerRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.HoTen))
                throw new ArgumentException("Ho ten khong duoc de trong.");

            if (string.IsNullOrWhiteSpace(request.Username))
                throw new ArgumentException("Ten dang nhap khong duoc de trong.");

            if (string.IsNullOrWhiteSpace(request.Email))
                throw new ArgumentException("Email khong duoc de trong.");

            if (!request.Email.Contains('@') || !request.Email.Contains('.'))
                throw new ArgumentException("Email khong dung dinh dang.");

            if (string.IsNullOrWhiteSpace(request.MatKhau))
                throw new ArgumentException("Mat khau khong duoc de trong.");

            if (request.MatKhau.Length < 8)
                throw new ArgumentException("Mat khau phai co it nhat 8 ky tu.");
        }

        private static async Task<bool> AccountFieldExistsAsync(
            MySqlConnection conn,
            MySqlTransaction transaction,
            string fieldName,
            string value)
        {
            var sql = $"SELECT 1 FROM taikhoan WHERE {fieldName} = @value LIMIT 1;";
            using var cmd = new MySqlCommand(sql, conn, transaction);
            cmd.Parameters.AddWithValue("@value", value);
            return await cmd.ExecuteScalarAsync() != null;
        }
    }
}
