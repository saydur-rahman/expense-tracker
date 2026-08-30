using System.ComponentModel.DataAnnotations;
using ExpenseTracker019.Api.Models;

namespace ExpenseTracker019.Api.Dtos.MonthCycle;

public class MonthCycleDto
{
    public PeriodKind PeriodKind { get; set; }

    /// <summary>Day of the month. Only governs the cycle when <see cref="PeriodKind"/> is Month.</summary>
    public int StartDay { get; set; }

    /// <summary>Day of the week. Only governs the cycle when <see cref="PeriodKind"/> is Week.</summary>
    public DayOfWeek WeekStartsOn { get; set; }

    public bool IsConfigured { get; set; }
}

public class UpdateMonthCycleRequest
{
    public PeriodKind PeriodKind { get; set; } = PeriodKind.Month;

    // Both are always sent, whichever rhythm is chosen, so the unused one is preserved
    // rather than reset — switching to weekly and back keeps the day of the month.
    [Range(1, 31)]
    public int StartDay { get; set; } = 1;

    public DayOfWeek WeekStartsOn { get; set; } = DayOfWeek.Monday;
}

public class BudgetPeriodDto
{
    public Guid Id { get; set; }
    public PeriodKind Kind { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Label { get; set; } = string.Empty;
}
