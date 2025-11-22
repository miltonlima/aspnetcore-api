using aspnetcore_api.Models;

namespace aspnetcore_api.Contracts;

public class EducationStudentResponse
{
    public long Id { get; init; }
    public long EducationClassId { get; init; }
    public string EducationClassName { get; init; } = string.Empty;
    public string EducationUnitName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? RegistrationCode { get; init; }
    public string? BirthDate { get; init; }
    public string? GuardianName { get; init; }
    public string? GuardianContact { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }

    public static EducationStudentResponse FromEntity(EducationStudent entity) => new()
    {
        Id = entity.Id,
        EducationClassId = entity.EducationClassId,
        EducationClassName = entity.ClassName ?? string.Empty,
        EducationUnitName = entity.EducationUnitName ?? string.Empty,
        Name = entity.Name,
        RegistrationCode = entity.RegistrationCode,
        BirthDate = entity.BirthDate?.ToString("yyyy-MM-dd"),
        GuardianName = entity.GuardianName,
        GuardianContact = entity.GuardianContact,
        Notes = entity.Notes,
        CreatedAt = entity.CreatedAt
    };
}
