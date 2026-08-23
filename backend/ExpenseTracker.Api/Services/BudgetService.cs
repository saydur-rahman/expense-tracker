using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos.Budgets;
using ExpenseTracker.Api.Exceptions;
using ExpenseTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services;

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

        var allocatedToHeads = await SumHeadBudgetsAsync(periodId, categoryId, excludingHeadId: null);
        if (amount < allocatedToHeads)
        {
            throw new ValidationAppException(
                $"This category's heads are already budgeted {allocatedToHeads:0.##} for this month. " +
                $"Lower the head budgets before setting the category budget below that.");
        }

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
        var head = await GetOwnedHeadAsync(userId, headId);

        await using var tx = await _db.Database.BeginTransactionAsync();

        var categoryBudget = await _db.CategoryBudgets
            .FirstOrDefaultAsync(cb => cb.BudgetPeriodId == periodId && cb.CategoryId == head.CategoryId);

        if (categoryBudget is null)
        {
            throw new ValidationAppException(
                "Set a budget for the category first — head budgets are bounded by it.");
        }

        var otherHeadsTotal = await SumHeadBudgetsAsync(periodId, head.CategoryId, excludingHeadId: headId);
        var proposedTotal = otherHeadsTotal + amount;

        if (proposedTotal > categoryBudget.Amount)
        {
            var remaining = categoryBudget.Amount - otherHeadsTotal;
            throw new ValidationAppException(
                $"That would put this category's heads at {proposedTotal:0.##}, over its {categoryBudget.Amount:0.##} budget. " +
                $"At most {remaining:0.##} is left for this head.");
        }

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

        var categoryBudget = await _db.CategoryBudgets
            .FirstOrDefaultAsync(cb => cb.BudgetPeriodId == periodId && cb.CategoryId == categoryId);
        if (categoryBudget is not null)
        {
            _db.CategoryBudgets.Remove(categoryBudget);
        }

        // Head budgets only exist within a category budget's bounds, so clearing the
        // category's budget for this month clears its heads' budgets for the same month
        // rather than leaving them unbounded.
        var headBudgets = await _db.HeadBudgets
            .Where(hb => hb.BudgetPeriodId == periodId && hb.Head.CategoryId == categoryId)
            .ToListAsync();
        _db.HeadBudgets.RemoveRange(headBudgets);

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
            await _db.SaveChangesAsync();
        }

        return await BuildAsync(userId, period);
    }

    private async Task<decimal> SumHeadBudgetsAsync(Guid periodId, Guid categoryId, Guid? excludingHeadId)
    {
        var query = _db.HeadBudgets
            .Where(hb => hb.BudgetPeriodId == periodId && hb.Head.CategoryId == categoryId);

        if (excludingHeadId is not null)
        {
            query = query.Where(hb => hb.HeadId != excludingHeadId.Value);
        }

        return await query.SumAsync(hb => (decimal?)hb.Amount) ?? 0m;
    }

    private async Task EnsureCategoryOwnedAsync(Guid userId, Guid categoryId)
    {
        var exists = await _db.Categories.AnyAsync(c => c.Id == categoryId && c.UserId == userId);
        if (!exists)
        {
            throw new NotFoundAppException("Category not found.");
        }
    }

    private async Task<Head> GetOwnedHeadAsync(Guid userId, Guid headId)
        => await _db.Heads.FirstOrDefaultAsync(h => h.Id == headId && h.Category.UserId == userId)
           ?? throw new NotFoundAppException("Head not found.");

    private async Task<PeriodBudgetsDto> BuildAsync(Guid userId, BudgetPeriod period)
    {
        var categories = await _db.Categories
            .Include(c => c.Heads)
            .Where(c => c.UserId == userId)
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

            var categoryAmount = categoryBudgets.TryGetValue(category.Id, out var ca) ? ca : (decimal?)null;
            var allocated = heads.Sum(h => h.Amount ?? 0m);

            dto.Categories.Add(new CategoryBudgetDto
            {
                CategoryId = category.Id,
                CategoryName = category.Name,
                Amount = categoryAmount,
                AllocatedToHeads = allocated,
                Unallocated = categoryAmount is null ? null : categoryAmount - allocated,
                Heads = heads,
            });
        }

        return dto;
    }
}
