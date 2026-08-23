namespace ExpenseTracker019.Api.Models;

/// <summary>
/// Append-only, effective-dated list of a user's month-cycle start day.
/// Only the start day is stored; the cycle end is always "the day before the next start,"
/// with roll-over handled at period-resolution time. Kept append-only (never mutated in place)
/// so changing the cycle later does not retroactively shift already-resolved BudgetPeriods.
/// </summary>
public class UserMonthCycleSetting
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public int StartDay { get; set; } = 1;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
