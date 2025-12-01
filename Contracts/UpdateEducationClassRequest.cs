using System;

namespace aspnetcore_api.Contracts;

public class UpdateEducationClassRequest
{
    public long EducationUnitId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? AcademicYear { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? ScheduleTime { get; set; }
    public int? Capacity { get; set; }
    public string? Description { get; set; }
}
