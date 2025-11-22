namespace aspnetcore_api.Models;

public class EducationClass
{
    public long Id { get; set; }
    public long EducationUnitId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? AcademicYear { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? EducationUnitName { get; set; }
}
