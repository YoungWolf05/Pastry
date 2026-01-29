using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

namespace PastryManager.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        // Exception handler
        app.UseExceptionHandler();

        // Swagger
        app.UseSwagger();
        app.UseSwaggerUI();

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

        // Standard middleware
        app.UseHttpsRedirection();
        app.UseCors("AllowAll");

        // Serilog request logging
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
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

        app.UseAuthorization();
        app.MapControllers();

        return app;
    }
}
