namespace aspnetcore_api.Models;

public class EducationStudent
{
    public long Id { get; set; }
    public long EducationClassId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? RegistrationCode { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianContact { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ClassName { get; set; }
    public string? EducationUnitName { get; set; }
}
