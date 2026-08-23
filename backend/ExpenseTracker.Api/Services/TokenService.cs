using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ExpenseTracker.Api.Services;

public class TokenService : ITokenService
{
    private const int ImpersonationMinutes = 15;

    private readonly JwtOptions _jwtOptions;

    public TokenService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    public string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles)
    {
        var claims = BaseClaims(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return Write(claims, DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes));
    }

    public string GenerateImpersonationToken(ApplicationUser target, Guid adminUserId)
    {
        var claims = BaseClaims(target);
        claims.Add(new Claim(AppClaims.ImpersonatedBy, adminUserId.ToString()));
        claims.Add(new Claim(AppClaims.ImpersonationReadOnly, "true"));
        return Write(claims, DateTime.UtcNow.AddMinutes(ImpersonationMinutes));
    }

    public (string RawToken, string TokenHash, DateTime ExpiresAtUtc) GenerateRefreshToken()
    {
        var rawBytes = RandomNumberGenerator.GetBytes(64);
        var rawToken = Convert.ToBase64String(rawBytes);
        var expiresAtUtc = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays);
        return (rawToken, HashToken(rawToken), expiresAtUtc);
    }

    public string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(bytes);
    }

    private static List<Claim> BaseClaims(ApplicationUser user) => new()
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
        new Claim("displayName", user.DisplayName),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
    };

    private string Write(IEnumerable<Claim> claims, DateTime expiresAtUtc)
    {
        var key = new SymmetricSecurityKey(Convert.FromBase64String(_jwtOptions.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
