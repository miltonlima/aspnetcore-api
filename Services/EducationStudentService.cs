using aspnetcore_api.Contracts;
using aspnetcore_api.Models;
using MySqlConnector;

namespace aspnetcore_api.Services;

public class EducationStudentService
{
    private readonly string _connectionString;

    public EducationStudentService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    private static EducationStudent MapReader(MySqlDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        EducationClassId = reader.GetInt64(1),
        Name = reader.GetString(2),
        RegistrationCode = reader.IsDBNull(3) ? null : reader.GetString(3),
        BirthDate = reader.IsDBNull(4) ? null : DateOnly.FromDateTime(reader.GetDateTime(4)),
        GuardianName = reader.IsDBNull(5) ? null : reader.GetString(5),
        GuardianContact = reader.IsDBNull(6) ? null : reader.GetString(6),
        Notes = reader.IsDBNull(7) ? null : reader.GetString(7),
        CreatedAt = reader.GetDateTime(8),
        ClassName = reader.IsDBNull(9) ? null : reader.GetString(9),
        EducationUnitName = reader.IsDBNull(10) ? null : reader.GetString(10)
    };

    private static void ValidateRequest(string name, string? registrationCode, string? guardianName, string? guardianContact, string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nome do aluno é obrigatório.");
        }

        if (name.Length > 160)
        {
            throw new ArgumentException("Nome do aluno deve ter no máximo 160 caracteres.");
        }

        if (!string.IsNullOrWhiteSpace(registrationCode) && registrationCode.Length > 80)
        {
            throw new ArgumentException("Código de matrícula deve ter no máximo 80 caracteres.");
        }

        if (!string.IsNullOrWhiteSpace(guardianName) && guardianName.Length > 160)
        {
            throw new ArgumentException("Nome do responsável deve ter no máximo 160 caracteres.");
        }

        if (!string.IsNullOrWhiteSpace(guardianContact) && guardianContact.Length > 160)
        {
            throw new ArgumentException("Contato do responsável deve ter no máximo 160 caracteres.");
        }

