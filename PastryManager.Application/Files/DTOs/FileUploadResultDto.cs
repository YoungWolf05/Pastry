namespace PastryManager.Application.Files.DTOs;

public record FileUploadResultDto(
    Guid FileId,
    string FileName,
    long FileSizeBytes,
    string Message
);
