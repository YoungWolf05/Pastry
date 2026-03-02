namespace PastryManager.Infrastructure.Services.Authentication;

public class JwtSettings
{
    public required string SecretKey { get; set; }
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
    public required string SigningAlgorithm { get; set; } = "HS512"; // SHA-512
}
