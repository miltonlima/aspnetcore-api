using aspnetcore_api.Models;

namespace aspnetcore_api.Contracts;

public class EducationStudentEnrollmentResponse
{
    public long EducationClassId { get; init; }
    public string EducationClassName { get; init; } = string.Empty;
    public long EducationUnitId { get; init; }
    public string EducationUnitName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }

    public static EducationStudentEnrollmentResponse FromEntity(EducationStudentEnrollment entity) => new()
    {
        EducationClassId = entity.EducationClassId,
        EducationClassName = entity.EducationClassName ?? string.Empty,
        EducationUnitId = entity.EducationUnitId,
        EducationUnitName = entity.EducationUnitName ?? string.Empty,
        CreatedAt = entity.CreatedAt
    };
}
