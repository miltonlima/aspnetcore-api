using System;

namespace aspnetcore_api.Models;

public class RequestLog
{
    public long Id { get; set; }
    public long? UserId { get; set; }
    public string? UserEmail { get; set; }
    public bool IsAuthenticated { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? QueryString { get; set; }
    public string? Action { get; set; }
    public string? Description { get; set; }
    public int StatusCode { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public long DurationMilliseconds { get; set; }
    public DateTime CreatedAt { get; set; }
}
