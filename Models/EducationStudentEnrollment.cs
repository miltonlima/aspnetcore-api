namespace aspnetcore_api.Models;

public class EducationStudentEnrollment
{
    public long StudentId { get; set; }
    public long EducationClassId { get; set; }
    public long EducationUnitId { get; set; }
    public string? EducationClassName { get; set; }
    public string? EducationUnitName { get; set; }
    public DateTime CreatedAt { get; set; }
}
