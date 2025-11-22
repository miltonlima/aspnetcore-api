namespace aspnetcore_api.Contracts;

public class CreateEducationUnitRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Description { get; set; }
}
