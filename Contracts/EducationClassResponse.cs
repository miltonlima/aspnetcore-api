using aspnetcore_api.Models;

namespace aspnetcore_api.Contracts;

public class EducationClassResponse
{
    public long Id { get; init; }
    public long EducationUnitId { get; init; }
    public string EducationUnitName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string? AcademicYear { get; init; }
    public int? Capacity { get; init; }
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }

    public static EducationClassResponse FromEntity(EducationClass entity) => new()
    {
        Id = entity.Id,
        EducationUnitId = entity.EducationUnitId,
        EducationUnitName = entity.EducationUnitName ?? string.Empty,
        Name = entity.Name,
        Code = entity.Code,
        AcademicYear = entity.AcademicYear,
        Capacity = entity.Capacity,
        Description = entity.Description,
        CreatedAt = entity.CreatedAt
    };
}
