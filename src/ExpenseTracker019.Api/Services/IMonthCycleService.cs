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

    Task<BudgetPeriod> GetPeriodByIdAsync(Guid userId, Guid periodId);
    Task<IReadOnlyList<BudgetPeriod>> ListPeriodsAsync(Guid userId);
}
