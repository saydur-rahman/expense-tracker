namespace ExpenseTracker019.Api.Models;

/// <summary>
/// Money borrowed from somewhere, and a view over the expenses that repay it.
/// </summary>
/// <remarks>
/// A loan owns no ledger rows of its own. <see cref="AmountTaken"/> is simply typed in —
/// borrowing is not earnings, so it never becomes an <see cref="Income"/> and never moves
/// the dashboard's figures. What has been repaid is a SUM over the expenses on the heads
/// linked through <see cref="LoanHead"/>, computed on read.
///
/// **Deliberately not stored as a running balance.** Keeping one would mean every expense
/// create, update and delete had to keep it in sync, and it would drift the first time
/// someone edited an old row.
/// </remarks>
public class Loan
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Who it is from. Free text — this app knows nothing about lenders.</summary>
    public string? Lender { get; set; }

    public decimal AmountTaken { get; set; }

    /// <summary>
    /// Payments are only counted from this date. An expense on a linked head dated before
    /// the loan existed is not a repayment of it.
    /// </summary>
    public DateOnly TakenOn { get; set; }

    /// <summary>Why it was taken. The thing you will have forgotten in a year.</summary>
    public string? Remark { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<LoanHead> Heads { get; set; } = new List<LoanHead>();
}

/// <summary>
/// A head whose spending repays a loan. Every expense on it counts, with no per-expense
/// tagging — which is why a head may be claimed by at most one loan (unique index on
/// <see cref="HeadId"/>). Two loans sharing a head would each count the same payment.
/// </summary>
public class LoanHead
{
    public Guid Id { get; set; }

    public Guid LoanId { get; set; }
    public Loan Loan { get; set; } = null!;

    public Guid HeadId { get; set; }
    public Head Head { get; set; } = null!;
}
