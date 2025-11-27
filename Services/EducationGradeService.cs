using aspnetcore_api.Contracts;
using MySqlConnector;

namespace aspnetcore_api.Services;

public class EducationGradeService
{
    private readonly string _connectionString;

    public EducationGradeService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    public async Task<IReadOnlyList<EducationStudentGradeResponse>> GetGradesForClassAsync(long classId, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureGradeTableAsync(connection, cancellationToken);
        await EnsureClassExistsAsync(connection, classId, cancellationToken);

        var results = new List<EducationStudentGradeResponse>();

        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT s.id,
                                      s.name,
                                      s.registration_code,
                                      g.av1,
                                      g.av2,
                                      g.av3,
                                      g.updated_at
                               FROM education_student_enrollments e
                               INNER JOIN education_students s ON s.id = e.student_id
                               LEFT JOIN education_student_grades g ON g.student_id = e.student_id AND g.class_id = e.class_id
                               WHERE e.class_id = @classId
                               ORDER BY s.name;";
        command.Parameters.AddWithValue("@classId", classId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var studentId = reader.GetInt64(0);
            var studentName = reader.GetString(1);
            var registrationCode = reader.IsDBNull(2) ? null : reader.GetString(2);
            var av1 = reader.IsDBNull(3) ? (decimal?)null : reader.GetDecimal(3);
            var av2 = reader.IsDBNull(4) ? (decimal?)null : reader.GetDecimal(4);
            var av3 = reader.IsDBNull(5) ? (decimal?)null : reader.GetDecimal(5);
            var updatedAt = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6);

            results.Add(EducationStudentGradeResponse.FromTuple(studentId, studentName, registrationCode, classId, av1, av2, av3, updatedAt));
        }

        return results;
    }

    public async Task<EducationStudentGradeResponse?> UpsertGradeAsync(long classId, long studentId, UpdateEducationStudentGradeRequest request, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureGradeTableAsync(connection, cancellationToken);
        await EnsureClassExistsAsync(connection, classId, cancellationToken);
        await EnsureEnrollmentExistsAsync(connection, classId, studentId, cancellationToken);

        var (av1, av2, av3) = NormalizeGrades(request.Av1, request.Av2, request.Av3);

        await using var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO education_student_grades (student_id, class_id, av1, av2, av3)
                                VALUES (@studentId, @classId, @av1, @av2, @av3)
                                ON DUPLICATE KEY UPDATE
                                    av1 = VALUES(av1),
                                    av2 = VALUES(av2),
                                    av3 = VALUES(av3),
                                    updated_at = CURRENT_TIMESTAMP;";
        command.Parameters.AddWithValue("@studentId", studentId);
        command.Parameters.AddWithValue("@classId", classId);
        command.Parameters.AddWithValue("@av1", av1.HasValue ? av1.Value : DBNull.Value);
        command.Parameters.AddWithValue("@av2", av2.HasValue ? av2.Value : DBNull.Value);
        command.Parameters.AddWithValue("@av3", av3.HasValue ? av3.Value : DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);

        return await GetGradeAsync(connection, classId, studentId, cancellationToken);
    }

    private static (decimal? Av1, decimal? Av2, decimal? Av3) NormalizeGrades(decimal? av1, decimal? av2, decimal? av3)
    {
        return (NormalizeGrade(av1, nameof(av1)), NormalizeGrade(av2, nameof(av2)), NormalizeGrade(av3, nameof(av3)));
    }

    private static decimal? NormalizeGrade(decimal? value, string fieldName)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var rounded = Math.Round(value.Value, 2, MidpointRounding.AwayFromZero);
        if (rounded < 0 || rounded > 10)
        {
            throw new ArgumentException($"{fieldName.ToUpperInvariant()} deve ser um valor entre 0 e 10.");
        }

        return rounded;
    }

    private static async Task EnsureGradeTableAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"CREATE TABLE IF NOT EXISTS education_student_grades (
                                    id BIGINT PRIMARY KEY AUTO_INCREMENT,
                                    student_id BIGINT NOT NULL,
                                    class_id BIGINT NOT NULL,
                                    av1 DECIMAL(5,2) NULL,
                                    av2 DECIMAL(5,2) NULL,
                                    av3 DECIMAL(5,2) NULL,
                                    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                                    UNIQUE KEY uq_student_class (student_id, class_id),
                                    CONSTRAINT fk_grade_student FOREIGN KEY (student_id) REFERENCES education_students(id) ON DELETE CASCADE,
                                    CONSTRAINT fk_grade_class FOREIGN KEY (class_id) REFERENCES education_classes(id) ON DELETE CASCADE
                                );";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureClassExistsAsync(MySqlConnection connection, long classId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM education_classes WHERE id = @classId;";
        command.Parameters.AddWithValue("@classId", classId);

        var exists = await command.ExecuteScalarAsync(cancellationToken);
        if (exists is not long count || count == 0)
        {
            throw new ArgumentException("Turma informada não existe.");
        }
    }

    private static async Task EnsureEnrollmentExistsAsync(MySqlConnection connection, long classId, long studentId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT COUNT(*)
                                 FROM education_student_enrollments
                                 WHERE class_id = @classId AND student_id = @studentId;";
        command.Parameters.AddWithValue("@classId", classId);
        command.Parameters.AddWithValue("@studentId", studentId);

        var exists = await command.ExecuteScalarAsync(cancellationToken);
        if (exists is not long count || count == 0)
        {
            throw new ArgumentException("Aluno não está matriculado na turma informada.");
        }
    }

    private static async Task<EducationStudentGradeResponse?> GetGradeAsync(MySqlConnection connection, long classId, long studentId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT s.id,
                                      s.name,
                                      s.registration_code,
                                      g.av1,
                                      g.av2,
                                      g.av3,
                                      g.updated_at
                               FROM education_student_enrollments e
                               INNER JOIN education_students s ON s.id = e.student_id
                               LEFT JOIN education_student_grades g ON g.student_id = e.student_id AND g.class_id = e.class_id
                               WHERE e.class_id = @classId AND e.student_id = @studentId
                               LIMIT 1;";
        command.Parameters.AddWithValue("@classId", classId);
        command.Parameters.AddWithValue("@studentId", studentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var studentName = reader.GetString(1);
            var registrationCode = reader.IsDBNull(2) ? null : reader.GetString(2);
            var av1 = reader.IsDBNull(3) ? (decimal?)null : reader.GetDecimal(3);
            var av2 = reader.IsDBNull(4) ? (decimal?)null : reader.GetDecimal(4);
            var av3 = reader.IsDBNull(5) ? (decimal?)null : reader.GetDecimal(5);
            var updatedAt = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6);

            return EducationStudentGradeResponse.FromTuple(studentId, studentName, registrationCode, classId, av1, av2, av3, updatedAt);
        }

        return null;
    }
}
