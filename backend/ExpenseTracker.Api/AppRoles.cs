namespace ExpenseTracker.Api;

public static class AppRoles
{
    public const string User = "User";
    public const string Admin = "Admin";

    public static readonly string[] All = { User, Admin };
}

public static class AppClaims
{
    /// <summary>User id of the admin who started an impersonation session.</summary>
    public const string ImpersonatedBy = "imp_by";

    /// <summary>Present ("true") on impersonation tokens; blocks all writes.</summary>
    public const string ImpersonationReadOnly = "imp_readonly";
}
