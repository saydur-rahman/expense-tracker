using ExpenseTracker019.Api.Data;
using ExpenseTracker019.Api.Dtos.MonthCycle;
using ExpenseTracker019.Api.Exceptions;
using ExpenseTracker019.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker019.Api.Services;

public class MonthCycleService : IMonthCycleService
{
    private const int DefaultStartDay = 1;

    private readonly AppDbContext _db;

    public MonthCycleService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<MonthCycleDto> GetAsync(Guid userId)
    {
        var setting = await GetCurrentSettingAsync(userId);
        return new MonthCycleDto
        {
            StartDay = setting?.StartDay ?? DefaultStartDay,
            IsConfigured = setting is not null,
        };
    }

    public async Task<MonthCycleDto> UpdateAsync(Guid userId, int startDay)
    {
        if (startDay is < 1 or > 31)
        {
            throw new ValidationAppException("Start day must be between 1 and 31.");
        }

        // Append-only: a new effective-dated row, so already-resolved periods keep their boundaries.
        _db.UserMonthCycleSettings.Add(new UserMonthCycleSetting
        {
            UserId = userId,
            StartDay = startDay,
            EffectiveFromUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        return new MonthCycleDto { StartDay = startDay, IsConfigured = true };
    }

    public async Task<BudgetPeriod> ResolvePeriodContainingAsync(Guid userId, DateOnly date)
    {
        // Boundaries are always computed from the user's *current* cycle setting, so
        // changing the start day re-cuts the month immediately. The stored row is only
        // an anchor for budgets to hang off; it never overrides the calculation.
        var startDay = (await GetCurrentSettingAsync(userId))?.StartDay ?? DefaultStartDay;
        var (start, end) = MonthCycleMath.ResolvePeriodContaining(date, startDay);

        var existing = await _db.BudgetPeriods
            .FirstOrDefaultAsync(p => p.UserId == userId && p.StartDate == start);

        if (existing is not null)
        {
            // A row cut under an older setting can share this start but end elsewhere;
            // realign it rather than reporting a window the user no longer uses.
            if (existing.EndDate != end)
            {
                existing.EndDate = end;
                existing.Label = MonthCycleMath.BuildLabel(start, end);
                await _db.SaveChangesAsync();
            }

            await CarryBudgetsForwardAsync(userId, existing);
            return existing;
        }

        var created = await CreatePeriodAsync(userId, start, end);
        await CarryBudgetsForwardAsync(userId, created);
        return created;
    }

    public async Task<BudgetPeriod> ResolveRelativePeriodAsync(Guid userId, int offset)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var current = await ResolvePeriodContainingAsync(userId, today);

        if (offset == 0)
        {
            return current;
        }

        var startDay = (await GetCurrentSettingAsync(userId))?.StartDay ?? DefaultStartDay;
        var (start, _) = MonthCycleMath.ShiftPeriod(current.StartDate, startDay, offset);

        return await ResolvePeriodContainingAsync(userId, start);
    }

    public async Task<BudgetPeriod> GetPeriodByIdAsync(Guid userId, Guid periodId)
    {
        var period = await _db.BudgetPeriods
            .FirstOrDefaultAsync(p => p.Id == periodId && p.UserId == userId)
            ?? throw new NotFoundAppException("Budget period not found.");

        await CarryBudgetsForwardAsync(userId, period);
        return period;
    }

    public async Task<IReadOnlyList<BudgetPeriod>> ListPeriodsAsync(Guid userId)
        => await _db.BudgetPeriods
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();

    /// <summary>
    /// Gives a month the previous month's budgets, so figures set once keep applying
    /// until the user changes them.
    /// </summary>
    /// <remarks>
    /// Three properties keep this predictable:
    /// it only ever reads <em>backwards</em>, so browsing into history never rewrites it;
    /// it never touches a period that already holds budgets;
    /// and it runs at most once per period, because a month the user has edited — or
    /// deliberately emptied — is flagged and left alone from then on.
    /// </remarks>
    private async Task CarryBudgetsForwardAsync(Guid userId, BudgetPeriod period)
    {
        if (period.BudgetsInitialized)
        {
            return;
        }

        if (await _db.CategoryBudgets.AnyAsync(cb => cb.BudgetPeriodId == period.Id))
        {
            // Already budgeted — nothing to carry, and nothing should ever overwrite it.
            period.BudgetsInitialized = true;
            await _db.SaveChangesAsync();
            return;
        }

        var source = await _db.BudgetPeriods
            .Where(p => p.UserId == userId
                        && p.StartDate < period.StartDate
                        && p.CategoryBudgets.Any())
            .OrderByDescending(p => p.StartDate)
            .FirstOrDefaultAsync();

        if (source is null)
        {
            // No budgeted month behind this one yet. Leave the flag clear so the first
            // month the user budgets can still flow forward into this one later.
            return;
        }

        // The global query filters make this the set of *active* categories, so an
        // archived one is quietly dropped instead of being carried forward forever.
        var activeCategoryIds = await _db.Categories
            .Where(c => c.UserId == userId && c.Kind == CategoryKind.Expense)
            .Select(c => c.Id)
            .ToListAsync();

        var categoryBudgets = await _db.CategoryBudgets
            .Where(cb => cb.BudgetPeriodId == source.Id && activeCategoryIds.Contains(cb.CategoryId))
            .ToListAsync();

        if (categoryBudgets.Count == 0)
        {
            return;
        }

        // Head budgets ride along only under a category whose budget came across too.
        // That is what keeps "a head budget needs a category budget" and "heads never
        // exceed their category" true by construction: the pair held in the source
        // month, and dropping an archived head only ever lowers the head total.
        var copiedCategoryIds = categoryBudgets.Select(cb => cb.CategoryId).ToList();

        var eligibleHeadIds = await _db.Heads
            .Where(h => copiedCategoryIds.Contains(h.CategoryId))
            .Select(h => h.Id)
            .ToListAsync();

        var headBudgets = await _db.HeadBudgets
            .Where(hb => hb.BudgetPeriodId == source.Id && eligibleHeadIds.Contains(hb.HeadId))
            .ToListAsync();

        var now = DateTime.UtcNow;

        _db.CategoryBudgets.AddRange(categoryBudgets.Select(cb => new CategoryBudget
        {
            BudgetPeriodId = period.Id,
            CategoryId = cb.CategoryId,
            Amount = cb.Amount,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        }));

        _db.HeadBudgets.AddRange(headBudgets.Select(hb => new HeadBudget
        {
            BudgetPeriodId = period.Id,
            HeadId = hb.HeadId,
            Amount = hb.Amount,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        }));

        period.BudgetsInitialized = true;
        await _db.SaveChangesAsync();
    }

    private Task<UserMonthCycleSetting?> GetCurrentSettingAsync(Guid userId)
        => _db.UserMonthCycleSettings
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.EffectiveFromUtc)
            .FirstOrDefaultAsync();

    private async Task<BudgetPeriod> CreatePeriodAsync(Guid userId, DateOnly start, DateOnly end)
    {
        var period = new BudgetPeriod
        {
            UserId = userId,
            StartDate = start,
            EndDate = end,
            Label = MonthCycleMath.BuildLabel(start, end),
        };

        _db.BudgetPeriods.Add(period);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Concurrent request created the same period first; fall back to the stored one.
            _db.Entry(period).State = EntityState.Detached;
            var existing = await _db.BudgetPeriods
                .FirstAsync(p => p.UserId == userId && p.StartDate == start);
            return existing;
        }

        return period;
    }
}
