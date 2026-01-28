using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PastryManager.Application.Common.Interfaces;
using PastryManager.Infrastructure.Data;
using PastryManager.Infrastructure.Repositories;
using PastryManager.Infrastructure.Services;

namespace PastryManager.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Aspire injects connection string with the database resource name "pastrydb"
        var connectionString = configuration.GetConnectionString("pastrydb")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'pastrydb' or 'DefaultConnection' not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITaskRequestRepository, TaskRequestRepository>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();

        return services;
    }
}
