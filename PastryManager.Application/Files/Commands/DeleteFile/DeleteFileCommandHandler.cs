using MediatR;
using Microsoft.EntityFrameworkCore;
using PastryManager.Application.Common.Interfaces;
using PastryManager.Application.Common.Models;

namespace PastryManager.Application.Files.Commands.DeleteFile;

public class DeleteFileCommandHandler : IRequestHandler<DeleteFileCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;

    public DeleteFileCommandHandler(
        IApplicationDbContext context,
        IFileStorageService fileStorageService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
    }

    public async Task<Result<bool>> Handle(
        DeleteFileCommand request, 
        CancellationToken cancellationToken)
    {
        var file = await _context.FileAttachments
            .FirstOrDefaultAsync(f => f.Id == request.FileId, cancellationToken);

        if (file == null)
        {
            return Result<bool>.Failure("File not found");
        }

        // Soft delete in database
        file.IsDeleted = true;
        file.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync(cancellationToken);

        // Delete from S3 (fire and forget to not block the response)
        _ = Task.Run(async () =>
        {
            try
            {
                await _fileStorageService.DeleteFileAsync(file.S3Key, cancellationToken);
            }
            catch
            {
                // Log error but don't fail the operation since DB is already updated
                // In production, consider using a message queue for reliable cleanup
            }
        }, cancellationToken);

        return Result<bool>.Success(true);
    }
}
