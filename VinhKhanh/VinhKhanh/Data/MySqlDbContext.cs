using MySqlConnector;

namespace VinhKhanh.Data
{
    public class MySqlDbContext
    {
        private readonly IConfiguration _config;

        public MySqlDbContext(IConfiguration config)
        {
            _config = config;
        }

        public MySqlConnection GetConnection()
        {
            return new MySqlConnection(_config.GetConnectionString("DefaultConnection"));
        }
    }
}