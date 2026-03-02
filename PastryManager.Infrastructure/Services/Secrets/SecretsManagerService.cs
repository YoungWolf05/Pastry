using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace PastryManager.Infrastructure.Services.Secrets;

public interface ISecretsManagerService
{
    Task<T?> GetSecretAsync<T>(string secretName, CancellationToken cancellationToken = default);
    Task<string?> GetSecretStringAsync(string secretName, CancellationToken cancellationToken = default);
    Task CreateOrUpdateSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken = default);
}

/// <summary>
/// AWS Secrets Manager service for secure configuration management
/// Implements zero-trust principles - no secrets in code or config files
/// </summary>
public class SecretsManagerService : ISecretsManagerService
{
    private readonly IAmazonSecretsManager _secretsManager;
    private readonly ILogger<SecretsManagerService> _logger;
    private readonly Dictionary<string, (string Value, DateTime CachedAt)> _cache = new();
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

    public SecretsManagerService(
        IAmazonSecretsManager secretsManager,
        ILogger<SecretsManagerService> logger)
    {
        _secretsManager = secretsManager;
        _logger = logger;
    }

    public async Task<T?> GetSecretAsync<T>(string secretName, CancellationToken cancellationToken = default)
    {
        var secretString = await GetSecretStringAsync(secretName, cancellationToken);
        
        if (string.IsNullOrEmpty(secretString))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(secretString);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize secret {SecretName}", secretName);
            return default;
        }
    }

    public async Task<string?> GetSecretStringAsync(string secretName, CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_cache.TryGetValue(secretName, out var cached))
        {
            if (DateTime.UtcNow - cached.CachedAt < _cacheExpiration)
            {
                return cached.Value;
            }
            _cache.Remove(secretName);
        }

        try
        {
            var request = new GetSecretValueRequest
            {
                SecretId = secretName,
                VersionStage = "AWSCURRENT"
            };

            var response = await _secretsManager.GetSecretValueAsync(request, cancellationToken);
            var secretValue = response.SecretString;

            // Cache the secret
            _cache[secretName] = (secretValue, DateTime.UtcNow);

            _logger.LogInformation("Successfully retrieved secret: {SecretName}", secretName);
            return secretValue;
        }
        catch (ResourceNotFoundException)
        {
            _logger.LogWarning("Secret not found: {SecretName}", secretName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret: {SecretName}", secretName);
            throw;
        }
    }

    public async Task CreateOrUpdateSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to update existing secret
            var updateRequest = new PutSecretValueRequest
            {
                SecretId = secretName,
                SecretString = secretValue
            };

            await _secretsManager.PutSecretValueAsync(updateRequest, cancellationToken);
            _logger.LogInformation("Successfully updated secret: {SecretName}", secretName);

            // Invalidate cache
            _cache.Remove(secretName);
        }
        catch (ResourceNotFoundException)
        {
            // Create new secret if it doesn't exist
            var createRequest = new CreateSecretRequest
            {
                Name = secretName,
                SecretString = secretValue
            };

            await _secretsManager.CreateSecretAsync(createRequest, cancellationToken);
            _logger.LogInformation("Successfully created secret: {SecretName}", secretName);
        }
    }
}
