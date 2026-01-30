using MediatR;
using Microsoft.EntityFrameworkCore;
using PastryManager.Application.Common.Interfaces;
using PastryManager.Application.Common.Models;
using PastryManager.Application.Files.DTOs;

namespace PastryManager.Application.Files.Queries.GetFile;

public class GetFileQueryHandler : IRequestHandler<GetFileQuery, Result<FileMetadataDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;

    public GetFileQueryHandler(
        IApplicationDbContext context,
        IFileStorageService fileStorageService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
    }

    public async Task<Result<FileMetadataDto>> Handle(
        GetFileQuery request, 
        CancellationToken cancellationToken)
    {
        var file = await _context.FileAttachments
            .Include(f => f.UploadedByUser)
            .FirstOrDefaultAsync(f => f.Id == request.FileId, cancellationToken);

        if (file == null)
        {
            return Result<FileMetadataDto>.Failure("File not found");
        }

        // Generate presigned download URL
        var downloadUrl = await _fileStorageService.GetPresignedDownloadUrlAsync(file.S3Key);

        var fileDto = new FileMetadataDto(
            file.Id,
            file.FileName,
            file.ContentType,
            file.FileSizeBytes,
            file.EntityType,
            file.EntityId,
            file.UploadedBy,
            file.UploadedByUser != null ? $"{file.UploadedByUser.FirstName} {file.UploadedByUser.LastName}" : null,
            file.CreatedAt,
            downloadUrl
        );

        return Result<FileMetadataDto>.Success(fileDto);
    }
}
