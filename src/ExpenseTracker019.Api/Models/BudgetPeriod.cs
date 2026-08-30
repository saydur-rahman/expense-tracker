namespace ExpenseTracker019.Api.Models;

/// <summary>
/// Explicit representation of one budgeting window — a month or a week — resolved from
/// the user's UserMonthCycleSetting
/// and created lazily on first access. Budgets attach to this stable row rather than being
/// computed on the fly, so a period's boundaries never shift after the fact.
/// </summary>
public class BudgetPeriod
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// Which rhythm cut this window. Part of the natural key: a user who switches cadence can
    /// have a week and a month starting on the same day, and they must not collide or realign
    /// into each other.
    /// </summary>
    public PeriodKind Kind { get; set; } = PeriodKind.Month;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Set once this period's budgets have been settled — either carried forward from the
    /// previous period or edited by the user. Carry-forward checks this so it fills a new
    /// period exactly once: one the user deliberately emptied stays empty.
    /// </summary>
    public bool BudgetsInitialized { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<CategoryBudget> CategoryBudgets { get; set; } = new List<CategoryBudget>();
    public ICollection<HeadBudget> HeadBudgets { get; set; } = new List<HeadBudget>();
}
