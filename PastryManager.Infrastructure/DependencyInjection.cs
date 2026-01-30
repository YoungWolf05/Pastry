using Amazon.S3;
using Amazon.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Amazon.Extensions.NETCore.Setup;
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

        // Register DbContext as IApplicationDbContext
        services.AddScoped<IApplicationDbContext, ApplicationDbContext>();

        // Register repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITaskRequestRepository, TaskRequestRepository>();
        
        // Register infrastructure services
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        
        // Configure AWS S3
        ConfigureAwsServices(services, configuration);

        return services;
    }

    private static void ConfigureAwsServices(IServiceCollection services, IConfiguration configuration)
    {
        var awsOptions = configuration.GetAWSOptions();
        
        // Check for LocalStack configuration (development environment)
        var serviceUrl = configuration["AWS:S3:ServiceURL"];
        var forcePathStyleStr = configuration["AWS:S3:ForcePathStyle"];
        var forcePathStyle = !string.IsNullOrEmpty(forcePathStyleStr) && bool.Parse(forcePathStyleStr);

        if (!string.IsNullOrEmpty(serviceUrl))
        {
            // LocalStack configuration for local development
            awsOptions.DefaultClientConfig.ServiceURL = serviceUrl;
            
            // Use dummy credentials for LocalStack
            awsOptions.Credentials = new BasicAWSCredentials("test", "test");
        }

        services.AddDefaultAWSOptions(awsOptions);
        
        // Configure S3 client with ForcePathStyle for LocalStack
        services.AddAWSService<IAmazonS3>(awsOptions);
        
        if (forcePathStyle)
        {
            services.AddSingleton<IAmazonS3>(sp =>
            {
                var config = new AmazonS3Config
                {
                    ServiceURL = serviceUrl,
                    ForcePathStyle = true,
                    AuthenticationRegion = awsOptions.Region?.SystemName ?? "us-east-1"
                };
                
                return new AmazonS3Client(
                    new BasicAWSCredentials("test", "test"),
                    config);
            });
        }
        
        // Register file storage service
        services.AddScoped<IFileStorageService, S3FileStorageService>();
    }
}
