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
        // An existing period that already covers this date wins, so changing the cycle
        // start day never retroactively re-cuts periods that budgets are already attached to.
        var existing = await _db.BudgetPeriods
            .FirstOrDefaultAsync(p => p.UserId == userId && p.StartDate <= date && date <= p.EndDate);
        if (existing is not null)
        {
            return existing;
        }

        var startDay = (await GetCurrentSettingAsync(userId))?.StartDay ?? DefaultStartDay;
        var (start, end) = MonthCycleMath.ResolvePeriodContaining(date, startDay);

        return await CreatePeriodAsync(userId, start, end);
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
            .FirstOrDefaultAsync(p => p.Id == periodId && p.UserId == userId);
        return period ?? throw new NotFoundAppException("Budget period not found.");
    }

    public async Task<IReadOnlyList<BudgetPeriod>> ListPeriodsAsync(Guid userId)
        => await _db.BudgetPeriods
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();

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
