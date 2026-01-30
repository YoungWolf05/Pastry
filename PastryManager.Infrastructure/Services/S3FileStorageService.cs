using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PastryManager.Application.Common.Interfaces;
using PastryManager.Domain.Enums;

namespace PastryManager.Infrastructure.Services;

public class S3FileStorageService : IFileStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<S3FileStorageService> _logger;
    private readonly string _bucketName;
    private readonly long _maxFileSizeBytes;
    private readonly HashSet<string> _allowedExtensions;

    public S3FileStorageService(
        IAmazonS3 s3Client,
        IConfiguration configuration,
        ILogger<S3FileStorageService> logger)
    {
        _s3Client = s3Client;
        _logger = logger;
        _bucketName = configuration["AWS:S3:BucketName"] 
            ?? throw new InvalidOperationException("AWS:S3:BucketName configuration is missing");
        
        // Default: 10 MB max file size
        var maxSizeConfig = configuration["FileUpload:MaxFileSizeBytes"];
        _maxFileSizeBytes = !string.IsNullOrEmpty(maxSizeConfig) 
            ? long.Parse(maxSizeConfig) 
            : 10 * 1024 * 1024;
        
        // Default allowed extensions
        var allowedExtensionsConfig = configuration.GetSection("FileUpload:AllowedExtensions").GetChildren()
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrEmpty(x))
            .Cast<string>()
            .ToArray();
        
        var allowedExtensions = allowedExtensionsConfig.Length > 0
            ? allowedExtensionsConfig
            : new[] { ".pdf", ".docx", ".xlsx", ".png", ".jpg", ".jpeg", ".txt" };
        _allowedExtensions = new HashSet<string>(allowedExtensions, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<string> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        EntityType entityType,
        Guid entityId,
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate S3 key: uploads/{entityType}/{entityId}/{fileId}-{filename}
            var s3Key = GenerateS3Key(entityType, entityId, fileId, fileName);

            _logger.LogInformation(
                "Uploading file to S3. Bucket: {BucketName}, Key: {S3Key}, ContentType: {ContentType}, Size: {Size}",
                _bucketName, s3Key, contentType, fileStream.Length);

            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = s3Key,
                InputStream = fileStream,
                ContentType = contentType,
                Metadata =
                {
                    ["original-filename"] = fileName,
                    ["entity-type"] = entityType.ToString(),
                    ["entity-id"] = entityId.ToString(),
                    ["file-id"] = fileId.ToString()
                }
            };

            var response = await _s3Client.PutObjectAsync(putRequest, cancellationToken);

            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                _logger.LogInformation("Successfully uploaded file to S3. Key: {S3Key}", s3Key);
                return s3Key;
            }

            throw new InvalidOperationException($"Failed to upload file to S3. Status code: {response.HttpStatusCode}");
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "AWS S3 error uploading file. Error: {Error}", ex.Message);
            throw new InvalidOperationException($"Failed to upload file to S3: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading file to S3");
            throw;
        }
    }

    public async Task<string> GetPresignedDownloadUrlAsync(string s3Key, int expirationMinutes = 60)
    {
        try
        {
            _logger.LogInformation("Generating presigned URL for S3 key: {S3Key}, Expiration: {Minutes} minutes", 
                s3Key, expirationMinutes);

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = s3Key,
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
                Verb = HttpVerb.GET
            };

            var url = await _s3Client.GetPreSignedURLAsync(request);
            
            _logger.LogInformation("Generated presigned URL for S3 key: {S3Key}", s3Key);
            
            return url;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "AWS S3 error generating presigned URL. Error: {Error}", ex.Message);
            throw new InvalidOperationException($"Failed to generate download URL: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating presigned URL for S3 key: {S3Key}", s3Key);
            throw;
        }
    }

    public async Task DeleteFileAsync(string s3Key, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deleting file from S3. Key: {S3Key}", s3Key);

            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = s3Key
            };

            var response = await _s3Client.DeleteObjectAsync(deleteRequest, cancellationToken);

            if (response.HttpStatusCode == System.Net.HttpStatusCode.NoContent || 
                response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                _logger.LogInformation("Successfully deleted file from S3. Key: {S3Key}", s3Key);
            }
            else
            {
                _logger.LogWarning("Unexpected response when deleting file from S3. Key: {S3Key}, Status: {Status}", 
                    s3Key, response.HttpStatusCode);
            }
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "AWS S3 error deleting file. Key: {S3Key}, Error: {Error}", s3Key, ex.Message);
            throw new InvalidOperationException($"Failed to delete file from S3: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting file from S3. Key: {S3Key}", s3Key);
            throw;
        }
    }

    public (bool IsValid, string? ErrorMessage) ValidateFile(string fileName, long fileSizeBytes)
    {
        // Check file size
        if (fileSizeBytes > _maxFileSizeBytes)
        {
            var maxSizeMB = _maxFileSizeBytes / (1024.0 * 1024.0);
            return (false, $"File size exceeds maximum allowed size of {maxSizeMB:F2} MB");
        }

        if (fileSizeBytes <= 0)
        {
            return (false, "File is empty");
        }

        // Check file extension
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension))
        {
            return (false, "File must have an extension");
        }

        if (!_allowedExtensions.Contains(extension))
        {
            var allowed = string.Join(", ", _allowedExtensions);
            return (false, $"File type '{extension}' is not allowed. Allowed types: {allowed}");
        }

        return (true, null);
    }

    private static string GenerateS3Key(EntityType entityType, Guid entityId, Guid fileId, string fileName)
    {
        // Sanitize filename to remove any path characters
        var sanitizedFileName = Path.GetFileName(fileName);
        
        // Create key: uploads/{entityType}/{entityId}/{fileId}-{filename}
        var entityTypeFolder = entityType.ToString().ToLowerInvariant();
        return $"uploads/{entityTypeFolder}/{entityId}/{fileId}-{sanitizedFileName}";
    }
}
