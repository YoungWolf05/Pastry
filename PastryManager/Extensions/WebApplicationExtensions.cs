using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using PastryManager.Middleware.Security;
using Serilog;

namespace PastryManager.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        // Security middleware - must be first
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<RateLimitingMiddleware>();
        app.UseMiddleware<InputValidationMiddleware>();
        
        // Exception handler
        app.UseExceptionHandler();

        // Swagger (development only recommended for production)
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // Health check endpoints
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live")
        });

        // Force HTTPS
        app.UseHttpsRedirection();
        
        // HSTS (HTTP Strict Transport Security)
        app.UseHsts();
        
        // CORS with secure policy
        app.UseCors("SecurePolicy");

        // Serilog request logging
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
                diagnosticContext.Set("RemoteIP", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
            };
            options.GetLevel = (httpContext, elapsed, ex) =>
            {
                if (ex != null)
                    return Serilog.Events.LogEventLevel.Error;

                return httpContext.Response.StatusCode >= 500
                    ? Serilog.Events.LogEventLevel.Error
                    : Serilog.Events.LogEventLevel.Information;
            };
        });

        // Authentication & Authorization - Order matters!
        app.UseAuthentication();
        app.UseAuthorization();
        
        // Audit logging middleware
        app.UseMiddleware<AuditLoggingMiddleware>();
        
        app.MapControllers();

        return app;
    }
}
