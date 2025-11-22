namespace aspnetcore_api.Models;

public class EducationUnit
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}
