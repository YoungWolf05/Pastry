using PastryManager.Application;
using PastryManager.Infrastructure;
using PastryManager.Infrastructure.Data;
using PastryManager.Api.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Exceptions;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithExceptionDetails()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/pastrymanager-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting PastryManager API application");

    var builder = WebApplication.CreateBuilder(args);

    // Add Serilog
    builder.Host.UseSerilog();

    // Add services to the container.
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // Add global exception handler
    builder.Services.AddExceptionHandler<PastryManager.Api.Middleware.GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "PastryManager API", Version = "v1" });
    });

    // Add health checks for AWS
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
        .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" });

    // Add CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    var app = builder.Build();

    // Add exception handler middleware
    app.UseExceptionHandler();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();

        // Auto-migrate database in development with retry logic
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            var retryCount = 0;
            var maxRetries = 10;
            var delay = TimeSpan.FromSeconds(2);

            while (retryCount < maxRetries)
            {
                try
                {
                    logger.LogInformation("Attempting to migrate database... (Attempt {Attempt}/{MaxRetries})", retryCount + 1, maxRetries);
                    await dbContext.Database.MigrateAsync();
                    logger.LogInformation("Database migration completed successfully.");
                    break;
                }
                catch (Exception ex) when (retryCount < maxRetries - 1)
                {
                    retryCount++;
                    logger.LogWarning(ex, "Database migration failed. Retrying in {Delay} seconds...", delay.TotalSeconds);
                    await Task.Delay(delay);
                }
            }
        }
    }
    else
    {
        // In production, enable Swagger
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // Health check endpoints for AWS ECS/ELB
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("live")
    });

    app.UseHttpsRedirection();
    app.UseCors("AllowAll");

    // Add Serilog request logging
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
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

    app.Run();
    Log.Information("PastryManager API stopped cleanly");
}
catch (Exception ex)
{
    Log.Fatal(ex, "PastryManager API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
