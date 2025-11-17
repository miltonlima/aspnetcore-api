namespace aspnetcore_api.Contracts;

public record AuthRequest(string Email, string Password);
public record AuthResponse(string Email, string? Token);
