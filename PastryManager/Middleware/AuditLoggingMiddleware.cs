using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PastryManager.Infrastructure.Services.Audit;

namespace PastryManager.Middleware.Security;

/// <summary>
/// Audit logging middleware to capture all HTTP requests
/// Part of compliance requirements for financial applications
/// </summary>
public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuditService auditService)
    {
        var startTime = DateTime.UtcNow;
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = context.Request.Headers["User-Agent"].ToString();
        var userId = context.User?.Identity?.Name ?? "anonymous";
        var method = context.Request.Method;
        var path = context.Request.Path;

        try
        {
            await _next(context);
        }
        finally
        {
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var statusCode = context.Response.StatusCode;

            // Log to audit service for sensitive operations
            if (IsSensitiveOperation(method, path))
            {
                var metadata = new Dictionary<string, object>
                {
                    ["method"] = method,
                    ["path"] = path,
                    ["statusCode"] = statusCode,
                    ["duration"] = duration
                };

                await auditService.LogActionAsync(
                    userId,
                    $"{method} {path}",
                    "HttpRequest",
                    Guid.NewGuid().ToString(),
                    null,
                    null,
                    clientIp,
                    userAgent,
                    metadata);
            }

            _logger.LogInformation(
                "HTTP {Method} {Path} completed with status {StatusCode} in {Duration}ms for IP {ClientIp}",
                method, path, statusCode, duration, clientIp);
        }
    }

    private static bool IsSensitiveOperation(string method, string path)
    {
        // Log all POST, PUT, DELETE, PATCH operations
        if (method is "POST" or "PUT" or "DELETE" or "PATCH")
            return true;

        // Log access to sensitive endpoints
        var sensitivePaths = new[] { "/api/accounts", "/api/transactions", "/api/transfers" };
        return sensitivePaths.Any(p => path.ToString().StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }
}
