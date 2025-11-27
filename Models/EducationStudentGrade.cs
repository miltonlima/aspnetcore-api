namespace aspnetcore_api.Models;

public class EducationStudentGrade
{
    public long StudentId { get; set; }
    public long EducationClassId { get; set; }
    public decimal? Av1 { get; set; }
    public decimal? Av2 { get; set; }
    public decimal? Av3 { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
