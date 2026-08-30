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

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
