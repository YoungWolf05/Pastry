using Microsoft.Extensions.Diagnostics.HealthChecks;
using PastryManager.Api.HealthChecks;

namespace PastryManager.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        // Exception handler
        services.AddExceptionHandler<Middleware.GlobalExceptionHandler>();
        services.AddProblemDetails();

        // Controllers
        services.AddControllers();
        services.AddEndpointsApiExplorer();

        // Swagger
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { Title = "PastryManager API", Version = "v1" });
        });

        // Health checks
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
            .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" });

        // CORS
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        return services;
    }
}
