using aspnetcore_api.Contracts;
using aspnetcore_api.Models;
using MySqlConnector;

namespace aspnetcore_api.Services;

public class EducationUnitService
{
    private readonly string _connectionString;

    public EducationUnitService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    private static EducationUnit MapReader(MySqlDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Name = reader.GetString(1),
        Code = reader.GetString(2),
        City = reader.IsDBNull(3) ? null : reader.GetString(3),
        State = reader.IsDBNull(4) ? null : reader.GetString(4),
        Description = reader.IsDBNull(5) ? null : reader.GetString(5),
        CreatedAt = reader.GetDateTime(6)
    };

    private static void ValidateRequest(string name, string code, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nome da unidade é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Código da unidade é obrigatório.");
        }

        if (code.Length > 60)
        {
            throw new ArgumentException("Código da unidade deve ter no máximo 60 caracteres.");
        }

        if (name.Length > 160)
        {
            throw new ArgumentException("Nome da unidade deve ter no máximo 160 caracteres.");
        }

        if (description is { Length: > 1000 })
        {
            throw new ArgumentException("Descrição deve ter no máximo 1000 caracteres.");
        }
    }

    private static (string Name, string Code, string? City, string? State, string? Description) Normalize(CreateEducationUnitRequest request)
    {
        var name = request.Name.Trim();
        var code = request.Code.Trim().ToUpperInvariant();
        var city = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim();
        var state = string.IsNullOrWhiteSpace(request.State) ? null : request.State.Trim();
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        ValidateRequest(name, code, description);

        if (city is { Length: > 160 })
        {
            throw new ArgumentException("Cidade deve ter no máximo 160 caracteres.");
        }

        if (state is { Length: > 80 })
        {
            throw new ArgumentException("Estado deve ter no máximo 80 caracteres.");
        }

        return (name, code, city, state, description);
    }

    private static (string Name, string Code, string? City, string? State, string? Description) Normalize(UpdateEducationUnitRequest request)
    {
        var name = request.Name.Trim();
        var code = request.Code.Trim().ToUpperInvariant();
        var city = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim();
        var state = string.IsNullOrWhiteSpace(request.State) ? null : request.State.Trim();
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        ValidateRequest(name, code, description);

        if (city is { Length: > 160 })
        {
            throw new ArgumentException("Cidade deve ter no máximo 160 caracteres.");
        }

        if (state is { Length: > 80 })
        {
            throw new ArgumentException("Estado deve ter no máximo 80 caracteres.");
        }

        return (name, code, city, state, description);
    }

    private static async Task EnsureTableAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"CREATE TABLE IF NOT EXISTS education_units (
                                    id BIGINT PRIMARY KEY AUTO_INCREMENT,
                                    name VARCHAR(160) NOT NULL,
                                    code VARCHAR(60) NOT NULL,
                                    city VARCHAR(160) NULL,
                                    state VARCHAR(80) NULL,
                                    description TEXT NULL,
                                    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                    CONSTRAINT uq_education_units_code UNIQUE (code)
                                );";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IEnumerable<EducationUnit>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureTableAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT id, name, code, city, state, description, created_at
                                FROM education_units
                                ORDER BY name;";

        var units = new List<EducationUnit>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            units.Add(MapReader(reader));
        }

        return units;
    }

    public async Task<EducationUnit?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureTableAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT id, name, code, city, state, description, created_at
                                FROM education_units
                                WHERE id = @id;";
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapReader(reader);
        }

        return null;
    }

    public async Task<EducationUnit> CreateAsync(CreateEducationUnitRequest request, CancellationToken cancellationToken)
    {
        var (name, code, city, state, description) = Normalize(request);

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureTableAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO education_units (name, code, city, state, description)
                                VALUES (@name, @code, @city, @state, @description);";
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@code", code);
        command.Parameters.AddWithValue("@city", city is null ? DBNull.Value : city);
        command.Parameters.AddWithValue("@state", state is null ? DBNull.Value : state);
        command.Parameters.AddWithValue("@description", description is null ? DBNull.Value : description);

        await command.ExecuteNonQueryAsync(cancellationToken);
        var id = command.LastInsertedId;

        var created = await GetByIdAsync((long)id, cancellationToken)
            ?? throw new InvalidOperationException("Falha ao recuperar a unidade criada.");

        return created;
    }

    public async Task<EducationUnit?> UpdateAsync(long id, UpdateEducationUnitRequest request, CancellationToken cancellationToken)
    {
        var existing = await GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var (name, code, city, state, description) = Normalize(request);

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureTableAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = @"UPDATE education_units
                                SET name = @name,
                                    code = @code,
                                    city = @city,
                                    state = @state,
                                    description = @description
                                WHERE id = @id;";
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@code", code);
        command.Parameters.AddWithValue("@city", city is null ? DBNull.Value : city);
        command.Parameters.AddWithValue("@state", state is null ? DBNull.Value : state);
        command.Parameters.AddWithValue("@description", description is null ? DBNull.Value : description);
        command.Parameters.AddWithValue("@id", id);

        await command.ExecuteNonQueryAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureTableAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM education_units WHERE id = @id;";
        command.Parameters.AddWithValue("@id", id);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }
}
