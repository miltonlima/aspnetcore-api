using aspnetcore_api.Models;

namespace aspnetcore_api.Contracts;

public record RegistrationResponse(long Id, string Name, DateOnly BirthDate, string Cpf, string Email, string? Description, DateTime CreatedAt)
{
    public static RegistrationResponse FromEntity(PersonRegistration entity) =>
        new(entity.Id, entity.Name, entity.BirthDate, entity.Cpf, entity.Email, entity.Description, entity.CreatedAt);
}
