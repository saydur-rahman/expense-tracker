namespace ExpenseTracker019.Api.Models;

/// <summary>
/// Append-only, effective-dated record of how a user budgets: monthly from a chosen day of
/// the month, or weekly from a chosen day of the week. Only the start is stored; the end is
/// always "the day before the next start," with roll-over handled at period-resolution time.
/// Kept append-only (never mutated in place) so changing the cycle later does not
/// retroactively shift already-resolved BudgetPeriods.
/// </summary>
public class UserMonthCycleSetting
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public PeriodKind PeriodKind { get; set; } = PeriodKind.Month;

    /// <summary>Day of the month the cycle begins. Meaningful only when monthly.</summary>
    public int StartDay { get; set; } = 1;

    /// <summary>Day of the week the cycle begins. Meaningful only when weekly.</summary>
    public DayOfWeek WeekStartsOn { get; set; } = DayOfWeek.Monday;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
