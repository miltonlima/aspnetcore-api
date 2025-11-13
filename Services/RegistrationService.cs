using aspnetcore_api.Contracts;
using aspnetcore_api.Models;
using MySqlConnector;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace aspnetcore_api.Services;

public class RegistrationService
{
    private readonly string _connectionString;

    public RegistrationService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    public async Task<PersonRegistration> CreateAsync(RegistrationRequest request, CancellationToken cancellationToken)
    {
        var person = ValidateAndMap(request);

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureTableAsync(connection, cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = @"INSERT INTO person_registrations (name, birth_date, cpf, email, description, created_at)
                                    VALUES (@name, @birthDate, @cpf, @email, @description, @createdAt);";
            command.Parameters.AddWithValue("@name", person.Name);
            command.Parameters.AddWithValue("@birthDate", person.BirthDate.ToDateTime(TimeOnly.MinValue));
            command.Parameters.AddWithValue("@cpf", person.Cpf);
            command.Parameters.AddWithValue("@email", person.Email);
            command.Parameters.AddWithValue("@description", person.Description is null ? DBNull.Value : person.Description);
            command.Parameters.AddWithValue("@createdAt", person.CreatedAt);

            await command.ExecuteNonQueryAsync(cancellationToken);
            person.Id = command.LastInsertedId;
        }

        return person;
    }

    public async Task<IReadOnlyList<PersonRegistration>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureTableAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT id, name, birth_date, cpf, email, description, created_at
                                FROM person_registrations
                                ORDER BY created_at DESC;";

