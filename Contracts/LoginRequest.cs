namespace aspnetcore_api.Contracts
{
    public class LoginRequest
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string Password { get; set; } = string.Empty;
    }
}
