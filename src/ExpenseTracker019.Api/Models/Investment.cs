namespace ExpenseTracker019.Api.Models;

/// <summary>Which side of an investment a linked head sits on.</summary>
public enum InvestmentDirection
{
    /// <summary>Money going in — an expense head.</summary>
    Contribution = 0,

    /// <summary>Money coming back — an income head.</summary>
    Return = 1,
}

/// <summary>
/// Money put into something, and what has come back out of it.
/// </summary>
/// <remarks>
/// Unlike <see cref="Loan"/> this carries no amount of its own: **both sides are derived**.
/// What you put in is the sum of expenses on its Contribution heads; what came back is the
/// sum of income on its Return heads. Nothing to type in, nothing to keep in sync.
///
/// The two directions link to heads of two different <see cref="CategoryKind"/>s, which
/// keeps the ledgers separate exactly as rule 8 requires — a Contribution head must be
/// <see cref="CategoryKind.Expense"/> and a Return head <see cref="CategoryKind.Income"/>.
/// </remarks>
public class Investment
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Why you put money in. Free text.</summary>
    public string? Remark { get; set; }

    /// <summary>
    /// Contributions and returns are only counted from this date, for the same reason a
    /// loan's payments are counted from the day it was taken.
    /// </summary>
    public DateOnly StartedOn { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<InvestmentHead> Heads { get; set; } = new List<InvestmentHead>();
}

/// <summary>
/// A head on one side of an investment. As with <see cref="LoanHead"/>, every row on the
/// head counts, so a head may be claimed by at most one investment.
/// </summary>
public class InvestmentHead
{
    public Guid Id { get; set; }

    public Guid InvestmentId { get; set; }
    public Investment Investment { get; set; } = null!;

    public Guid HeadId { get; set; }
    public Head Head { get; set; } = null!;

    public InvestmentDirection Direction { get; set; }
}