        if (!string.IsNullOrWhiteSpace(notes) && notes.Length > 1000)
        {
            throw new ArgumentException("Observações devem ter no máximo 1000 caracteres.");
        }
    }

    private static (string Name, string? RegistrationCode, DateOnly? BirthDate, string? GuardianName, string? GuardianContact, string? Notes) Normalize(
        string name,
        string? registrationCode,
        string? birthDate,
        string? guardianName,
        string? guardianContact,
        string? notes)
    {
        var normalizedName = name.Trim();
        var normalizedRegistration = string.IsNullOrWhiteSpace(registrationCode) ? null : registrationCode.Trim().ToUpperInvariant();
        var normalizedGuardianName = string.IsNullOrWhiteSpace(guardianName) ? null : guardianName.Trim();
        var normalizedGuardianContact = string.IsNullOrWhiteSpace(guardianContact) ? null : guardianContact.Trim();
        var normalizedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        DateOnly? parsedBirthDate = null;
        if (!string.IsNullOrWhiteSpace(birthDate))
        {
            if (!DateOnly.TryParse(birthDate, out var parsed))
            {
                throw new ArgumentException("Data de nascimento inválida. Use o formato YYYY-MM-DD.");
            }

            parsedBirthDate = parsed;
        }

        ValidateRequest(normalizedName, normalizedRegistration, normalizedGuardianName, normalizedGuardianContact, normalizedNotes);
        return (normalizedName, normalizedRegistration, parsedBirthDate, normalizedGuardianName, normalizedGuardianContact, normalizedNotes);
    }

    private static async Task EnsureTableAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"CREATE TABLE IF NOT EXISTS education_students (
                                    id BIGINT PRIMARY KEY AUTO_INCREMENT,
                                    education_class_id BIGINT NOT NULL,
                                    name VARCHAR(160) NOT NULL,
                                    registration_code VARCHAR(80) NULL,
                                    birth_date DATE NULL,
                                    guardian_name VARCHAR(160) NULL,
                                    guardian_contact VARCHAR(160) NULL,
                                    notes TEXT NULL,
                                    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                    CONSTRAINT fk_education_students_class FOREIGN KEY (education_class_id)
                                        REFERENCES education_classes(id)
                                        ON UPDATE CASCADE
                                        ON DELETE RESTRICT,
                                    CONSTRAINT uq_education_students_registration UNIQUE (registration_code)
                                );";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureClassExistsAsync(MySqlConnection connection, long educationClassId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM education_classes WHERE id = @id;";
        command.Parameters.AddWithValue("@id", educationClassId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is not long count || count == 0)
        {
            throw new ArgumentException("Turma informada não existe.");
        }
    }

    public async Task<IEnumerable<EducationStudent>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureTableAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT s.id,
                                        s.education_class_id,
                                        s.name,
                                        s.registration_code,
                                        s.birth_date,
                                        s.guardian_name,
                                        s.guardian_contact,
                                        s.notes,
                                        s.created_at,
                                        c.name AS class_name,
                                        u.name AS unit_name
                                 FROM education_students s
                                 INNER JOIN education_classes c ON c.id = s.education_class_id
                                 INNER JOIN education_units u ON u.id = c.education_unit_id
                                 ORDER BY u.name, c.name, s.name;";

        var students = new List<EducationStudent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            students.Add(MapReader(reader));
        }

        return students;
    }

    public async Task<EducationStudent?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureTableAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT s.id,
                                        s.education_class_id,
                                        s.name,
                                        s.registration_code,
                                        s.birth_date,
                                        s.guardian_name,
                                        s.guardian_contact,
                                        s.notes,
                                        s.created_at,
                                        c.name AS class_name,
                                        u.name AS unit_name
                                 FROM education_students s
                                 INNER JOIN education_classes c ON c.id = s.education_class_id
                                 INNER JOIN education_units u ON u.id = c.education_unit_id
                                 WHERE s.id = @id;";
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapReader(reader);
        }

        return null;
    }

    public async Task<EducationStudent> CreateAsync(CreateEducationStudentRequest request, CancellationToken cancellationToken)
    {
        if (request.EducationClassId <= 0)
        {
            throw new ArgumentException("Turma é obrigatória.");
        }

        var (name, registrationCode, birthDate, guardianName, guardianContact, notes) = Normalize(
            request.Name,
            request.RegistrationCode,
            request.BirthDate,
            request.GuardianName,
            request.GuardianContact,
            request.Notes);

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureTableAsync(connection, cancellationToken);
        await EnsureClassExistsAsync(connection, request.EducationClassId, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO education_students (education_class_id, name, registration_code, birth_date, guardian_name, guardian_contact, notes)
                                VALUES (@classId, @name, @registrationCode, @birthDate, @guardianName, @guardianContact, @notes);";
        command.Parameters.AddWithValue("@classId", request.EducationClassId);
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@registrationCode", registrationCode is null ? DBNull.Value : registrationCode);
        command.Parameters.AddWithValue("@birthDate", birthDate.HasValue ? birthDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value);
        command.Parameters.AddWithValue("@guardianName", guardianName is null ? DBNull.Value : guardianName);
        command.Parameters.AddWithValue("@guardianContact", guardianContact is null ? DBNull.Value : guardianContact);
        command.Parameters.AddWithValue("@notes", notes is null ? DBNull.Value : notes);

        await command.ExecuteNonQueryAsync(cancellationToken);
        var id = command.LastInsertedId;

        return await GetByIdAsync((long)id, cancellationToken)
            ?? throw new InvalidOperationException("Falha ao recuperar o aluno criado.");
    }

    public async Task<EducationStudent?> UpdateAsync(long id, UpdateEducationStudentRequest request, CancellationToken cancellationToken)
    {
        if (request.EducationClassId <= 0)
        {
            throw new ArgumentException("Turma é obrigatória.");
        }

        var existing = await GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var (name, registrationCode, birthDate, guardianName, guardianContact, notes) = Normalize(
            request.Name,
            request.RegistrationCode,
            request.BirthDate,
            request.GuardianName,
            request.GuardianContact,
            request.Notes);

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureTableAsync(connection, cancellationToken);
        await EnsureClassExistsAsync(connection, request.EducationClassId, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = @"UPDATE education_students
                                SET education_class_id = @classId,
                                    name = @name,
                                    registration_code = @registrationCode,
                                    birth_date = @birthDate,
                                    guardian_name = @guardianName,
                                    guardian_contact = @guardianContact,
                                    notes = @notes
                                WHERE id = @id;";
        command.Parameters.AddWithValue("@classId", request.EducationClassId);
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@registrationCode", registrationCode is null ? DBNull.Value : registrationCode);
        command.Parameters.AddWithValue("@birthDate", birthDate.HasValue ? birthDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value);
        command.Parameters.AddWithValue("@guardianName", guardianName is null ? DBNull.Value : guardianName);
        command.Parameters.AddWithValue("@guardianContact", guardianContact is null ? DBNull.Value : guardianContact);
        command.Parameters.AddWithValue("@notes", notes is null ? DBNull.Value : notes);
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
        command.CommandText = "DELETE FROM education_students WHERE id = @id;";
        command.Parameters.AddWithValue("@id", id);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }
}
