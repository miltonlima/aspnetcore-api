using aspnetcore_api.Models;

namespace aspnetcore_api.Contracts
{
    public class UserProfileResponse
    {
        public long Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string BirthDate { get; init; } = string.Empty;
        public string Cpf { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string Theme { get; init; } = "dark";
        public DateTime CreatedAt { get; init; }

        public static UserProfileResponse FromEntity(PersonRegistration entity) => new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Email = entity.Email,
            BirthDate = entity.BirthDate.ToString("yyyy-MM-dd"),
            Cpf = entity.Cpf,
            Description = entity.Description,
            Theme = string.IsNullOrWhiteSpace(entity.Theme) ? "dark" : entity.Theme,
            CreatedAt = entity.CreatedAt
        };
    }
}
