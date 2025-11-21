using System.Security.Cryptography;
using System.Text;
using Dapper;
using MySqlConnector;
using aspnetcore_api.Models;

namespace aspnetcore_api.Services
{
    public class UserService
    {
        private readonly MySqlConnection _connection;

        public UserService(MySqlConnection connection)
        {
            _connection = connection;
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = _connection.Clone();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INT AUTO_INCREMENT PRIMARY KEY,
                    Email VARCHAR(255) NOT NULL UNIQUE,
                    PasswordHash VARCHAR(255) NOT NULL
                );";
            command.ExecuteNonQuery();
        }

        public async Task<User> CreateUserAsync(string email, string password)
        {
            using var connection = _connection.Clone();
            var passwordHash = HashPassword(password);
            var user = new User { Email = email, PasswordHash = passwordHash };
            var id = await connection.ExecuteAsync(
                "INSERT INTO Users (Email, PasswordHash) VALUES (@Email, @PasswordHash)",
                user
            );
            user.Id = id;
            return user;
        }

        public async Task<User> GetUserByEmailAsync(string email)
        {
            using var connection = _connection.Clone();
            return await connection.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE Email = @Email",
                new { Email = email }
            );
        }

        public bool VerifyPassword(string password, string passwordHash)
        {
            return HashPassword(password) == passwordHash;
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            var builder = new StringBuilder();
            foreach (var b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }
    }
}
