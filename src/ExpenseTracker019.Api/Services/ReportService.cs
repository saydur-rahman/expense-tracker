using ExpenseTracker019.Api.Data;
using ExpenseTracker019.Api.Dtos.Reports;
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
            .Select(g => new { HeadId = g.Key, Spent = g.Sum(e => e.Amount) })
            .ToDictionaryAsync(x => x.HeadId, x => x.Spent);

        var summary = new PeriodSummaryDto
        {
            PeriodId = period.Id,
            PeriodLabel = period.Label,
            StartDate = period.StartDate,
            EndDate = period.EndDate,
        };

        foreach (var category in categories)
        {
            var heads = category.Heads
                .OrderBy(h => h.DisplayOrder).ThenBy(h => h.Name)
                .Select(h =>
                {
                    var budget = headBudgets.TryGetValue(h.Id, out var hb) ? hb : (decimal?)null;
                    var spent = spentByHead.TryGetValue(h.Id, out var s) ? s : 0m;
                    return new HeadSummaryDto
                    {
                        HeadId = h.Id,
                        HeadName = h.Name,
                        IsArchived = h.IsArchived,
                        Budget = budget,
                        Spent = spent,
                        Remaining = budget is null ? null : budget - spent,
                        IsOverBudget = budget is not null && spent > budget,
                    };
                })
                .Where(h => !h.IsArchived || h.Spent > 0 || h.Budget is not null)
                .ToList();

            var categoryBudget = categoryBudgets.TryGetValue(category.Id, out var cb) ? cb : (decimal?)null;
            var categorySpent = heads.Sum(h => h.Spent);

            var isRelevant = !category.IsArchived || categorySpent > 0 || categoryBudget is not null;
            if (!isRelevant)
            {
                continue;
            }

            summary.Categories.Add(new CategorySummaryDto
            {
                CategoryId = category.Id,
                CategoryName = category.Name,
                IsArchived = category.IsArchived,
                Budget = categoryBudget,
                Spent = categorySpent,
                Remaining = categoryBudget is null ? null : categoryBudget - categorySpent,
                IsOverBudget = categoryBudget is not null && categorySpent > categoryBudget,
                Heads = heads,
            });
        }

        summary.TotalBudget = summary.Categories.Sum(c => c.Budget ?? 0m);
        summary.TotalSpent = summary.Categories.Sum(c => c.Spent);
        summary.TotalRemaining = summary.TotalBudget - summary.TotalSpent;

        return summary;
    }
}
