namespace aspnetcore_api.Contracts
{
    public class UpdateUserProfileRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? BirthDate { get; set; }
        public string? Cpf { get; set; }
        public string? Description { get; set; }
    }
}
