using System.Collections.Generic;
using System.Linq;

namespace aspnetcore_api.Contracts;

public class EducationStudentGradeResponse
{
    public long StudentId { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public string? RegistrationCode { get; init; }
    public long EducationClassId { get; init; }
    public decimal? Av1 { get; init; }
    public decimal? Av2 { get; init; }
    public decimal? Av3 { get; init; }
    public decimal? FinalAverage { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public static EducationStudentGradeResponse FromTuple(
        long studentId,
        string studentName,
        string? registrationCode,
        long classId,
        decimal? av1,
        decimal? av2,
        decimal? av3,
        DateTime? updatedAt)
    {
        return new EducationStudentGradeResponse
        {
            StudentId = studentId,
            StudentName = studentName,
            RegistrationCode = registrationCode,
            EducationClassId = classId,
            Av1 = av1,
            Av2 = av2,
            Av3 = av3,
            FinalAverage = CalculateAverage(av1, av2, av3),
            UpdatedAt = updatedAt
        };
    }

    private static decimal? CalculateAverage(decimal? av1, decimal? av2, decimal? av3)
    {
        var grades = new List<decimal>();
        if (av1.HasValue)
        {
            grades.Add(av1.Value);
        }
        if (av2.HasValue)
        {
            grades.Add(av2.Value);
        }
        if (av3.HasValue)
        {
            grades.Add(av3.Value);
        }

        if (grades.Count == 0)
        {
            return null;
        }

        var average = grades.Average();
        return Math.Round(average, 2, MidpointRounding.AwayFromZero);
    }
}
