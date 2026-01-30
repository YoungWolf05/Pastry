using MediatR;
using PastryManager.Application.Common.Models;
using PastryManager.Application.Files.DTOs;

namespace PastryManager.Application.Files.Queries.GetFile;

public record GetFileQuery(Guid FileId) : IRequest<Result<FileMetadataDto>>;
