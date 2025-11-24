using aspnetcore_api.Contracts;
using aspnetcore_api.Models;
using MySqlConnector;

namespace aspnetcore_api.Services;

public class EducationClassService
{
    private readonly string _connectionString;

    public EducationClassService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    private static EducationClass MapReader(MySqlDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        EducationUnitId = reader.GetInt64(1),
        Name = reader.GetString(2),
        Code = reader.IsDBNull(3) ? null : reader.GetString(3),
        AcademicYear = reader.IsDBNull(4) ? null : reader.GetString(4),
        Description = reader.IsDBNull(5) ? null : reader.GetString(5),
        CreatedAt = reader.GetDateTime(6),
        EducationUnitName = reader.IsDBNull(7) ? null : reader.GetString(7)
    };

    private static void ValidateRequest(string name, string? code, string? academicYear, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nome da turma é obrigatório.");
        }

        if (name.Length > 160)
        {
            throw new ArgumentException("Nome da turma deve ter no máximo 160 caracteres.");
        }

        if (!string.IsNullOrWhiteSpace(code) && code.Length > 60)
        {
            throw new ArgumentException("Código da turma deve ter no máximo 60 caracteres.");
        }

        if (!string.IsNullOrWhiteSpace(academicYear) && academicYear.Length > 40)
        {
            throw new ArgumentException("Ano/etapa deve ter no máximo 40 caracteres.");
        }

