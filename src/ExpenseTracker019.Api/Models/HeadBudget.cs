namespace ExpenseTracker019.Api.Models;

public class HeadBudget
{
    public Guid Id { get; set; }

    public Guid BudgetPeriodId { get; set; }
    public BudgetPeriod BudgetPeriod { get; set; } = null!;

    public Guid HeadId { get; set; }
    public Head Head { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
