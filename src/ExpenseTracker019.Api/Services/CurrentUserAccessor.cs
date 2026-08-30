using ExpenseTracker019.Api.Exceptions;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ExpenseTracker019.Api.Services;

public interface ICurrentUser
{
    Guid Id { get; }

    /// <summary>Display name from the token, for attributing what the user writes.</summary>
    string DisplayName { get; }

    /// <summary>Email from the token. Never used to identify — that is always <see cref="Id"/>.</summary>
    string Email { get; }

    /// <summary>True when the token carries the Admin role. Impersonation tokens carry none.</summary>
    bool IsAdmin { get; }
}

/// <summary>
/// The authenticated user's id, read from the <c>sub</c> claim of an Auth019 token.
/// This is the tenant-isolation boundary: every query is scoped by this value and a
/// client-supplied user id is never trusted.
/// </summary>
public class CurrentUserAccessor : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid Id
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.GetClaim(Claims.Subject);
            if (!Guid.TryParse(value, out var id))
            {
                throw new UnauthorizedAppException("Not authenticated.");
            }
            return id;
        }
    }

    public string DisplayName =>
        _httpContextAccessor.HttpContext?.User.GetClaim(Claims.Name)
        ?? _httpContextAccessor.HttpContext?.User.GetClaim(Claims.Email)
        ?? "Unknown";

    public string Email => _httpContextAccessor.HttpContext?.User.GetClaim(Claims.Email) ?? string.Empty;

    // An impersonation token deliberately carries no roles, so an impersonated
    // session can never reach anything gated on this.
    public bool IsAdmin =>
        _httpContextAccessor.HttpContext?.User.HasClaim(Claims.Role, AppRoles.Admin) ?? false;
}
