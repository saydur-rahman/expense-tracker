using ExpenseTracker.Api.Models;

namespace ExpenseTracker.Api.Services;

public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles);

    /// <summary>
    /// A short-lived, read-only token acting as <paramref name="target"/>, stamped with the
    /// acting admin's id. Deliberately carries no roles — an impersonated session must never
    /// inherit admin rights — and has no matching refresh token.
    /// </summary>
    string GenerateImpersonationToken(ApplicationUser target, Guid adminUserId);

    (string RawToken, string TokenHash, DateTime ExpiresAtUtc) GenerateRefreshToken();
    string HashToken(string rawToken);
}
