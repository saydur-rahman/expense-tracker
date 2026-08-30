using ExpenseTracker019.Api.Dtos.MonthCycle;
using ExpenseTracker019.Api.Models;

namespace ExpenseTracker019.Api.Services;

public interface IMonthCycleService
{
    Task<MonthCycleDto> GetAsync(Guid userId);
    Task<MonthCycleDto> UpdateAsync(Guid userId, PeriodKind kind, int startDay, DayOfWeek weekStartsOn);

    /// <summary>Resolves (creating if needed) the BudgetPeriod containing the given date.</summary>
    Task<BudgetPeriod> ResolvePeriodContainingAsync(Guid userId, DateOnly date);

    /// <summary>Resolves (creating if needed) the period <paramref name="offset"/> cycles from today's.</summary>
    Task<BudgetPeriod> ResolveRelativePeriodAsync(Guid userId, int offset);

    /// <summary>
    /// The cycle windows from today's back to the one holding the user's earliest activity.
    /// **Computes** them — it must never create BudgetPeriod rows, because merely listing
    /// history would then write to the database and could carry budgets into months the
    /// user never budgeted.
    /// </summary>
    Task<IReadOnlyList<PeriodWindowDto>> ListRecentWindowsAsync(Guid userId, int max);

    Task<BudgetPeriod> GetPeriodByIdAsync(Guid userId, Guid periodId);
    Task<IReadOnlyList<BudgetPeriod>> ListPeriodsAsync(Guid userId);
}
