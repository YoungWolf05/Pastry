namespace PastryManager.Application.Users.DTOs;

public record RegisterUserDto(
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string Password
);
