using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using aspnetcore_api.Contracts;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace aspnetcore_api.Services;

public class DashboardService
{
    private readonly string _connectionString;

    public DashboardService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    public async Task<DashboardEnrollmentSummaryResponse> GetEnrollmentSummaryAsync(CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        if (!await TableExistsAsync(connection, "education_units", cancellationToken))
        {
            return DashboardEnrollmentSummaryResponse.CreateEmpty();
        }

        var units = await LoadUnitsAsync(connection, cancellationToken);
        if (units.Count == 0)
        {
            return DashboardEnrollmentSummaryResponse.CreateEmpty();
        }

        var classesByUnit = await LoadClassesByUnitAsync(connection, cancellationToken);
        var enrollmentCounts = await LoadEnrollmentCountsAsync(connection, cancellationToken);

        var unitResponses = new List<DashboardUnitEnrollmentResponse>(units.Count);
        long overallTotal = 0;

        foreach (var unit in units.OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase))
        {
            var classResponses = new List<DashboardClassEnrollmentResponse>();
            if (classesByUnit.TryGetValue(unit.Id, out var classInfos))
            {
                foreach (var classInfo in classInfos.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var count = enrollmentCounts.TryGetValue(classInfo.Id, out var total) ? total : 0;
                    classResponses.Add(new DashboardClassEnrollmentResponse
                    {
                        Id = classInfo.Id,
                        Name = classInfo.Name,
                        TotalStudents = count
                    });
                }
            }

            var unitTotal = classResponses.Sum(c => c.TotalStudents);
            overallTotal += unitTotal;

            unitResponses.Add(new DashboardUnitEnrollmentResponse
            {
                Id = unit.Id,
                Name = unit.Name,
                TotalStudents = unitTotal,
                Classes = classResponses.ToArray()
            });
        }

        return new DashboardEnrollmentSummaryResponse
        {
            GeneratedAt = DateTime.UtcNow,
            TotalStudents = overallTotal,
            Units = unitResponses.ToArray()
        };
    }

    private static async Task<bool> TableExistsAsync(MySqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT COUNT(*)
                                 FROM information_schema.TABLES
                                 WHERE TABLE_SCHEMA = DATABASE()
                                   AND TABLE_NAME = @tableName;";
        command.Parameters.AddWithValue("@tableName", tableName);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is long count && count > 0;
    }

    private static async Task<IReadOnlyList<UnitRecord>> LoadUnitsAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        var units = new List<UnitRecord>();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name FROM education_units ORDER BY name;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            units.Add(new UnitRecord(reader.GetInt64(0), reader.GetString(1)));
        }

        return units;
    }

    private async Task<Dictionary<long, List<ClassRecord>>> LoadClassesByUnitAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "education_classes", cancellationToken))
        {
            return new Dictionary<long, List<ClassRecord>>();
        }

        var classesByUnit = new Dictionary<long, List<ClassRecord>>();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, education_unit_id FROM education_classes ORDER BY name;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var classRecord = new ClassRecord(reader.GetInt64(0), reader.GetString(1), reader.GetInt64(2));
            if (!classesByUnit.TryGetValue(classRecord.UnitId, out var list))
            {
                list = new List<ClassRecord>();
                classesByUnit[classRecord.UnitId] = list;
            }

            list.Add(classRecord);
        }

        return classesByUnit;
    }

    private async Task<Dictionary<long, long>> LoadEnrollmentCountsAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "education_student_enrollments", cancellationToken))
        {
            return new Dictionary<long, long>();
        }

        var counts = new Dictionary<long, long>();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT class_id, COUNT(*) FROM education_student_enrollments GROUP BY class_id;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var classId = reader.GetInt64(0);
            var total = reader.GetInt64(1);
            counts[classId] = total;
        }

        return counts;
    }

    private readonly struct UnitRecord
    {
        public UnitRecord(long id, string name)
        {
            Id = id;
            Name = name;
        }

        public long Id { get; }
        public string Name { get; }
    }

    private readonly struct ClassRecord
    {
        public ClassRecord(long id, string name, long unitId)
        {
            Id = id;
            Name = name;
            UnitId = unitId;
        }

        public long Id { get; }
        public string Name { get; }
        public long UnitId { get; }
    }
}
