using System.Collections.Immutable;
using System.Security.Claims;
using Auth019.Models;
using Auth019.Services;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using Microsoft.AspNetCore.WebUtilities;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Auth019.Controllers;

/// <summary>
/// The OAuth 2.0 / OpenID Connect endpoints. The SPA never sees credentials —
/// it redirects here, the user signs in against Auth019's own login page, and
/// an authorization code comes back to be exchanged for tokens.
/// </summary>
public class AuthorizationController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictScopeManager _scopeManager;
    private readonly TokenExchangeHandler _tokenExchangeHandler;
    private readonly IOpenIddictTokenManager _tokenManager;

    public AuthorizationController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager,
        TokenExchangeHandler tokenExchangeHandler,
        IOpenIddictTokenManager tokenManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _applicationManager = applicationManager;
        _scopeManager = scopeManager;
        _tokenExchangeHandler = tokenExchangeHandler;
        _tokenManager = tokenManager;
    }

    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        var result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);

        // Not signed in (or the client demanded a fresh login) — bounce to the login page.
        if (!result.Succeeded || request.HasPromptValue(PromptValues.Login))
        {
            if (request.HasPromptValue(PromptValues.None))
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.LoginRequired,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user is not logged in.",
                    }));
            }

            return Challenge(
                authenticationSchemes: IdentityConstants.ApplicationScheme,
                properties: new AuthenticationProperties
                {
                    RedirectUri = Request.PathBase + Request.Path + QueryString.Create(
                        Request.HasFormContentType ? Request.Form : Request.Query),
                });
        }

        var user = await _userManager.GetUserAsync(result.Principal)
            ?? throw new InvalidOperationException("The user details cannot be retrieved.");

        if (!user.IsActive)
        {
            await _signInManager.SignOutAsync();
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.AccessDenied,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "This account has been deactivated.",
                }));
        }

        // An account that never went through the registration form — an external
        // sign-up, or one created before these fields existed — has no country, and
        // therefore no currency to show amounts in. Collect it before issuing a token.
        // Enforced here rather than in the external-login callback so that holding a
        // session cookie is not a way around it.
        if (string.IsNullOrWhiteSpace(user.Country))
        {
            var resumeUrl = Request.PathBase + Request.Path + QueryString.Create(
                Request.HasFormContentType ? Request.Form : Request.Query);

            return Redirect(QueryHelpers.AddQueryString(
                "/Account/CompleteProfile", "returnUrl", resumeUrl));
        }

        var identity = await BuildIdentityAsync(user, request.GetScopes());

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("~/connect/token"), IgnoreAntiforgeryToken, Produces("application/json")]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            return await ExchangeCodeOrRefreshTokenAsync(request);
        }

        if (request.IsTokenExchangeGrantType())
        {
            return await _tokenExchangeHandler.HandleAsync(HttpContext, request);
        }

        throw new InvalidOperationException("The specified grant type is not supported.");
    }

    private async Task<IActionResult> ExchangeCodeOrRefreshTokenAsync(OpenIddictRequest request)
    {
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var userId = result.Principal?.GetClaim(Claims.Subject);
        var user = userId is null ? null : await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return Reject(Errors.InvalidGrant, "The token is no longer valid.");
        }

        // Re-checked on every refresh: a deactivated user must not be able to keep
        // trading a refresh token they already hold for fresh access tokens.
        if (!user.IsActive)
        {
            return Reject(Errors.InvalidGrant, "This account has been deactivated.");
        }

        var identity = await BuildIdentityAsync(user, result.Principal!.GetScopes());

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("~/connect/logout"), HttpPost("~/connect/logout"), IgnoreAntiforgeryToken]
    public async Task<IActionResult> Logout()
    {
        // Dropping the cookie alone leaves every refresh token already issued to this
        // user still redeemable, so "log out" would not actually end the session —
        // anything holding one could mint fresh access tokens indefinitely.
        var user = await _userManager.GetUserAsync(User);
        if (user is not null)
        {
            await foreach (var token in _tokenManager.FindBySubjectAsync(user.Id.ToString()))
            {
                await _tokenManager.TryRevokeAsync(token);
            }
        }

        await _signInManager.SignOutAsync();

        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties { RedirectUri = "/" });
    }

    [HttpGet("~/connect/userinfo"), HttpPost("~/connect/userinfo"), Produces("application/json")]
    [Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    public async Task<IActionResult> UserInfo()
    {
        var user = await _userManager.FindByIdAsync(User.GetClaim(Claims.Subject)!);
        if (user is null)
        {
            return Reject(Errors.InvalidToken, "The token is no longer valid.");
        }

        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [Claims.Subject] = user.Id.ToString(),
            [Claims.Email] = user.Email ?? string.Empty,
            [Claims.Name] = user.DisplayName,
            [Claims.Role] = await _userManager.GetRolesAsync(user),
        };

        if (!string.IsNullOrWhiteSpace(user.Country))
        {
            claims[AppClaims.Country] = user.Country;
            var currency = Countries.CurrencyFor(user.Country);
            if (currency is not null)
            {
                claims[AppClaims.Currency] = currency;
            }
        }

        var impersonatedBy = User.GetClaim(AppClaims.ImpersonatedBy);
        if (!string.IsNullOrEmpty(impersonatedBy))
        {
            claims[AppClaims.ImpersonatedBy] = impersonatedBy;
        }

        return Ok(claims);
    }

    private async Task<ClaimsIdentity> BuildIdentityAsync(ApplicationUser user, IEnumerable<string> requestedScopes)
    {
        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType,
            Claims.Name,
            Claims.Role);

        identity
            .SetClaim(Claims.Subject, user.Id.ToString())
            .SetClaim(Claims.Email, user.Email)
            .SetClaim(Claims.Name, user.DisplayName)
            .SetClaims(Claims.Role, (await _userManager.GetRolesAsync(user)).ToImmutableArray());

        // Carried on the token so the app can format money the moment it loads,
        // with no extra call. Null for accounts predating the country field.
        if (!string.IsNullOrWhiteSpace(user.Country))
        {
            identity.SetClaim(AppClaims.Country, user.Country);
            identity.SetClaim(AppClaims.Currency, Countries.CurrencyFor(user.Country));
        }

        identity.SetScopes(requestedScopes);
        identity.SetResources(await _scopeManager.ListResourcesAsync(identity.GetScopes()).ToListAsync());
        identity.SetDestinations(TokenDestinations.Resolve);

        return identity;
    }

    private IActionResult Reject(string error, string description) => Forbid(
        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
        properties: new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
        }));
}
