namespace ExpenseTracker019.Api.Models;

/// <summary>
/// Money coming in, recorded against a head of an <see cref="CategoryKind.Income"/>
/// category. Deliberately a mirror of <see cref="Expense"/> — same shape, no budget.
/// </summary>
public class Income
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid HeadId { get; set; }
    public Head Head { get; set; } = null!;

    public decimal Amount { get; set; }
    public DateOnly IncomeDate { get; set; }
    public string? Note { get; set; }

    /// <summary>
    /// Set when this row is the money a loan handed over — the one ledger row a
    /// <see cref="Models.Loan"/> owns. <c>LoanService</c> writes it, keeps it matching
    /// the loan's amount and date, and takes it away with the loan; the income screen
    /// refuses to edit or delete it so the two cannot drift apart.
    /// </summary>
    public Guid? LoanId { get; set; }
    public Loan? Loan { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
