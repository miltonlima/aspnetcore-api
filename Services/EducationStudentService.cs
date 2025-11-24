using System.Globalization;
using System.Text.RegularExpressions;
using aspnetcore_api.Contracts;
using aspnetcore_api.Models;
using MySqlConnector;

namespace aspnetcore_api.Services;

public class EducationStudentService
{
    private readonly string _connectionString;
    private const int RegistrationSequenceDigits = 6;
    private static readonly Regex NumericRegistrationRegex = new("^[0-9]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public EducationStudentService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    private static EducationStudent MapStudent(MySqlDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Name = reader.GetString(1),
        RegistrationCode = reader.IsDBNull(2) ? null : reader.GetString(2),
        Cpf = reader.IsDBNull(3) ? null : reader.GetString(3),
        BirthDate = reader.IsDBNull(4) ? null : DateOnly.FromDateTime(reader.GetDateTime(4)),
        GuardianName = reader.IsDBNull(5) ? null : reader.GetString(5),
        GuardianContact = reader.IsDBNull(6) ? null : reader.GetString(6),
        Notes = reader.IsDBNull(7) ? null : reader.GetString(7),
        CreatedAt = reader.GetDateTime(8),
        Enrollments = new List<EducationStudentEnrollment>()
    };

    private static EducationStudentEnrollment MapEnrollment(MySqlDataReader reader) => new()
    {
        StudentId = reader.GetInt64(0),
        EducationClassId = reader.GetInt64(1),
        CreatedAt = reader.GetDateTime(2),
        EducationUnitId = reader.GetInt64(3),
        EducationClassName = reader.IsDBNull(4) ? null : reader.GetString(4),
        EducationUnitName = reader.IsDBNull(5) ? null : reader.GetString(5)
    };

    private static void ValidateStudent(string name, string? registrationCode, string? cpf, string? guardianName, string? guardianContact, string? notes)
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

        if (!string.IsNullOrWhiteSpace(registrationCode) && !NumericRegistrationRegex.IsMatch(registrationCode))
        {
            throw new ArgumentException("Código de matrícula deve conter apenas números.");
        }

        if (!string.IsNullOrWhiteSpace(cpf) && cpf.Length != 11)
        {
            throw new ArgumentException("CPF deve conter 11 dígitos.");
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

    private static (string Name, string? RegistrationCode, string? Cpf, DateOnly? BirthDate, string? GuardianName, string? GuardianContact, string? Notes) NormalizeStudent(
        string name,
        string? registrationCode,
        string? cpf,
        string? birthDate,
        string? guardianName,
        string? guardianContact,
        string? notes)
    {
        var normalizedName = name.Trim();
        var normalizedRegistration = NormalizeRegistrationCode(registrationCode);
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

        var normalizedCpf = NormalizeCpf(cpf);

        ValidateStudent(normalizedName, normalizedRegistration, normalizedCpf, normalizedGuardianName, normalizedGuardianContact, normalizedNotes);
        return (normalizedName, normalizedRegistration, normalizedCpf, parsedBirthDate, normalizedGuardianName, normalizedGuardianContact, normalizedNotes);
    }

    private static string? NormalizeRegistrationCode(string? registrationCode)
    {
        if (string.IsNullOrWhiteSpace(registrationCode))
        {
            return null;
        }

        var digitsOnly = Regex.Replace(registrationCode, "[^0-9]", string.Empty);
        if (string.IsNullOrWhiteSpace(digitsOnly))
        {
            throw new ArgumentException("Código de matrícula deve conter apenas números.");
        }

        return digitsOnly;
    }

    private static string? NormalizeCpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
        {
            return null;
        }

        var digitsOnly = Regex.Replace(cpf, "[^0-9]", string.Empty);
        return string.IsNullOrWhiteSpace(digitsOnly) ? null : digitsOnly;
    }

