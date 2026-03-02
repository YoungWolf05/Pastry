using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PastryManager.Middleware.Security;

/// <summary>
/// Request validation middleware for input sanitization
/// Implements OWASP Input Validation principles
/// </summary>
public class InputValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<InputValidationMiddleware> _logger;
    
    // Dangerous patterns that might indicate injection attacks
    private static readonly string[] DangerousPatterns = 
    {
        "<script", "javascript:", "onerror=", "onload=", "eval(",
        "'; DROP", "' OR '1'='1", "'; DELETE", "'; UPDATE",
        "../", "..\\", "cmd.exe", "powershell"
    };

    public InputValidationMiddleware(RequestDelegate next, ILogger<InputValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Validate query string
        if (context.Request.QueryString.HasValue)
        {
            var queryString = context.Request.QueryString.Value;
            if (ContainsDangerousContent(queryString))
            {
                _logger.LogWarning(
                    "Dangerous content detected in query string from IP: {ClientIp}",
                    context.Connection.RemoteIpAddress);
                    
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Invalid request detected.");
                return;
            }
        }

        // Validate headers for injection attempts (skip standard HTTP headers)
        var standardHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Accept", "Accept-Encoding", "Accept-Language", "Authorization", "Cache-Control",
            "Connection", "Content-Length", "Content-Type", "Cookie", "Host", "Origin",
            "Referer", "User-Agent", "Sec-Fetch-Site", "Sec-Fetch-Mode", "Sec-Fetch-Dest",
            "Sec-Ch-Ua", "Sec-Ch-Ua-Mobile", "Sec-Ch-Ua-Platform"
        };

        foreach (var header in context.Request.Headers)
        {
            // Only validate custom headers, skip standard HTTP headers
            if (standardHeaders.Contains(header.Key))
                continue;

            if (ContainsDangerousContent(header.Value))
            {
                _logger.LogWarning(
                    "Dangerous content detected in header '{HeaderName}' from IP: {ClientIp}",
                    header.Key,
                    context.Connection.RemoteIpAddress);
                    
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Invalid request detected.");
                return;
            }
        }

        await _next(context);
    }

    private static bool ContainsDangerousContent(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return false;

        var lowerContent = content.ToLowerInvariant();
        return DangerousPatterns.Any(pattern => 
            lowerContent.Contains(pattern.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase));
    }
}
