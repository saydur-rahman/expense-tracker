namespace ExpenseTracker019.Api;

/// <summary>
/// Scopes issued by Auth019. Read and write are separate so a read-only
/// impersonation token is enforced here as an ordinary scope check — this API
/// needs no concept of impersonation at all.
/// </summary>
public static class AppScopes
{
    public const string ExpenseRead = "expense.read";
    public const string ExpenseWrite = "expense.write";
}

public static class AuthPolicies
{
    /// <summary>Required to read expense data.</summary>
    public const string ExpenseRead = "ExpenseRead";

    /// <summary>Required for anything that changes data. Impersonation tokens lack it.</summary>
    public const string ExpenseWrite = "ExpenseWrite";
}

public static class CorsPolicies
{
    public const string Spa = "Spa";
}