    private static async Task EnsureStudentTableAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"CREATE TABLE IF NOT EXISTS education_students (
                                    id BIGINT PRIMARY KEY AUTO_INCREMENT,
                                    name VARCHAR(160) NOT NULL,
                                    registration_code VARCHAR(80) NULL,
                                    cpf VARCHAR(11) NULL,
                                    birth_date DATE NULL,
                                    guardian_name VARCHAR(160) NULL,
                                    guardian_contact VARCHAR(160) NULL,
                                    notes TEXT NULL,
                                    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                    CONSTRAINT uq_education_students_registration UNIQUE (registration_code),
                                    CONSTRAINT uq_education_students_cpf UNIQUE (cpf)
                                );";
        await command.ExecuteNonQueryAsync(cancellationToken);

        command.CommandText = @"SELECT CONSTRAINT_NAME
                                   FROM information_schema.KEY_COLUMN_USAGE
                                   WHERE TABLE_SCHEMA = DATABASE()
                                     AND TABLE_NAME = 'education_students'
                                     AND COLUMN_NAME = 'education_class_id'
                                     AND REFERENCED_TABLE_NAME IS NOT NULL
                                   LIMIT 1;";
        var fkResult = await command.ExecuteScalarAsync(cancellationToken);
        if (fkResult is string fkName && !string.IsNullOrWhiteSpace(fkName))
        {
            command.CommandText = "ALTER TABLE education_students DROP FOREIGN KEY `" + fkName + "`;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        command.CommandText = @"SELECT COUNT(*)
                                   FROM information_schema.COLUMNS
                                   WHERE TABLE_SCHEMA = DATABASE()
                                     AND TABLE_NAME = 'education_students'
                                     AND COLUMN_NAME = 'education_class_id';";
        var hasLegacyColumn = await command.ExecuteScalarAsync(cancellationToken);
        if (hasLegacyColumn is long legacy && legacy > 0)
        {
            command.CommandText = "ALTER TABLE education_students DROP COLUMN education_class_id;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        command.CommandText = @"SELECT COUNT(*)
                                   FROM information_schema.COLUMNS
                                   WHERE TABLE_SCHEMA = DATABASE()
                                     AND TABLE_NAME = 'education_students'
                                     AND COLUMN_NAME = 'cpf';";
        var hasCpfColumn = await command.ExecuteScalarAsync(cancellationToken);
        if (hasCpfColumn is long cpfColumn && cpfColumn == 0)
        {
            command.CommandText = "ALTER TABLE education_students ADD COLUMN cpf VARCHAR(11) NULL AFTER registration_code;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        command.CommandText = @"SELECT COUNT(*)
                                   FROM information_schema.STATISTICS
                                   WHERE TABLE_SCHEMA = DATABASE()
                                     AND TABLE_NAME = 'education_students'
                                     AND INDEX_NAME = 'uq_education_students_cpf';";
        var hasCpfIndex = await command.ExecuteScalarAsync(cancellationToken);
        if (hasCpfIndex is long cpfIndex && cpfIndex == 0)
        {
            command.CommandText = "CREATE UNIQUE INDEX uq_education_students_cpf ON education_students (cpf);";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task EnsureEnrollmentTableAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"CREATE TABLE IF NOT EXISTS education_student_enrollments (
                                    id BIGINT PRIMARY KEY AUTO_INCREMENT,
                                    student_id BIGINT NOT NULL,
                                    class_id BIGINT NOT NULL,
                                    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                    CONSTRAINT fk_student_enrollment_student FOREIGN KEY (student_id)
                                        REFERENCES education_students(id)
                                        ON DELETE CASCADE,
                                    CONSTRAINT fk_student_enrollment_class FOREIGN KEY (class_id)
                                        REFERENCES education_classes(id)
                                        ON DELETE CASCADE,
                                    CONSTRAINT uq_student_enrollment UNIQUE (student_id, class_id)
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

    private static async Task<bool> StudentExistsAsync(MySqlConnection connection, long studentId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM education_students WHERE id = @id;";
        command.Parameters.AddWithValue("@id", studentId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is long count && count > 0;
    }

    private async Task PopulateEnrollmentsAsync(IList<EducationStudent> students, CancellationToken cancellationToken)
    {
        if (students.Count == 0)
        {
            return;
        }

        var ids = students.Select(s => s.Id).ToArray();
        var parameters = ids.Select((_, index) => $"@studentId{index}").ToArray();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $@"SELECT e.student_id,
                                         e.class_id,
                                         e.created_at,
                                         c.education_unit_id,
                                         c.name,
                                         u.name AS unit_name
                                  FROM education_student_enrollments e
                                  INNER JOIN education_classes c ON c.id = e.class_id
                                  INNER JOIN education_units u ON u.id = c.education_unit_id
                                  WHERE e.student_id IN ({string.Join(",", parameters)})
                                  ORDER BY u.name, c.name;";

        for (var i = 0; i < ids.Length; i++)
        {
            command.Parameters.AddWithValue(parameters[i], ids[i]);
        }

        var studentLookup = students.ToDictionary(s => s.Id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var enrollment = MapEnrollment(reader);
            if (studentLookup.TryGetValue(enrollment.StudentId, out var student))
            {
                student.Enrollments.Add(enrollment);
            }
        }
    }

    private static async Task<string> GenerateRegistrationCodeAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        while (true)
        {
            var yearPrefix = DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture);

            await using var lastCodeCommand = connection.CreateCommand();
            lastCodeCommand.CommandText = @"SELECT registration_code
                                            FROM education_students
                                            WHERE registration_code REGEXP '^[0-9]+$' AND registration_code LIKE @prefix
                                            ORDER BY registration_code DESC
                                            LIMIT 1;";
            lastCodeCommand.Parameters.AddWithValue("@prefix", $"{yearPrefix}%");

            var result = await lastCodeCommand.ExecuteScalarAsync(cancellationToken);

            long nextSequence = 1;
            if (result is string lastCode && lastCode.StartsWith(yearPrefix, StringComparison.Ordinal))
            {
                var suffix = lastCode[yearPrefix.Length..];
                if (long.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
                {
                    nextSequence = parsed + 1;
                }
            }

            var suffixText = nextSequence.ToString(CultureInfo.InvariantCulture);
            if (suffixText.Length < RegistrationSequenceDigits)
            {
                suffixText = suffixText.PadLeft(RegistrationSequenceDigits, '0');
            }

            var candidate = string.Concat(yearPrefix, suffixText);

            await using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT COUNT(*) FROM education_students WHERE registration_code = @code;";
            checkCommand.Parameters.AddWithValue("@code", candidate);

            var exists = await checkCommand.ExecuteScalarAsync(cancellationToken);
            if (exists is long total && total == 0)
            {
                return candidate;
            }
        }
    }

    public async Task<IEnumerable<EducationStudent>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureStudentTableAsync(connection, cancellationToken);
        await EnsureEnrollmentTableAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT s.id,
                                        s.name,
                                        s.registration_code,
                                        s.cpf,
                                        s.birth_date,
                                        s.guardian_name,
                                        s.guardian_contact,
                                        s.notes,
                                        s.created_at
                                 FROM education_students s
                                 ORDER BY s.name;";

        var students = new List<EducationStudent>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                students.Add(MapStudent(reader));
            }
        }

        await PopulateEnrollmentsAsync(students, cancellationToken);

        return students;
    }

    public async Task<EducationStudent?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureStudentTableAsync(connection, cancellationToken);
        await EnsureEnrollmentTableAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT s.id,
                                        s.name,
                                        s.registration_code,
                                        s.cpf,
                                        s.birth_date,
                                        s.guardian_name,
                                        s.guardian_contact,
                                        s.notes,
                                        s.created_at
                                 FROM education_students s
                                 WHERE s.id = @id;";
        command.Parameters.AddWithValue("@id", id);

        EducationStudent? student;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            student = MapStudent(reader);
        }

        if (student is null)
        {
            return null;
        }

        await PopulateEnrollmentsAsync(new List<EducationStudent> { student }, cancellationToken);
        return student;
    }

    public async Task<EducationStudent> CreateAsync(CreateEducationStudentRequest request, CancellationToken cancellationToken)
    {
        var (name, registrationCode, cpf, birthDate, guardianName, guardianContact, notes) = NormalizeStudent(
            request.Name,
            request.RegistrationCode,
            request.Cpf,
            request.BirthDate,
            request.GuardianName,
            request.GuardianContact,
            request.Notes);

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureStudentTableAsync(connection, cancellationToken);
        await EnsureEnrollmentTableAsync(connection, cancellationToken);

        var finalRegistrationCode = registrationCode;
        var inserted = false;
        long id = 0;

        while (!inserted)
        {
            finalRegistrationCode ??= await GenerateRegistrationCodeAsync(connection, cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO education_students (name, registration_code, cpf, birth_date, guardian_name, guardian_contact, notes)
                    VALUES (@name, @registrationCode, @cpf, @birthDate, @guardianName, @guardianContact, @notes);";
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@registrationCode", finalRegistrationCode);
            command.Parameters.AddWithValue("@cpf", cpf is null ? DBNull.Value : cpf);
            command.Parameters.AddWithValue("@birthDate", birthDate.HasValue ? birthDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value);
            command.Parameters.AddWithValue("@guardianName", guardianName is null ? DBNull.Value : guardianName);
            command.Parameters.AddWithValue("@guardianContact", guardianContact is null ? DBNull.Value : guardianContact);
            command.Parameters.AddWithValue("@notes", notes is null ? DBNull.Value : notes);

            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
                id = (long)command.LastInsertedId;
                inserted = true;
            }
            catch (MySqlException ex)
            {
                if (ex.Number == 1062 && registrationCode is null && IsRegistrationDuplicate(ex))
                {
                    finalRegistrationCode = null;
                    continue;
                }

                throw;
            }
        }

        var created = await GetByIdAsync(id, cancellationToken);
        return created ?? throw new InvalidOperationException("Falha ao recuperar o aluno criado.");
    }

    public async Task<EducationStudent?> UpdateAsync(long id, UpdateEducationStudentRequest request, CancellationToken cancellationToken)
    {
        var existing = await GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var (name, registrationCode, cpf, birthDate, guardianName, guardianContact, notes) = NormalizeStudent(
            request.Name,
            request.RegistrationCode,
            request.Cpf,
            request.BirthDate,
            request.GuardianName,
            request.GuardianContact,
            request.Notes);

        var finalRegistrationCode = registrationCode ?? existing.RegistrationCode;
        var cpfProvided = request.Cpf is not null;
        var finalCpf = cpfProvided ? cpf : existing.Cpf;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureStudentTableAsync(connection, cancellationToken);
        await EnsureEnrollmentTableAsync(connection, cancellationToken);

        if (finalRegistrationCode is null)
        {
            finalRegistrationCode = await GenerateRegistrationCodeAsync(connection, cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = @"UPDATE education_students
                                SET name = @name,
                                    registration_code = @registrationCode,
                                    cpf = @cpf,
                                    birth_date = @birthDate,
                                    guardian_name = @guardianName,
                                    guardian_contact = @guardianContact,
                                    notes = @notes
                                WHERE id = @id;";
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@registrationCode", finalRegistrationCode);
        command.Parameters.AddWithValue("@cpf", finalCpf is null ? DBNull.Value : finalCpf);
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

        await EnsureStudentTableAsync(connection, cancellationToken);
        await EnsureEnrollmentTableAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM education_students WHERE id = @id;";
        command.Parameters.AddWithValue("@id", id);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    public async Task<EducationStudent?> EnrollAsync(long studentId, long classId, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureStudentTableAsync(connection, cancellationToken);
        await EnsureEnrollmentTableAsync(connection, cancellationToken);
        await EnsureClassExistsAsync(connection, classId, cancellationToken);

        if (!await StudentExistsAsync(connection, studentId, cancellationToken))
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO education_student_enrollments (student_id, class_id)
                                VALUES (@studentId, @classId);";
        command.Parameters.AddWithValue("@studentId", studentId);
        command.Parameters.AddWithValue("@classId", classId);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            throw new InvalidOperationException("O aluno já está inscrito nesta turma.", ex);
        }

        return await GetByIdAsync(studentId, cancellationToken);
    }

    public async Task<EducationStudent?> UnenrollAsync(long studentId, long classId, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureStudentTableAsync(connection, cancellationToken);
        await EnsureEnrollmentTableAsync(connection, cancellationToken);

        if (!await StudentExistsAsync(connection, studentId, cancellationToken))
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM education_student_enrollments WHERE student_id = @studentId AND class_id = @classId;";
        command.Parameters.AddWithValue("@studentId", studentId);
        command.Parameters.AddWithValue("@classId", classId);

        await command.ExecuteNonQueryAsync(cancellationToken);

        return await GetByIdAsync(studentId, cancellationToken);
    }

    private static bool IsRegistrationDuplicate(MySqlException ex) =>
        ex.Message.Contains("registration_code", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("uq_education_students_registration", StringComparison.OrdinalIgnoreCase);
}
