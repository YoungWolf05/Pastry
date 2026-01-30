using MediatR;
using PastryManager.Application.Common.Models;

namespace PastryManager.Application.Files.Commands.DeleteFile;

public record DeleteFileCommand(Guid FileId, Guid UserId) : IRequest<Result<bool>>;
