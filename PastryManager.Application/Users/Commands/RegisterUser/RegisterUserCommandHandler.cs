using MediatR;
using PastryManager.Application.Common.Interfaces;
using PastryManager.Application.Common.Models;
using PastryManager.Application.Users.DTOs;
using PastryManager.Domain.Entities;

namespace PastryManager.Application.Users.Commands.RegisterUser;

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public RegisterUserCommandHandler(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<UserDto>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        // Check if user already exists
        if (await _userRepository.ExistsAsync(request.Email, cancellationToken))
        {
            return Result<UserDto>.Failure("User with this email already exists");
        }

        // Create new user
        var user = new User
        {
            Email = request.Email.ToLowerInvariant(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = UserRole.User,
            IsActive = true
        };

        // Save user
        var createdUser = await _userRepository.AddAsync(user, cancellationToken);

        var userDto = new UserDto(
            createdUser.Id,
            createdUser.Email,
            createdUser.FirstName,
            createdUser.LastName,
            createdUser.Role,
            createdUser.IsActive,
            createdUser.CreatedAt,
            createdUser.LastLoginAt
        );

        return Result<UserDto>.Success(userDto);
    }
}
