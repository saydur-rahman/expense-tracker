using ExpenseTracker.Api.Dtos.Budgets;

namespace ExpenseTracker.Api.Services;

public interface IBudgetService
{
    Task<PeriodBudgetsDto> GetPeriodBudgetsAsync(Guid userId, Guid periodId);
    Task<PeriodBudgetsDto> SetCategoryBudgetAsync(Guid userId, Guid periodId, Guid categoryId, decimal amount);
    Task<PeriodBudgetsDto> SetHeadBudgetAsync(Guid userId, Guid periodId, Guid headId, decimal amount);
    Task<PeriodBudgetsDto> ClearCategoryBudgetAsync(Guid userId, Guid periodId, Guid categoryId);
    Task<PeriodBudgetsDto> ClearHeadBudgetAsync(Guid userId, Guid periodId, Guid headId);
}
