using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ExpenseTracker019.Api.Authorization;

/// <summary>
/// Requires the <c>expense.write</c> scope for every state-changing request.
///
/// Applied globally rather than per-action so an endpoint added later is protected
/// by default, with nobody needing to remember the attribute. This is what makes a
/// read-only impersonation token harmless here: it simply lacks the write scope.
/// </summary>
public class RequireWriteScopeFilter : IAsyncAuthorizationFilter
{
    private static readonly string[] SafeMethods =
        { HttpMethods.Get, HttpMethods.Head, HttpMethods.Options, HttpMethods.Trace };

    private readonly IAuthorizationService _authorizationService;

    public RequireWriteScopeFilter(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (SafeMethods.Contains(context.HttpContext.Request.Method))
        {
            return;
        }

        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
        {
            return;
        }

        var result = await _authorizationService.AuthorizeAsync(
            context.HttpContext.User, AuthPolicies.ExpenseWrite);

        if (!result.Succeeded)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "This session is read-only. Exit impersonation to make changes.",
            })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
        }
    }
}
