using MediatR;
using Microsoft.EntityFrameworkCore;
using PastryManager.Application.Common.Interfaces;
using PastryManager.Application.Common.Models;
using PastryManager.Application.Files.DTOs;
using PastryManager.Domain.Entities;
using PastryManager.Domain.Enums;

namespace PastryManager.Application.Files.Commands.UploadFile;

public class UploadFileCommandHandler : IRequestHandler<UploadFileCommand, Result<FileUploadResultDto>>
{
    private readonly IFileStorageService _fileStorageService;
    private readonly IApplicationDbContext _context;
    private readonly IUserRepository _userRepository;

    public UploadFileCommandHandler(
        IFileStorageService fileStorageService,
        IApplicationDbContext context,
        IUserRepository userRepository)
    {
        _fileStorageService = fileStorageService;
        _context = context;
        _userRepository = userRepository;
    }

    public async Task<Result<FileUploadResultDto>> Handle(
        UploadFileCommand request, 
        CancellationToken cancellationToken)
    {
        // Verify user exists
        var user = await _userRepository.GetByIdAsync(request.UploadedBy, cancellationToken);
        if (user == null)
        {
            return Result<FileUploadResultDto>.Failure("User not found");
        }

        // Verify entity exists based on EntityType
        var entityExists = await VerifyEntityExistsAsync(
            request.EntityType, 
            request.EntityId, 
            cancellationToken);

        if (!entityExists)
        {
            return Result<FileUploadResultDto>.Failure(
                $"{request.EntityType} with ID {request.EntityId} not found");
        }

        // Validate file
        var (isValid, errorMessage) = _fileStorageService.ValidateFile(
            request.FileName, 
            request.FileSizeBytes);

        if (!isValid)
        {
            return Result<FileUploadResultDto>.Failure(errorMessage!);
        }

        // Generate file ID
        var fileId = Guid.NewGuid();

        // Upload to S3
        string s3Key;
        try
        {
            s3Key = await _fileStorageService.UploadFileAsync(
                request.FileStream,
                request.FileName,
                request.ContentType,
                request.EntityType,
                request.EntityId,
                fileId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            return Result<FileUploadResultDto>.Failure($"Failed to upload file: {ex.Message}");
        }

        // Save metadata to database
        var fileAttachment = new FileAttachment
        {
            Id = fileId,
            FileName = request.FileName,
            S3Key = s3Key,
            ContentType = request.ContentType,
            FileSizeBytes = request.FileSizeBytes,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            UploadedBy = request.UploadedBy
        };

        _context.FileAttachments.Add(fileAttachment);
        await _context.SaveChangesAsync(cancellationToken);

        var result = new FileUploadResultDto(
            fileId,
            request.FileName,
            request.FileSizeBytes,
            "File uploaded successfully"
        );

        return Result<FileUploadResultDto>.Success(result);
    }

    private async Task<bool> VerifyEntityExistsAsync(
        EntityType entityType, 
        Guid entityId, 
        CancellationToken cancellationToken)
    {
        return entityType switch
        {
            EntityType.TaskRequest => await _context.TaskRequests
                .AnyAsync(t => t.Id == entityId && !t.IsDeleted, cancellationToken),
            EntityType.User => await _context.Users
                .AnyAsync(u => u.Id == entityId && !u.IsDeleted, cancellationToken),
            _ => false
        };
    }
}
