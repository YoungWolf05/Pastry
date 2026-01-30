using MediatR;
using Microsoft.AspNetCore.Mvc;
using PastryManager.Application.Files.Commands.DeleteFile;
using PastryManager.Application.Files.Commands.UploadFile;
using PastryManager.Application.Files.DTOs;
using PastryManager.Application.Files.Queries.GetFile;
using PastryManager.Application.Files.Queries.GetFilesByEntity;
using PastryManager.Domain.Enums;

namespace PastryManager.Api.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private readonly IMediator _mediator;

    public FilesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Upload a file for a specific entity
    /// </summary>
    /// <param name="entityType">Entity type: 'taskrequest' or 'user'</param>
    /// <param name="entityId">GUID of the entity to attach the file to</param>
    /// <param name="file">The file to upload (max 10MB)</param>
    /// <param name="userId">User ID from header</param>
    /// <remarks>
    /// Example: POST /api/files/taskrequest/123e4567-e89b-12d3-a456-426614174000
    /// 
    /// Supported entity types:
    /// - taskrequest: Attach file to a task request
    /// - user: Attach file to a user profile
    /// </remarks>
    [HttpPost("{entityType}/{entityId:guid}")]
    [ProducesResponseType(typeof(FileUploadResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB limit
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadFile(
        [FromRoute] string entityType,
        [FromRoute] Guid entityId,
        IFormFile file,
        [FromHeader(Name = "X-User-Id")] Guid userId)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { errors = new[] { "File is required" } });
        }

        if (!Enum.TryParse<EntityType>(entityType, true, out var parsedEntityType))
        {
            return BadRequest(new { errors = new[] { $"Invalid entity type: {entityType}" } });
        }

        using var stream = file.OpenReadStream();
        var command = new UploadFileCommand(
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            parsedEntityType, 
            entityId, 
            userId);
        
        var result = await _mediator.Send(command);

        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return CreatedAtAction(
            nameof(GetFile), 
            new { fileId = result.Data!.FileId }, 
            result.Data);
    }

    /// <summary>
    /// Get all files for a specific entity
    /// </summary>
    [HttpGet("{entityType}/{entityId:guid}")]
    [ProducesResponseType(typeof(List<FileMetadataDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetFilesByEntity(string entityType, Guid entityId)
    {
        if (!Enum.TryParse<EntityType>(entityType, true, out var parsedEntityType))
        {
            return BadRequest(new { errors = new[] { $"Invalid entity type: {entityType}" } });
        }

        var query = new GetFilesByEntityQuery(parsedEntityType, entityId);
        var result = await _mediator.Send(query);

        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Get a specific file with presigned download URL
    /// </summary>
    [HttpGet("{fileId:guid}")]
    [ProducesResponseType(typeof(FileMetadataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFile(Guid fileId)
    {
        var query = new GetFileQuery(fileId);
        var result = await _mediator.Send(query);

        if (!result.IsSuccess)
        {
            return NotFound(new { errors = result.Errors });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Delete a file
    /// </summary>
    [HttpDelete("{fileId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFile(
        Guid fileId,
        [FromHeader(Name = "X-User-Id")] Guid userId)
    {
        var command = new DeleteFileCommand(fileId, userId);
        var result = await _mediator.Send(command);

        if (!result.IsSuccess)
        {
            return NotFound(new { errors = result.Errors });
        }

        return NoContent();
    }
}
