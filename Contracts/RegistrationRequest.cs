namespace aspnetcore_api.Contracts;

public record RegistrationRequest(string Name, DateOnly BirthDate, string Cpf, string Email, string? Description);
