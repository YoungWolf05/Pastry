using Microsoft.AspNetCore.Http;

namespace PastryManager.Middleware.Security;

/// <summary>
/// Security headers middleware implementing OWASP best practices
/// Adds headers to mitigate XSS, clickjacking, MIME sniffing, etc.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // OWASP Security Headers

        // Prevent clickjacking attacks
        context.Response.Headers["X-Frame-Options"] = "DENY";
        
        // Enable browser XSS protection
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        
        // Prevent MIME sniffing
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        
        // Referrer policy
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        
        // Content Security Policy (CSP) - relaxed for Swagger UI in development
        var isSwagger = context.Request.Path.StartsWithSegments("/swagger");
        if (isSwagger)
        {
            // Relaxed CSP for Swagger UI (needs inline scripts/styles and eval)
            context.Response.Headers["Content-Security-Policy"] = 
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data: https:; " +
                "font-src 'self' data:; " +
                "connect-src 'self'";
        }
        else
        {
            // Strict CSP for API endpoints
            context.Response.Headers["Content-Security-Policy"] = 
                "default-src 'self'; " +
                "script-src 'self'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data: https:; " +
                "font-src 'self'; " +
                "connect-src 'self'; " +
                "frame-ancestors 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self'";
        }
        
        // Permissions Policy (formerly Feature Policy)
        context.Response.Headers["Permissions-Policy"] = 
            "geolocation=(), microphone=(), camera=(), payment=()";
        
        // Strict Transport Security (HSTS) - force HTTPS
        context.Response.Headers["Strict-Transport-Security"] = 
            "max-age=31536000; includeSubDomains; preload";
        
        // Remove server header to avoid information disclosure
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");
        context.Response.Headers.Remove("X-AspNet-Version");
        context.Response.Headers.Remove("X-AspNetMvc-Version");

        await _next(context);
    }
}
