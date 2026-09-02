namespace ExpenseTracker019.Api.Models;

/// <summary>
/// Whether money put out is an investment or a loan to someone else.
/// </summary>
/// <remarks>
/// The arithmetic is identical — money goes out, money comes back, and the ring tracks how
/// much of it you have recouped — so both live on one entity rather than in two near-copies
/// of the same screen. Only the wording and the grouping follow this.
///
/// `Investment` is 0 so that existing rows, written before this column, land on it.
/// </remarks>
public enum InvestmentKind
{
    /// <summary>Capital put into something.</summary>
    Investment = 0,

    /// <summary>Money lent to someone, expected back.</summary>
    Lend = 1,
}

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

    public InvestmentKind Kind { get; set; }

    /// <summary>
    /// Who you lent it to. Only meaningful for <see cref="InvestmentKind.Lend"/>; an
    /// investment has no counterparty worth naming beyond its own name.
    /// </summary>
    public string? Counterparty { get; set; }

    /// <summary>Why you put money in, or why you lent it. Free text.</summary>
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
