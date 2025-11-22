namespace aspnetcore_api.Contracts
{
    public class UpdatePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
