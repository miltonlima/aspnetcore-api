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

    public async Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, email, password_hash FROM users WHERE email = @email";
        cmd.Parameters.AddWithValue("@email", email);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new User
            {
                Id = (int)reader.GetInt64(0),
                Email = reader.GetString(1),
                PasswordHash = reader.GetString(2)
            };
        }
        return null;
    }

    public async Task<User> CreateUserAsync(string email, string password, CancellationToken cancellationToken)
    {
        var passwordHash = BC.HashPassword(password);

        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO users (email, password_hash) VALUES (@email, @password_hash)";
        cmd.Parameters.AddWithValue("@email", email);
        cmd.Parameters.AddWithValue("@password_hash", passwordHash);

        await cmd.ExecuteNonQueryAsync(cancellationToken);

        var user = await GetUserByEmailAsync(email, cancellationToken);
        return user!;
    }
}
