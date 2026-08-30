using System.Collections.Immutable;
using System.Security.Claims;
using Auth019.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Auth019.Services;

/// <summary>
/// RFC 8693 token exchange, used for one purpose: letting an admin obtain a
/// <em>read-only</em> token that acts as another user, for support.
///
/// The issued token deliberately carries only <c>expense.read</c> — no write scope,
/// no admin scope, and no roles — so the resource server needs no special knowledge
/// of impersonation: an ordinary scope check is enough. It is also not paired with a
/// refresh token, so it simply expires and cannot be silently extended.
/// </summary>
public class TokenExchangeHandler
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOpenIddictScopeManager _scopeManager;

    public TokenExchangeHandler(UserManager<ApplicationUser> userManager, IOpenIddictScopeManager scopeManager)
    {
        _userManager = userManager;
        _scopeManager = scopeManager;
    }

    public async Task<IActionResult> HandleAsync(HttpContext context, OpenIddictRequest request)
    {
        // OpenIddict has already validated the subject_token from the request body by
        // this point; the server scheme surfaces the principal it resolved from it.
        // (Authenticating with the *validation* scheme would look for a bearer header,
        // which a token-exchange request does not carry.)
        var authentication = await context.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        if (!authentication.Succeeded || authentication.Principal is null)
        {
            return Error(Errors.InvalidGrant, "A valid subject token is required.");
        }

        var actorPrincipal = authentication.Principal;
        var actorId = actorPrincipal.GetClaim(Claims.Subject);

        if (actorId is null || !Guid.TryParse(actorId, out var actorGuid))
        {
            return Error(Errors.InvalidGrant, "The subject token is not valid.");
        }

        // Only admins may impersonate.
        var actor = await _userManager.FindByIdAsync(actorId);
        if (actor is null || !actor.IsActive || !await _userManager.IsInRoleAsync(actor, AppRoles.Admin))
        {
            return Error(Errors.InvalidGrant, "Only an administrator can exchange a token.");
        }

        // An already-impersonated session must not be able to chain another exchange.
        if (!string.IsNullOrEmpty(actorPrincipal.GetClaim(AppClaims.ImpersonatedBy)))
        {
            return Error(Errors.InvalidGrant, "An impersonated session cannot impersonate.");
        }

        var requestedSubject = (string?)request.GetParameter(AppTokenExchange.RequestedSubjectParameter);
        if (string.IsNullOrWhiteSpace(requestedSubject) || !Guid.TryParse(requestedSubject, out var targetGuid))
        {
            return Error(Errors.InvalidRequest, "A requested_subject is required.");
        }

        if (targetGuid == actorGuid)
        {
            return Error(Errors.InvalidRequest, "You are already signed in as this account.");
        }

        var target = await _userManager.FindByIdAsync(requestedSubject);
        if (target is null)
        {
            return Error(Errors.InvalidRequest, "The requested user does not exist.");
        }

        if (!target.IsActive)
        {
            return Error(Errors.InvalidRequest, "This account is deactivated. Reactivate it before viewing as this user.");
        }

        // Impersonating another admin would let one admin borrow another's authority.
        if (await _userManager.IsInRoleAsync(target, AppRoles.Admin))
        {
            return Error(Errors.InvalidRequest, "Admin accounts cannot be impersonated.");
        }

        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType,
            Claims.Name,
            Claims.Role);

        identity
            .SetClaim(Claims.Subject, target.Id.ToString())
            .SetClaim(Claims.Email, target.Email)
            .SetClaim(Claims.Name, target.DisplayName)
            .SetClaim(AppClaims.ImpersonatedBy, actor.Id.ToString());

        // The admin is looking at this user's books, so amounts should read in
        // this user's currency, not the admin's.
        if (!string.IsNullOrWhiteSpace(target.Country))
        {
            identity.SetClaim(AppClaims.Country, target.Country);
            identity.SetClaim(AppClaims.Currency, Countries.CurrencyFor(target.Country));
        }

        // Read-only, and no roles: this token can never reach a write endpoint or admin API.
        identity.SetScopes(ImmutableArray.Create(AppScopes.ExpenseRead));
        identity.SetResources(await _scopeManager.ListResourcesAsync(identity.GetScopes()).ToListAsync());
        identity.SetDestinations(TokenDestinations.Resolve);

        // Deliberately short-lived and non-renewable.
        identity.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));

        var principal = new ClaimsPrincipal(identity);

        return new Microsoft.AspNetCore.Mvc.SignInResult(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, principal);
    }

    private static ForbidResult Error(string error, string description) => new(
        OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
        new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
        }));
}
