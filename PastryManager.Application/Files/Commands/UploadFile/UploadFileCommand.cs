using MediatR;
using PastryManager.Application.Common.Models;
using PastryManager.Application.Files.DTOs;
using PastryManager.Domain.Enums;

namespace PastryManager.Application.Files.Commands.UploadFile;

public record UploadFileCommand(
    Stream FileStream,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    EntityType EntityType,
    Guid EntityId,
    Guid UploadedBy
) : IRequest<Result<FileUploadResultDto>>;
