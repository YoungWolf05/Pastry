using MediatR;
using PastryManager.Application.Common.Models;
using PastryManager.Application.Files.DTOs;
using PastryManager.Domain.Enums;

namespace PastryManager.Application.Files.Queries.GetFilesByEntity;

public record GetFilesByEntityQuery(
    EntityType EntityType,
    Guid EntityId
) : IRequest<Result<List<FileMetadataDto>>>;
