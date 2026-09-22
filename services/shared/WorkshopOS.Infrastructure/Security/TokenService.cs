using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WorkshopOS.Domain.Entities;

namespace WorkshopOS.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "WorkshopOS";
    public string Audience { get; set; } = "WorkshopOS.Client";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 12;
}

public sealed class TokenService
{
    private readonly JwtOptions _options;
    private readonly PasswordHasher<AppUser> _passwordHasher = new();

    public TokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public string HashPassword(AppUser user, string password) => _passwordHasher.HashPassword(user, password);

    public bool VerifyPassword(AppUser user, string password) =>
        _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;

    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(AppUser user, IEnumerable<string> permissions)
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Email, user.Email),
            new("name", user.DisplayName),
            new(ClaimTypes.Name, user.DisplayName),
            new("role", user.Role.Key),
            new(ClaimTypes.Role, user.Role.Key),
            new("is_owner", user.IsOwner ? "true" : "false")
        };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expires.UtcDateTime,
            signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }

    public static string CreateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
