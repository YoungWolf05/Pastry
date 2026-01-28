using MediatR;
using PastryManager.Application.Common.Models;
using PastryManager.Application.Users.DTOs;

namespace PastryManager.Application.Users.Queries.GetUserById;

public record GetUserByIdQuery(Guid UserId) : IRequest<Result<UserDto>>;
