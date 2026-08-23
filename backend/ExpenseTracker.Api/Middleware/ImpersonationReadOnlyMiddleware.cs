using ExpenseTracker.Api.Exceptions;

namespace ExpenseTracker.Api.Middleware;

/// <summary>
/// Blocks every state-changing request made with an impersonation token.
/// Enforced centrally rather than per-endpoint so any endpoint added later is
/// read-only under impersonation by default, without needing to remember the check.
/// </summary>
public class ImpersonationReadOnlyMiddleware
{
    private static readonly string[] SafeMethods = { HttpMethods.Get, HttpMethods.Head, HttpMethods.Options };

    private readonly RequestDelegate _next;

    public ImpersonationReadOnlyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var isImpersonating = context.User.HasClaim(AppClaims.ImpersonationReadOnly, "true");

        if (isImpersonating)
        {
            if (!SafeMethods.Contains(context.Request.Method))
            {
                throw new ForbiddenAppException("You are viewing this account read-only. Exit impersonation to make changes.");
            }

            // An impersonated session carries no admin role, but this closes the door
            // explicitly so admin surface can never be reached while impersonating.
            if (context.Request.Path.StartsWithSegments("/api/admin"))
            {
                throw new ForbiddenAppException("Admin endpoints are not available while impersonating.");
            }
        }

        await _next(context);
    }
}
