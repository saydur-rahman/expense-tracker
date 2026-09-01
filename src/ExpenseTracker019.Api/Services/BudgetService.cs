using ExpenseTracker019.Api.Data;
using ExpenseTracker019.Api.Dtos.Budgets;
using ExpenseTracker019.Api.Exceptions;
using ExpenseTracker019.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker019.Api.Services;

public class BudgetService : IBudgetService
{
    private readonly AppDbContext _db;
    private readonly IMonthCycleService _monthCycleService;

    public BudgetService(AppDbContext db, IMonthCycleService monthCycleService)
    {
        _db = db;
        _monthCycleService = monthCycleService;
    }

    public async Task<PeriodBudgetsDto> GetPeriodBudgetsAsync(Guid userId, Guid periodId)
    {
        var period = await _monthCycleService.GetPeriodByIdAsync(userId, periodId);
        return await BuildAsync(userId, period);
    }

    public async Task<PeriodBudgetsDto> SetCategoryBudgetAsync(Guid userId, Guid periodId, Guid categoryId, decimal amount)
    {
        if (amount < 0)
        {
            throw new ValidationAppException("Budget amount cannot be negative.");
        }

        var period = await _monthCycleService.GetPeriodByIdAsync(userId, periodId);
        await EnsureCategoryOwnedAsync(userId, categoryId);

        await using var tx = await _db.Database.BeginTransactionAsync();

        // The category figure is a target, not a cap. Heads may add up to more or less than
        // it; the difference is reported back rather than refused.
        MarkBudgetsSettled(period);

        var existing = await _db.CategoryBudgets
            .FirstOrDefaultAsync(cb => cb.BudgetPeriodId == periodId && cb.CategoryId == categoryId);

        if (existing is null)
        {
            _db.CategoryBudgets.Add(new CategoryBudget
            {
                BudgetPeriodId = periodId,
                CategoryId = categoryId,
                Amount = amount,
            });
        }
        else
        {
            existing.Amount = amount;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return await BuildAsync(userId, period);
    }

    public async Task<PeriodBudgetsDto> SetHeadBudgetAsync(Guid userId, Guid periodId, Guid headId, decimal amount)
    {
        if (amount < 0)
        {
            throw new ValidationAppException("Budget amount cannot be negative.");
        }

        var period = await _monthCycleService.GetPeriodByIdAsync(userId, periodId);
        await GetOwnedHeadAsync(userId, headId);

        await using var tx = await _db.Database.BeginTransactionAsync();

        // A head stands on its own: no category budget is needed first, and nothing caps it.
        // The category is simply what its heads add up to.
        MarkBudgetsSettled(period);

        var existing = await _db.HeadBudgets
            .FirstOrDefaultAsync(hb => hb.BudgetPeriodId == periodId && hb.HeadId == headId);

        if (existing is null)
        {
            _db.HeadBudgets.Add(new HeadBudget
            {
                BudgetPeriodId = periodId,
                HeadId = headId,
                Amount = amount,
            });
        }
        else
        {
            existing.Amount = amount;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return await BuildAsync(userId, period);
    }

    public async Task<PeriodBudgetsDto> ClearCategoryBudgetAsync(Guid userId, Guid periodId, Guid categoryId)
    {
        var period = await _monthCycleService.GetPeriodByIdAsync(userId, periodId);
        await EnsureCategoryOwnedAsync(userId, categoryId);

        await using var tx = await _db.Database.BeginTransactionAsync();

        MarkBudgetsSettled(period);

        var categoryBudget = await _db.CategoryBudgets
            .FirstOrDefaultAsync(cb => cb.BudgetPeriodId == periodId && cb.CategoryId == categoryId);
        if (categoryBudget is not null)
        {
            _db.CategoryBudgets.Remove(categoryBudget);
        }

        // Heads are no longer bounded by the category, so clearing the target leaves them
        // alone — the category simply falls back to whatever its heads add up to. Clearing
        // the heads too would silently throw away figures the user entered separately.
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return await BuildAsync(userId, period);
    }

    public async Task<PeriodBudgetsDto> ClearHeadBudgetAsync(Guid userId, Guid periodId, Guid headId)
    {
        var period = await _monthCycleService.GetPeriodByIdAsync(userId, periodId);
        await GetOwnedHeadAsync(userId, headId);

        var existing = await _db.HeadBudgets
            .FirstOrDefaultAsync(hb => hb.BudgetPeriodId == periodId && hb.HeadId == headId);
        if (existing is not null)
        {
            _db.HeadBudgets.Remove(existing);
        }

        MarkBudgetsSettled(period);
        await _db.SaveChangesAsync();

        return await BuildAsync(userId, period);
    }

    /// <summary>
    /// Records that the user has set this month's budgets themselves. Carry-forward
    /// checks this flag, so a month someone deliberately emptied is never refilled
    /// from the month before it.
    /// </summary>
    private static void MarkBudgetsSettled(BudgetPeriod period) => period.BudgetsInitialized = true;

    /// <summary>
    /// Budgets exist to cap spending, so an income category can never carry one —
    /// which is also what keeps the dashboard's budget totals purely about expenses.
    /// </summary>
    private async Task EnsureCategoryOwnedAsync(Guid userId, Guid categoryId)
    {
        var category = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId)
            ?? throw new NotFoundAppException("Category not found.");

        if (category.Kind != CategoryKind.Expense)
        {
            throw new ValidationAppException("Income categories don't take a budget.");
        }
    }

    private async Task<Head> GetOwnedHeadAsync(Guid userId, Guid headId)
    {
        var head = await _db.Heads
            .Include(h => h.Category)
            .FirstOrDefaultAsync(h => h.Id == headId && h.Category.UserId == userId)
            ?? throw new NotFoundAppException("Head not found.");

        if (head.Category.Kind != CategoryKind.Expense)
        {
            throw new ValidationAppException("Income heads don't take a budget.");
        }

        return head;
    }

    private async Task<PeriodBudgetsDto> BuildAsync(Guid userId, BudgetPeriod period)
    {
        var categories = await _db.Categories
            .Include(c => c.Heads)
            .Where(c => c.UserId == userId && c.Kind == CategoryKind.Expense)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .ToListAsync();

        var categoryBudgets = await _db.CategoryBudgets
            .Where(cb => cb.BudgetPeriodId == period.Id)
            .ToDictionaryAsync(cb => cb.CategoryId, cb => cb.Amount);

        var headBudgets = await _db.HeadBudgets
            .Where(hb => hb.BudgetPeriodId == period.Id)
            .ToDictionaryAsync(hb => hb.HeadId, hb => hb.Amount);

        var dto = new PeriodBudgetsDto
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
                .Select(h => new HeadBudgetDto
                {
                    HeadId = h.Id,
                    HeadName = h.Name,
                    Amount = headBudgets.TryGetValue(h.Id, out var ha) ? ha : null,
                })
                .ToList();

            var target = categoryBudgets.TryGetValue(category.Id, out var ca) ? ca : (decimal?)null;
            var allocated = heads.Sum(h => h.Amount ?? 0m);
            var anyHeadBudgeted = heads.Any(h => h.Amount is not null);

            dto.Categories.Add(new CategoryBudgetDto
            {
                CategoryId = category.Id,
                CategoryName = category.Name,
                Amount = anyHeadBudgeted ? allocated : target,
                Target = target,
                AllocatedToHeads = allocated,
                Difference = target is null || !anyHeadBudgeted ? null : allocated - target,
                Heads = heads,
            });
        }

        return dto;
    }
}
