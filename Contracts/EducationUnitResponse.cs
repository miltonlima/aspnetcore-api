using aspnetcore_api.Models;

namespace aspnetcore_api.Contracts;

public class EducationUnitResponse
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? City { get; init; }
    public string? State { get; init; }
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }

    public static EducationUnitResponse FromEntity(EducationUnit entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Code = entity.Code,
        City = entity.City,
        State = entity.State,
        Description = entity.Description,
        CreatedAt = entity.CreatedAt
    };
}
