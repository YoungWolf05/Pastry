using MediatR;
using PastryManager.Application.Common.Models;
using PastryManager.Application.Users.DTOs;

namespace PastryManager.Application.Users.Commands.RegisterUser;

public record RegisterUserCommand(
    string Email,
    string FirstName,
    string LastName,
    string Password
) : IRequest<Result<UserDto>>;
