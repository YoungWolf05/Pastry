using Microsoft.EntityFrameworkCore;
using PastryManager.Infrastructure.Data;

namespace PastryManager.Api.Extensions;

public static class DatabaseExtensions
{
    public static async Task MigrateDatabaseAsync(
        this IServiceProvider serviceProvider,
        IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
            return;

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        const int maxRetries = 10;
        var delay = TimeSpan.FromSeconds(2);

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                logger.LogInformation("Attempting to migrate database... (Attempt {Attempt}/{MaxRetries})", attempt, maxRetries);
                await dbContext.Database.MigrateAsync();
                logger.LogInformation("Database migration completed successfully.");
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                logger.LogWarning(ex, "Database migration failed. Retrying in {Delay} seconds...", delay.TotalSeconds);
                await Task.Delay(delay);
            }
        }
    }
}
