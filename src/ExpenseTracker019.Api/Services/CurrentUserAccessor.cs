using ExpenseTracker019.Api.Exceptions;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ExpenseTracker019.Api.Services;

public interface ICurrentUser
{
    Guid Id { get; }
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
}
