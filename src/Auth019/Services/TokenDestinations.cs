using System.Security.Claims;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Auth019.Services;

/// <summary>
/// Decides which claims travel in the access token versus the identity token.
/// Anything the expense API needs to authorize a request must reach the access token.
/// </summary>
public static class TokenDestinations
{
    public static IEnumerable<string> Resolve(Claim claim)
    {
        switch (claim.Type)
        {
            case Claims.Name:
            case Claims.Email:
                yield return Destinations.AccessToken;
                if (claim.Subject?.HasScope(claim.Type == Claims.Email ? Scopes.Email : Scopes.Profile) == true)
                {
                    yield return Destinations.IdentityToken;
                }
                yield break;

            case Claims.Role:
                yield return Destinations.AccessToken;
                if (claim.Subject?.HasScope(Scopes.Roles) == true)
                {
                    yield return Destinations.IdentityToken;
                }
                yield break;

            // The resource server reads this to know a session is impersonated.
            case AppClaims.ImpersonatedBy:
                yield return Destinations.AccessToken;
                yield break;

            // Security stamps are internal to Identity and must never leave the server.
            case "AspNet.Identity.SecurityStamp":
                yield break;

            default:
                yield return Destinations.AccessToken;
                yield break;
        }
    }
}
