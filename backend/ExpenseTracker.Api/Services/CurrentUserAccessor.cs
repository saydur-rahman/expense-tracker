using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ExpenseTracker.Api.Exceptions;

namespace ExpenseTracker.Api.Services;

public interface ICurrentUser
{
    Guid Id { get; }
}

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
            var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (!Guid.TryParse(value, out var id))
            {
                throw new UnauthorizedAppException("Not authenticated.");
            }
            return id;
        }
    }
}
