using PastryManager.Domain.Enums;

namespace PastryManager.Application.Common.Interfaces;

public interface IFileStorageService
{
    /// <summary>
    /// Uploads a file to S3 storage
    /// </summary>
    /// <param name="fileStream">The file stream to upload</param>
    /// <param name="fileName">The original file name</param>
    /// <param name="contentType">The MIME type of the file</param>
    /// <param name="entityType">The type of entity this file belongs to</param>
    /// <param name="entityId">The ID of the entity this file belongs to</param>
    /// <param name="fileId">Unique identifier for the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The S3 key where the file was stored</returns>
    Task<string> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        EntityType entityType,
        Guid entityId,
        Guid fileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a presigned URL for downloading a file
    /// </summary>
    /// <param name="s3Key">The S3 key of the file</param>
    /// <param name="expirationMinutes">URL expiration time in minutes (default: 60)</param>
    /// <returns>Presigned URL for file download</returns>
    Task<string> GetPresignedDownloadUrlAsync(
        string s3Key,
        int expirationMinutes = 60);

    /// <summary>
    /// Deletes a file from S3 storage
    /// </summary>
    /// <param name="s3Key">The S3 key of the file to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteFileAsync(
        string s3Key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if a file meets the requirements (size, extension)
    /// </summary>
    /// <param name="fileName">The file name</param>
    /// <param name="fileSizeBytes">The file size in bytes</param>
    /// <returns>Validation result with error message if invalid</returns>
    (bool IsValid, string? ErrorMessage) ValidateFile(string fileName, long fileSizeBytes);
}
