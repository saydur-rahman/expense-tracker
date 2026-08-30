namespace ExpenseTracker019.Api.Models;

/// <summary>
/// Explicit representation of a user's "month," resolved from their UserMonthCycleSetting
/// and created lazily on first access. Budgets attach to this stable row rather than being
/// computed on the fly, so a period's boundaries never shift after the fact.
/// </summary>
public class BudgetPeriod
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Set once this period's budgets have been settled — either carried forward from the
    /// previous month or edited by the user. Carry-forward checks this so it fills a new
    /// month exactly once: a month the user deliberately emptied stays empty.
    /// </summary>
    public bool BudgetsInitialized { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<CategoryBudget> CategoryBudgets { get; set; } = new List<CategoryBudget>();
    public ICollection<HeadBudget> HeadBudgets { get; set; } = new List<HeadBudget>();
}
