using MediatR;
using Microsoft.EntityFrameworkCore;
using PastryManager.Application.Common.Interfaces;
using PastryManager.Application.Common.Models;
using PastryManager.Application.Files.DTOs;

namespace PastryManager.Application.Files.Queries.GetFilesByEntity;

public class GetFilesByEntityQueryHandler : IRequestHandler<GetFilesByEntityQuery, Result<List<FileMetadataDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetFilesByEntityQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<FileMetadataDto>>> Handle(
        GetFilesByEntityQuery request, 
        CancellationToken cancellationToken)
    {
        var files = await _context.FileAttachments
            .Include(f => f.UploadedByUser)
            .Where(f => f.EntityType == request.EntityType && f.EntityId == request.EntityId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new FileMetadataDto(
                f.Id,
                f.FileName,
                f.ContentType,
                f.FileSizeBytes,
                f.EntityType,
                f.EntityId,
                f.UploadedBy,
                f.UploadedByUser != null ? $"{f.UploadedByUser.FirstName} {f.UploadedByUser.LastName}" : null,
                f.CreatedAt,
                null
            ))
            .ToListAsync(cancellationToken);

        return Result<List<FileMetadataDto>>.Success(files);
    }
}
