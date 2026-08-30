using ExpenseTracker019.Api.Data;
using ExpenseTracker019.Api.Dtos.Reports;
using ExpenseTracker019.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker019.Api.Services;

public class ReportService : IReportService
{
    private readonly AppDbContext _db;
    private readonly IMonthCycleService _monthCycleService;

    public ReportService(AppDbContext db, IMonthCycleService monthCycleService)
    {
        _db = db;
        _monthCycleService = monthCycleService;
    }

    public async Task<PeriodSummaryDto> GetPeriodSummaryAsync(Guid userId, Guid periodId)
    {
        var period = await _monthCycleService.GetPeriodByIdAsync(userId, periodId);

        // Filters are ignored so an archived category/head that still holds this period's
        // spending or budget stays in the report; purely inactive ones are dropped below.
        var categories = await _db.Categories
            .IgnoreQueryFilters()
            .Include(c => c.Heads)
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .ToListAsync();

        var categoryBudgets = await _db.CategoryBudgets
            .Where(cb => cb.BudgetPeriodId == periodId)
            .ToDictionaryAsync(cb => cb.CategoryId, cb => cb.Amount);

        var headBudgets = await _db.HeadBudgets
            .Where(hb => hb.BudgetPeriodId == periodId)
            .ToDictionaryAsync(hb => hb.HeadId, hb => hb.Amount);

        var spentByHead = await _db.Expenses
            .IgnoreQueryFilters()
            .Where(e => e.UserId == userId
                        && e.ExpenseDate >= period.StartDate
                        && e.ExpenseDate <= period.EndDate)
            .GroupBy(e => e.HeadId)
            .Select(g => new { HeadId = g.Key, Amount = g.Sum(e => e.Amount) })
            .ToDictionaryAsync(x => x.HeadId, x => x.Amount);

        var earnedByHead = await _db.Incomes
            .IgnoreQueryFilters()
            .Where(i => i.UserId == userId
                        && i.IncomeDate >= period.StartDate
                        && i.IncomeDate <= period.EndDate)
            .GroupBy(i => i.HeadId)
            .Select(g => new { HeadId = g.Key, Amount = g.Sum(i => i.Amount) })
            .ToDictionaryAsync(x => x.HeadId, x => x.Amount);

        var summary = new PeriodSummaryDto
        {
            PeriodId = period.Id,
            PeriodLabel = period.Label,
            StartDate = period.StartDate,
            EndDate = period.EndDate,
        };

        summary.Categories = BuildCategories(
            categories.Where(c => c.Kind == CategoryKind.Expense),
            spentByHead,
            categoryBudgets,
            headBudgets);

        // Income carries no budgets, so the same builder runs with empty budget maps.
        summary.IncomeCategories = BuildCategories(
            categories.Where(c => c.Kind == CategoryKind.Income),
            earnedByHead,
            categoryBudgets: new Dictionary<Guid, decimal>(),
            headBudgets: new Dictionary<Guid, decimal>());

        summary.TotalBudget = summary.Categories.Sum(c => c.Budget ?? 0m);
        summary.TotalSpent = summary.Categories.Sum(c => c.Spent);
        summary.TotalRemaining = summary.TotalBudget - summary.TotalSpent;

        summary.TotalIncome = summary.IncomeCategories.Sum(c => c.Spent);
        summary.TotalSaved = summary.TotalIncome - summary.TotalSpent;

        return summary;
    }

    /// <summary>
    /// Rolls a ledger's categories up from their heads. <paramref name="amountByHead"/> is
    /// spending on the expense side and income on the other; the budget maps are empty for
    /// income, which leaves every budget field null exactly as it should be.
    /// </summary>
    private static List<CategorySummaryDto> BuildCategories(
        IEnumerable<Category> categories,
        IReadOnlyDictionary<Guid, decimal> amountByHead,
        IReadOnlyDictionary<Guid, decimal> categoryBudgets,
        IReadOnlyDictionary<Guid, decimal> headBudgets)
    {
        var result = new List<CategorySummaryDto>();

        foreach (var category in categories)
        {
            var heads = category.Heads
                .OrderBy(h => h.DisplayOrder).ThenBy(h => h.Name)
                .Select(h =>
                {
                    var budget = headBudgets.TryGetValue(h.Id, out var hb) ? hb : (decimal?)null;
                    var amount = amountByHead.TryGetValue(h.Id, out var a) ? a : 0m;
                    return new HeadSummaryDto
                    {
                        HeadId = h.Id,
                        HeadName = h.Name,
                        IsArchived = h.IsArchived,
                        Budget = budget,
                        Spent = amount,
                        Remaining = budget is null ? null : budget - amount,
                        IsOverBudget = budget is not null && amount > budget,
                    };
                })
                .Where(h => !h.IsArchived || h.Spent > 0 || h.Budget is not null)
                .ToList();

            var categoryBudget = categoryBudgets.TryGetValue(category.Id, out var cb) ? cb : (decimal?)null;
            var categoryAmount = heads.Sum(h => h.Spent);

            var isRelevant = !category.IsArchived || categoryAmount > 0 || categoryBudget is not null;
            if (!isRelevant)
            {
                continue;
            }

            result.Add(new CategorySummaryDto
            {
                CategoryId = category.Id,
                CategoryName = category.Name,
                IsArchived = category.IsArchived,
                Budget = categoryBudget,
                Spent = categoryAmount,
                Remaining = categoryBudget is null ? null : categoryBudget - categoryAmount,
                IsOverBudget = categoryBudget is not null && categoryAmount > categoryBudget,
                Heads = heads,
            });
        }

        return result;
    }
}