        if (!string.IsNullOrWhiteSpace(description) && description.Length > 1000)
        {
            throw new ArgumentException("Descrição deve ter no máximo 1000 caracteres.");
        }
    }

    private static (string Name, string? Code, string? AcademicYear, string? Description) Normalize(string name, string? code, string? academicYear, string? description)
    {
        var normalizedName = name.Trim();
        var normalizedCode = string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
        var normalizedAcademicYear = string.IsNullOrWhiteSpace(academicYear) ? null : academicYear.Trim();
        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        ValidateRequest(normalizedName, normalizedCode, normalizedAcademicYear, normalizedDescription);
        return (normalizedName, normalizedCode, normalizedAcademicYear, normalizedDescription);
    }

    private static async Task EnsureTableAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"CREATE TABLE IF NOT EXISTS education_classes (
                                    id BIGINT PRIMARY KEY AUTO_INCREMENT,
                                    education_unit_id BIGINT NOT NULL,
                                    name VARCHAR(160) NOT NULL,
                                    code VARCHAR(60) NULL,
                                    academic_year VARCHAR(40) NULL,
                                    description TEXT NULL,
                                    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                    CONSTRAINT fk_education_classes_unit FOREIGN KEY (education_unit_id)
                                        REFERENCES education_units(id)
                                        ON UPDATE CASCADE
                                        ON DELETE RESTRICT,
                                    CONSTRAINT uq_education_classes_code UNIQUE (code)
                                );";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureUnitExistsAsync(MySqlConnection connection, long educationUnitId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM education_units WHERE id = @id;";
        command.Parameters.AddWithValue("@id", educationUnitId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is not long count || count == 0)
        {
            throw new ArgumentException("Unidade de ensino informada não existe.");
        }
    }

    public async Task<IEnumerable<EducationClass>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureTableAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT c.id,
                                        c.education_unit_id,
                                        c.name,
                                        c.code,
                                        c.academic_year,
                                        c.description,
                                        c.created_at,
                                        u.name AS unit_name
                                 FROM education_classes c
                                 INNER JOIN education_units u ON u.id = c.education_unit_id
                                 ORDER BY u.name, c.name;";

        var classes = new List<EducationClass>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            classes.Add(MapReader(reader));
        }

        return classes;
    }

    public async Task<EducationClass?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureTableAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT c.id,
                                        c.education_unit_id,
                                        c.name,
                                        c.code,
                                        c.academic_year,
                                        c.description,
                                        c.created_at,
                                        u.name AS unit_name
                                 FROM education_classes c
                                 INNER JOIN education_units u ON u.id = c.education_unit_id
                                 WHERE c.id = @id;";
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapReader(reader);
        }

        return null;
    }

    private static async Task<string> GenerateNextCodeAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT COALESCE(MAX(CAST(code AS UNSIGNED)), 0) + 1
                                 FROM education_classes
                                 WHERE code REGEXP '^[0-9]+$';";

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result switch
        {
            null => "1",
            DBNull => "1",
            long longValue => longValue.ToString(),
            int intValue => intValue.ToString(),
            decimal decimalValue => Convert.ToInt64(decimalValue).ToString(),
            ulong ulongValue => ulongValue.ToString(),
            _ => long.TryParse(result.ToString(), out var parsed) ? parsed.ToString() : "1"
        };
    }

    public async Task<EducationClass> CreateAsync(CreateEducationClassRequest request, CancellationToken cancellationToken)
    {
        if (request.EducationUnitId <= 0)
        {
            throw new ArgumentException("Unidade de ensino é obrigatória.");
        }

        var (name, _, academicYear, description) = Normalize(request.Name, null, request.AcademicYear, request.Description);

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureTableAsync(connection, cancellationToken);
        await EnsureUnitExistsAsync(connection, request.EducationUnitId, cancellationToken);

        long insertedId;
        while (true)
        {
            var nextCode = await GenerateNextCodeAsync(connection, cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO education_classes (education_unit_id, name, code, academic_year, description)
                                    VALUES (@educationUnitId, @name, @code, @academicYear, @description);";
            command.Parameters.AddWithValue("@educationUnitId", request.EducationUnitId);
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@code", nextCode);
            command.Parameters.AddWithValue("@academicYear", academicYear is null ? DBNull.Value : academicYear);
            command.Parameters.AddWithValue("@description", description is null ? DBNull.Value : description);

            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
                insertedId = (long)command.LastInsertedId;
                break;
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                // Código duplicado; gerar outro valor sequencial.
                continue;
            }
        }

        return await GetByIdAsync(insertedId, cancellationToken)
            ?? throw new InvalidOperationException("Falha ao recuperar a turma criada.");
    }

    public async Task<EducationClass?> UpdateAsync(long id, UpdateEducationClassRequest request, CancellationToken cancellationToken)
    {
        if (request.EducationUnitId <= 0)
        {
            throw new ArgumentException("Unidade de ensino é obrigatória.");
        }

        var existing = await GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var (name, normalizedExistingCode, academicYear, description) = Normalize(request.Name, existing.Code, request.AcademicYear, request.Description);

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureTableAsync(connection, cancellationToken);
        await EnsureUnitExistsAsync(connection, request.EducationUnitId, cancellationToken);

        var currentCode = string.IsNullOrWhiteSpace(normalizedExistingCode) ? null : normalizedExistingCode;

        while (true)
        {
            var codeToPersist = currentCode ?? await GenerateNextCodeAsync(connection, cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = @"UPDATE education_classes
                                    SET education_unit_id = @educationUnitId,
                                        name = @name,
                                        code = @code,
                                        academic_year = @academicYear,
                                        description = @description
                                    WHERE id = @id;";
            command.Parameters.AddWithValue("@educationUnitId", request.EducationUnitId);
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@code", codeToPersist);
            command.Parameters.AddWithValue("@academicYear", academicYear is null ? DBNull.Value : academicYear);
            command.Parameters.AddWithValue("@description", description is null ? DBNull.Value : description);
            command.Parameters.AddWithValue("@id", id);

            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
                break;
            }
            catch (MySqlException ex) when (ex.Number == 1062 && currentCode is null)
            {
                // Código gerado acabou de ser usado; repetir geração.
                currentCode = null;
                continue;
            }
        }

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureTableAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM education_classes WHERE id = @id;";
        command.Parameters.AddWithValue("@id", id);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }
}
