namespace Auth019;

public static class AppRoles
{
    public const string User = "User";
    public const string Admin = "Admin";

    public static readonly string[] All = { User, Admin };
}

/// <summary>
/// OAuth scopes. Read and write are separate so an impersonation token can be
/// issued with read only — the resource server then enforces it as an ordinary
/// scope check rather than needing to know what impersonation is.
/// </summary>
public static class AppScopes
{
    public const string ExpenseRead = "expense.read";
    public const string ExpenseWrite = "expense.write";

    /// <summary>Grants access to Auth019's own user-administration API.</summary>
    public const string AuthAdmin = "auth.admin";

    public static readonly string[] All = { ExpenseRead, ExpenseWrite, AuthAdmin };
}

public static class AppClaims
{
    /// <summary>User id of the admin who started an impersonation session.</summary>
    public const string ImpersonatedBy = "imp_by";

    /// <summary>The user's ISO 3166-1 alpha-2 country.</summary>
    public const string Country = "country";

    /// <summary>
    /// ISO 4217 currency, derived from the country rather than stored — so the app
    /// can format every amount without a round trip, and changing country changes it.
    /// </summary>
    public const string Currency = "currency";
}

public static class AppClients
{
    /// <summary>The React SPA — a public client using authorization code + PKCE.</summary>
    public const string Spa = "expensetracker019-spa";
}

public static class AppGrantTypes
{
    /// <summary>RFC 8693 token exchange, used to mint read-only impersonation tokens.</summary>
    public const string TokenExchange = "urn:ietf:params:oauth:grant-type:token-exchange";
}

public static class AppTokenExchange
{
    public const string SubjectTokenParameter = "subject_token";
    public const string SubjectTokenTypeParameter = "subject_token_type";
    public const string RequestedSubjectParameter = "requested_subject";
    public const string AccessTokenType = "urn:ietf:params:oauth:token-type:access_token";
}

public static class AuthPolicies
{
    public const string AuthAdmin = "AuthAdmin";
}

public static class CorsPolicies
{
    public const string Spa = "Spa";
}
