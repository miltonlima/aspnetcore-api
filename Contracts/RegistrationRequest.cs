using System.Text.Json.Serialization;

namespace aspnetcore_api.Contracts;

public record RegistrationRequest
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("birthDate")]
    public string BirthDate { get; init; } = string.Empty;

    [JsonPropertyName("cpf")]
    public string Cpf { get; init; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("password")]
    public string Password { get; init; } = string.Empty;
}
