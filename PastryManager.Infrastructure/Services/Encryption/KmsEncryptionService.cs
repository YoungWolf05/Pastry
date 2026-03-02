using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace PastryManager.Infrastructure.Services.Encryption;

public class EncryptionSettings
{
    public required string KmsKeyId { get; set; }
    public required string Region { get; set; }
    public string Algorithm { get; set; } = "SYMMETRIC_DEFAULT";
}

public interface IEncryptionService
{
    Task<string> EncryptAsync(string plaintext, CancellationToken cancellationToken = default);
    Task<string> DecryptAsync(string ciphertext, CancellationToken cancellationToken = default);
    Task<byte[]> EncryptBytesAsync(byte[] plaintext, CancellationToken cancellationToken = default);
    Task<byte[]> DecryptBytesAsync(byte[] ciphertext, CancellationToken cancellationToken = default);
}

/// <summary>
/// AWS KMS-based encryption service for PII and sensitive data
/// Follows zero-trust principles with centralized key management
/// </summary>
public class KmsEncryptionService : IEncryptionService
{
    private readonly IAmazonKeyManagementService _kmsClient;
    private readonly ILogger<KmsEncryptionService> _logger;
    private readonly EncryptionSettings _settings;

    public KmsEncryptionService(
        IAmazonKeyManagementService kmsClient,
        IOptions<EncryptionSettings> settings,
        ILogger<KmsEncryptionService> logger)
    {
        _kmsClient = kmsClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> EncryptAsync(string plaintext, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(plaintext))
            return plaintext;

        try
        {
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var encryptedBytes = await EncryptBytesAsync(plaintextBytes, cancellationToken);
            return Convert.ToBase64String(encryptedBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt data");
            throw new InvalidOperationException("Encryption failed", ex);
        }
    }

    public async Task<string> DecryptAsync(string ciphertext, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(ciphertext))
            return ciphertext;

        try
        {
            var ciphertextBytes = Convert.FromBase64String(ciphertext);
            var decryptedBytes = await DecryptBytesAsync(ciphertextBytes, cancellationToken);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt data");
            throw new InvalidOperationException("Decryption failed", ex);
        }
    }

    public async Task<byte[]> EncryptBytesAsync(byte[] plaintext, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new EncryptRequest
            {
                KeyId = _settings.KmsKeyId,
                Plaintext = new MemoryStream(plaintext),
                EncryptionAlgorithm = EncryptionAlgorithmSpec.FindValue(_settings.Algorithm)
            };

            var response = await _kmsClient.EncryptAsync(request, cancellationToken);
            
            using var memoryStream = new MemoryStream();
            response.CiphertextBlob.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KMS encryption failed");
            throw;
        }
    }

    public async Task<byte[]> DecryptBytesAsync(byte[] ciphertext, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new DecryptRequest
            {
                CiphertextBlob = new MemoryStream(ciphertext),
                KeyId = _settings.KmsKeyId,
                EncryptionAlgorithm = EncryptionAlgorithmSpec.FindValue(_settings.Algorithm)
            };

            var response = await _kmsClient.DecryptAsync(request, cancellationToken);
            
            using var memoryStream = new MemoryStream();
            response.Plaintext.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KMS decryption failed");
            throw;
        }
    }
}
