using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace aspnetcore_api.Services;

public class RequestLogService
{
    private const int MaxMethodLength = 16;
    private const int MaxPathLength = 512;
    private const int MaxEmailLength = 320;
    private const int MaxIpLength = 128;
    private readonly string _connectionString;
    private readonly ILogger<RequestLogService> _logger;
    private static volatile bool _tableEnsured;
    private static readonly SemaphoreSlim TableSemaphore = new(1, 1);

    public RequestLogService(IConfiguration configuration, ILogger<RequestLogService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        _logger = logger;
    }

    public async Task LogRequestAsync(HttpContext context, TimeSpan duration, CancellationToken cancellationToken)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await EnsureTableExistsAsync(connection, cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO request_logs (
                                        user_id,
                                        user_email,
                                        is_authenticated,
                                        method,
                                        path,
                                        query_string,
                                        status_code,
                                        ip_address,
                                        user_agent,
                                        duration_ms
                                    ) VALUES (
                                        @user_id,
                                        @user_email,
                                        @is_authenticated,
                                        @method,
                                        @path,
                                        @query_string,
                                        @status_code,
                                        @ip_address,
                                        @user_agent,
                                        @duration_ms
                                    );";

            var (userId, userEmail, isAuthenticated) = ExtractUser(context.User);

            command.Parameters.AddWithValue("@user_id", userId.HasValue ? userId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@user_email", string.IsNullOrWhiteSpace(userEmail) ? DBNull.Value : Truncate(userEmail, MaxEmailLength)!);
            command.Parameters.AddWithValue("@is_authenticated", isAuthenticated);
            command.Parameters.AddWithValue("@method", Truncate(context.Request.Method, MaxMethodLength) ?? string.Empty);
            command.Parameters.AddWithValue("@path", Truncate(context.Request.Path.Value, MaxPathLength) ?? string.Empty);
            command.Parameters.AddWithValue("@query_string", string.IsNullOrWhiteSpace(context.Request.QueryString.Value) ? DBNull.Value : context.Request.QueryString.Value);
            command.Parameters.AddWithValue("@status_code", context.Response.StatusCode);
            var ipAddress = GetIpAddress(context);
            command.Parameters.AddWithValue("@ip_address", string.IsNullOrWhiteSpace(ipAddress) ? DBNull.Value : Truncate(ipAddress, MaxIpLength)!);
            var userAgent = context.Request.Headers["User-Agent"].ToString();
            command.Parameters.AddWithValue("@user_agent", string.IsNullOrWhiteSpace(userAgent) ? DBNull.Value : userAgent);
            command.Parameters.AddWithValue("@duration_ms", Math.Max(0, (long)Math.Round(duration.TotalMilliseconds)));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Request logging cancelled for {Path}.", context.Request.Path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist request log for {Method} {Path}", context.Request.Method, context.Request.Path);
        }
    }

    private static (long? UserId, string? Email, bool IsAuthenticated) ExtractUser(ClaimsPrincipal principal)
    {
        if (principal?.Identity is not { IsAuthenticated: true })
        {
            return (null, null, false);
        }

        long? userId = null;
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (long.TryParse(userIdValue, out var parsedId))
        {
            userId = parsedId;
        }

        var email = principal.FindFirstValue(ClaimTypes.Email);
        return (userId, email, true);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string? GetIpAddress(HttpContext context)
    {
        const string forwardedHeader = "X-Forwarded-For";
        if (context.Request.Headers.TryGetValue(forwardedHeader, out var forwardedValue) && !string.IsNullOrWhiteSpace(forwardedValue))
        {
            return forwardedValue.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    private static async Task EnsureTableExistsAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        if (_tableEnsured)
        {
            return;
        }

        await TableSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_tableEnsured)
            {
                return;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = @"CREATE TABLE IF NOT EXISTS request_logs (
                                        id BIGINT PRIMARY KEY AUTO_INCREMENT,
                                        user_id BIGINT NULL,
                                        user_email VARCHAR(320) NULL,
                                        is_authenticated TINYINT(1) NOT NULL,
                                        method VARCHAR(16) NOT NULL,
                                        path VARCHAR(512) NOT NULL,
                                        query_string TEXT NULL,
                                        status_code INT NOT NULL,
                                        ip_address VARCHAR(128) NULL,
                                        user_agent TEXT NULL,
                                        duration_ms BIGINT NOT NULL,
                                        created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                        INDEX idx_request_logs_created_at (created_at),
                                        INDEX idx_request_logs_user_id (user_id)
                                    );";
            await command.ExecuteNonQueryAsync(cancellationToken);
            _tableEnsured = true;
        }
        finally
        {
            TableSemaphore.Release();
        }
    }
}
