using PastryManager.Domain.Enums;

namespace PastryManager.Application.Files.DTOs;

public record FileMetadataDto(
    Guid Id,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    EntityType EntityType,
    Guid EntityId,
    Guid UploadedBy,
    string? UploadedByName,
    DateTime CreatedAt,
    string? DownloadUrl = null
);
