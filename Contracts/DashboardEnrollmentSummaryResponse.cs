using System;
using System.Collections.Generic;

namespace aspnetcore_api.Contracts;

public class DashboardClassEnrollmentResponse
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public long TotalStudents { get; init; }
}

public class DashboardUnitEnrollmentResponse
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public long TotalStudents { get; init; }
    public IReadOnlyList<DashboardClassEnrollmentResponse> Classes { get; init; } = Array.Empty<DashboardClassEnrollmentResponse>();
}

public class DashboardEnrollmentSummaryResponse
{
    public DateTime GeneratedAt { get; init; }
    public long TotalStudents { get; init; }
    public IReadOnlyList<DashboardUnitEnrollmentResponse> Units { get; init; } = Array.Empty<DashboardUnitEnrollmentResponse>();

    public static DashboardEnrollmentSummaryResponse CreateEmpty() => new()
    {
        GeneratedAt = DateTime.UtcNow,
        TotalStudents = 0,
        Units = Array.Empty<DashboardUnitEnrollmentResponse>()
    };
}
