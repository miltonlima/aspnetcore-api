namespace aspnetcore_api.Contracts;

public record UpdateRegistrationRequest(string? Name, DateOnly? BirthDate, string? Cpf, string? Email, string? Description);
