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
            command.CommandText = @"INSERT INTO person_registrations (name, birth_date, cpf, email, created_at)
                                    VALUES (@name, @birthDate, @cpf, @email, @createdAt);";
            command.Parameters.AddWithValue("@name", person.Name);
            command.Parameters.AddWithValue("@birthDate", person.BirthDate.ToDateTime(TimeOnly.MinValue));
            command.Parameters.AddWithValue("@cpf", person.Cpf);
            command.Parameters.AddWithValue("@email", person.Email);
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
        command.CommandText = @"SELECT id, name, birth_date, cpf, email, created_at
                                FROM person_registrations
                                ORDER BY created_at DESC;";

        var registrations = new List<PersonRegistration>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            registrations.Add(new PersonRegistration
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                BirthDate = DateOnly.FromDateTime(reader.GetDateTime(2)),
                Cpf = reader.GetString(3),
                Email = reader.GetString(4),
                CreatedAt = reader.GetDateTime(5)
            });
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

        return new PersonRegistration
        {
            Name = request.Name.Trim(),
            BirthDate = request.BirthDate,
            Cpf = sanitizedCpf,
            Email = request.Email.Trim(),
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

    private static async Task EnsureTableAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"CREATE TABLE IF NOT EXISTS person_registrations (
                id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                name VARCHAR(150) NOT NULL,
                birth_date DATE NOT NULL,
                cpf CHAR(11) NOT NULL,
                email VARCHAR(180) NOT NULL,
                created_at DATETIME NOT NULL,
                UNIQUE KEY uq_person_registrations_cpf (cpf),
                UNIQUE KEY uq_person_registrations_email (email)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
