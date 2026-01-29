using Microsoft.Extensions.Diagnostics.HealthChecks;
using PastryManager.Infrastructure.Data;

namespace PastryManager.Api.HealthChecks;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _dbContext;

    public DatabaseHealthCheck(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.Database.CanConnectAsync(cancellationToken);
            return HealthCheckResult.Healthy("Database is reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database check failed", ex);
        }
    }
}
