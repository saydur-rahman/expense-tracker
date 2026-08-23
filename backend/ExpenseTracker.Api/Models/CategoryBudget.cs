namespace ExpenseTracker.Api.Models;

public class CategoryBudget
{
    public Guid Id { get; set; }

    public Guid BudgetPeriodId { get; set; }
    public BudgetPeriod BudgetPeriod { get; set; } = null!;

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
