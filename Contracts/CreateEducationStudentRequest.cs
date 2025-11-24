namespace aspnetcore_api.Contracts;

public class CreateEducationStudentRequest
{
    public string Name { get; set; } = string.Empty;
    public string? RegistrationCode { get; set; }
    public string? Cpf { get; set; }
    public string? BirthDate { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianContact { get; set; }
    public string? Notes { get; set; }
}