        var registrations = new List<PersonRegistration>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            registrations.Add(MapReader(reader));
        }

        return registrations;
    }

    private static PersonRegistration ValidateAndMap(RegistrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Nome é obrigatório.", nameof(request.Name));
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ArgumentException("E-mail é obrigatório.", nameof(request.Email));
        }

        if (request.BirthDate == default)
        {
            throw new ArgumentException("Data de nascimento é obrigatória.", nameof(request.BirthDate));
        }

        var sanitizedCpf = SanitizeCpf(request.Cpf);
        if (sanitizedCpf.Length != 11)
        {
            throw new ArgumentException("CPF deve conter 11 dígitos.", nameof(request.Cpf));
        }

        if (!IsValidEmail(request.Email))
        {
            throw new ArgumentException("E-mail inválido.", nameof(request.Email));
        }

        var description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();

        if (description is { Length: > 1000 })
        {
            throw new ArgumentException("Descrição deve conter no máximo 1000 caracteres.", nameof(request.Description));
        }

        return new PersonRegistration
        {
            Name = request.Name.Trim(),
            BirthDate = request.BirthDate,
            Cpf = sanitizedCpf,
            Email = request.Email.Trim(),
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var _ = new MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string SanitizeCpf(string cpf) =>
        Regex.Replace(cpf ?? string.Empty, "[^0-9]", string.Empty);

    public async Task<PersonRegistration?> UpdateDescriptionAsync(long id, string? description, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureTableAsync(connection, cancellationToken);

        var normalizedDescription = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();

        if (normalizedDescription is { Length: > 1000 })
        {
            throw new ArgumentException("Descrição deve conter no máximo 1000 caracteres.", nameof(description));
        }

        await using (var update = connection.CreateCommand())
        {
            update.CommandText = @"UPDATE person_registrations
                                    SET description = @description
                                    WHERE id = @id;";
            update.Parameters.AddWithValue("@description", normalizedDescription is null ? DBNull.Value : normalizedDescription);
            update.Parameters.AddWithValue("@id", id);

            var affected = await update.ExecuteNonQueryAsync(cancellationToken);
            if (affected == 0)
            {
                return null;
            }
        }

        await using var fetch = connection.CreateCommand();
        fetch.CommandText = @"SELECT id, name, birth_date, cpf, email, description, created_at
                                FROM person_registrations
                                WHERE id = @id;";
        fetch.Parameters.AddWithValue("@id", id);

        await using var reader = await fetch.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapReader(reader);
        }

        return null;
    }

    public async Task<PersonRegistration?> UpdateAsync(long id, Contracts.UpdateRegistrationRequest request, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureTableAsync(connection, cancellationToken);

        // Fetch existing
        await using var fetch = connection.CreateCommand();
        fetch.CommandText = @"SELECT id, name, birth_date, cpf, email, description, created_at
                                FROM person_registrations
                                WHERE id = @id;";
        fetch.Parameters.AddWithValue("@id", id);

        PersonRegistration existing;
        await using (var reader = await fetch.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            existing = MapReader(reader);
        }

        // Determine new values (use provided or keep existing)
        var newName = request.Name is null ? existing.Name : request.Name.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new ArgumentException("Nome é obrigatório.", nameof(request.Name));
        }

        var newBirthDate = request.BirthDate ?? existing.BirthDate;
        if (newBirthDate == default)
        {
            throw new ArgumentException("Data de nascimento é obrigatória.", nameof(request.BirthDate));
        }

        var newCpf = request.Cpf is null ? existing.Cpf : SanitizeCpf(request.Cpf);
        if (newCpf.Length != 11)
        {
            throw new ArgumentException("CPF deve conter 11 dígitos.", nameof(request.Cpf));
        }

        var newEmail = request.Email is null ? existing.Email : request.Email.Trim();
        if (string.IsNullOrWhiteSpace(newEmail))
        {
            throw new ArgumentException("E-mail é obrigatório.", nameof(request.Email));
        }

        if (!IsValidEmail(newEmail))
        {
            throw new ArgumentException("E-mail inválido.", nameof(request.Email));
        }

        var normalizedDescription = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();

        if (normalizedDescription is { Length: > 1000 })
        {
            throw new ArgumentException("Descrição deve conter no máximo 1000 caracteres.", nameof(request.Description));
        }

        // Update
        await using (var update = connection.CreateCommand())
        {
            update.CommandText = @"UPDATE person_registrations
                                    SET name = @name,
                                        birth_date = @birthDate,
                                        cpf = @cpf,
                                        email = @email,
                                        description = @description
                                    WHERE id = @id;";

            update.Parameters.AddWithValue("@name", newName);
            update.Parameters.AddWithValue("@birthDate", newBirthDate.ToDateTime(TimeOnly.MinValue));
            update.Parameters.AddWithValue("@cpf", newCpf);
            update.Parameters.AddWithValue("@email", newEmail);
            update.Parameters.AddWithValue("@description", normalizedDescription is null ? DBNull.Value : normalizedDescription);
            update.Parameters.AddWithValue("@id", id);

            var affected = await update.ExecuteNonQueryAsync(cancellationToken);
            if (affected == 0)
            {
                return null;
            }
        }

        // Fetch updated
        await using var fetchUpdated = connection.CreateCommand();
        fetchUpdated.CommandText = @"SELECT id, name, birth_date, cpf, email, description, created_at
                                FROM person_registrations
                                WHERE id = @id;";
        fetchUpdated.Parameters.AddWithValue("@id", id);

        await using var reader2 = await fetchUpdated.ExecuteReaderAsync(cancellationToken);
        if (await reader2.ReadAsync(cancellationToken))
        {
            return MapReader(reader2);
        }

        return null;
    }

    private static async Task EnsureTableAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"CREATE TABLE IF NOT EXISTS person_registrations (
                id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                name VARCHAR(150) NOT NULL,
                birth_date DATE NOT NULL,
                cpf CHAR(11) NOT NULL,
                email VARCHAR(180) NOT NULL,
                description TEXT NULL,
                created_at DATETIME NOT NULL,
                UNIQUE KEY uq_person_registrations_cpf (cpf),
                UNIQUE KEY uq_person_registrations_email (email)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

        await command.ExecuteNonQueryAsync(cancellationToken);

        command.CommandText = "ALTER TABLE person_registrations ADD COLUMN IF NOT EXISTS description TEXT NULL AFTER email;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static PersonRegistration MapReader(MySqlDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Name = reader.GetString(1),
        BirthDate = DateOnly.FromDateTime(reader.GetDateTime(2)),
        Cpf = reader.GetString(3),
        Email = reader.GetString(4),
        Description = reader.IsDBNull(5) ? null : reader.GetString(5),
        CreatedAt = reader.GetDateTime(6)
    };
}
