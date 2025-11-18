using aspnetcore_api.Models;
using MySqlConnector;
using System.Threading.Tasks;
using BC = BCrypt.Net.BCrypt;

namespace aspnetcore_api.Services;

public class AuthService
{
    private readonly IConfiguration _configuration;

    public AuthService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<User?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, username, password_hash FROM users WHERE username = @username";
        cmd.Parameters.AddWithValue("@username", username);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new User
            {
                Id = (int)reader.GetInt64(0),
                Username = reader.GetString(1),
                PasswordHash = reader.GetString(2)
            };
        }
        return null;
    }

    public async Task<User> CreateUserAsync(string username, string password, CancellationToken cancellationToken)
    {
        var passwordHash = BC.HashPassword(password);

        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO users (username, password_hash) VALUES (@username, @password_hash)";
        cmd.Parameters.AddWithValue("@username", username);
        cmd.Parameters.AddWithValue("@password_hash", passwordHash);

        await cmd.ExecuteNonQueryAsync(cancellationToken);

        var user = await GetUserByUsernameAsync(username, cancellationToken);
        return user!;
    }
}
