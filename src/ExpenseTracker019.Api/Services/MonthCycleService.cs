using ExpenseTracker019.Api.Data;
using ExpenseTracker019.Api.Dtos.MonthCycle;
using ExpenseTracker019.Api.Exceptions;
using ExpenseTracker019.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker019.Api.Services;

public class MonthCycleService : IMonthCycleService
{
    private const int DefaultStartDay = 1;
    private const PeriodKind DefaultKind = PeriodKind.Month;
    private const DayOfWeek DefaultWeekStart = DayOfWeek.Monday;

    /// <summary>The cycle in force for a user right now, with defaults applied.</summary>
    private sealed record Cycle(PeriodKind Kind, int StartDay, DayOfWeek WeekStartsOn)
    {
        public static readonly Cycle Default = new(DefaultKind, DefaultStartDay, DefaultWeekStart);

        public (DateOnly Start, DateOnly End) Containing(DateOnly date) => Kind == PeriodKind.Week
            ? MonthCycleMath.ResolveWeekContaining(date, WeekStartsOn)
            : MonthCycleMath.ResolvePeriodContaining(date, StartDay);

        public (DateOnly Start, DateOnly End) Shift(DateOnly start, int offset) => Kind == PeriodKind.Week
            ? MonthCycleMath.ShiftWeek(start, offset)
            : MonthCycleMath.ShiftPeriod(start, StartDay, offset);

        public string Label(DateOnly start, DateOnly end) => Kind == PeriodKind.Week
            ? MonthCycleMath.BuildWeekLabel(start, end)
            : MonthCycleMath.BuildLabel(start, end);
    }

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
            PeriodKind = setting?.PeriodKind ?? DefaultKind,
            StartDay = setting?.StartDay ?? DefaultStartDay,
            WeekStartsOn = setting?.WeekStartsOn ?? DefaultWeekStart,
            IsConfigured = setting is not null,
        };
    }

    public async Task<MonthCycleDto> UpdateAsync(Guid userId, PeriodKind kind, int startDay, DayOfWeek weekStartsOn)
    {
        // Only the field governing the chosen rhythm is validated; the other rides along
        // untouched, so switching back and forth doesn't lose the day already picked.
        if (kind == PeriodKind.Month && startDay is < 1 or > 31)
        {
            throw new ValidationAppException("Start day must be between 1 and 31.");
        }

        if (kind == PeriodKind.Week && !Enum.IsDefined(weekStartsOn))
        {
            throw new ValidationAppException("Pick a day of the week for your budget to start on.");
        }

        // Append-only: a new effective-dated row, so already-resolved periods keep their boundaries.
        _db.UserMonthCycleSettings.Add(new UserMonthCycleSetting
        {
            UserId = userId,
            PeriodKind = kind,
            StartDay = startDay,
            WeekStartsOn = weekStartsOn,
            EffectiveFromUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        return new MonthCycleDto
        {
            PeriodKind = kind,
            StartDay = startDay,
            WeekStartsOn = weekStartsOn,
            IsConfigured = true,
        };
    }

    public async Task<BudgetPeriod> ResolvePeriodContainingAsync(Guid userId, DateOnly date)
    {
        // Boundaries are always computed from the user's *current* cycle setting, so
        // changing the cycle re-cuts the period immediately. The stored row is only
        // an anchor for budgets to hang off; it never overrides the calculation.
        var cycle = await GetCurrentCycleAsync(userId);
        var (start, end) = cycle.Containing(date);

        // Kind belongs in the lookup, not just on the row: a week and a month can share a
        // start date, and without this the realign below would rewrite one into the other
        // and strand its budgets.
        var existing = await _db.BudgetPeriods
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Kind == cycle.Kind && p.StartDate == start);

        if (existing is not null)
        {
            // A row cut under an older setting can share this start but end elsewhere;
            // realign it rather than reporting a window the user no longer uses.
            if (existing.EndDate != end)
            {
                existing.EndDate = end;
                existing.Label = cycle.Label(start, end);
                await _db.SaveChangesAsync();
            }

            await CarryBudgetsForwardAsync(userId, existing);
            return existing;
        }

        var created = await CreatePeriodAsync(userId, cycle, start, end);
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

        var cycle = await GetCurrentCycleAsync(userId);
        var (start, _) = cycle.Shift(current.StartDate, offset);

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
    /// Gives a period the previous one's budgets, so figures set once keep applying
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

        // Head budgets can stand alone now, so either kind of row counts as "already budgeted".
        var alreadyBudgeted =
            await _db.CategoryBudgets.AnyAsync(cb => cb.BudgetPeriodId == period.Id)
            || await _db.HeadBudgets.AnyAsync(hb => hb.BudgetPeriodId == period.Id);

        if (alreadyBudgeted)
        {
            // Nothing to carry, and nothing should ever overwrite it.
            period.BudgetsInitialized = true;
            await _db.SaveChangesAsync();
            return;
        }

        // Same rhythm only. A month's figures landing in a week (or the reverse) would be
        // an amount the user never chose, so a freshly switched cadence starts empty and
        // fills forward from the first period budgeted under it.
        var source = await _db.BudgetPeriods
            .Where(p => p.UserId == userId
                        && p.Kind == period.Kind
                        && p.StartDate < period.StartDate
                        && (p.CategoryBudgets.Any() || p.HeadBudgets.Any()))
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

        // Head budgets carry independently of the category target: a user who only ever fills
        // in heads has no category rows to gate them on, and gating would silently drop their
        // whole budget on the turn of the period.
        var activeHeadIds = await _db.Heads
            .Where(h => activeCategoryIds.Contains(h.CategoryId))
            .Select(h => h.Id)
            .ToListAsync();

        var headBudgets = await _db.HeadBudgets
            .Where(hb => hb.BudgetPeriodId == source.Id && activeHeadIds.Contains(hb.HeadId))
            .ToListAsync();

        if (categoryBudgets.Count == 0 && headBudgets.Count == 0)
        {
            // Everything the source held belonged to categories since archived.
            return;
        }

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

    private async Task<Cycle> GetCurrentCycleAsync(Guid userId)
    {
        var setting = await GetCurrentSettingAsync(userId);
        return setting is null
            ? Cycle.Default
            : new Cycle(setting.PeriodKind, setting.StartDay, setting.WeekStartsOn);
    }

    private Task<UserMonthCycleSetting?> GetCurrentSettingAsync(Guid userId)
        => _db.UserMonthCycleSettings
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.EffectiveFromUtc)
            .FirstOrDefaultAsync();

    private async Task<BudgetPeriod> CreatePeriodAsync(Guid userId, Cycle cycle, DateOnly start, DateOnly end)
    {
        var period = new BudgetPeriod
        {
            UserId = userId,
            Kind = cycle.Kind,
            StartDate = start,
            EndDate = end,
            Label = cycle.Label(start, end),
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
                .FirstAsync(p => p.UserId == userId && p.Kind == cycle.Kind && p.StartDate == start);
            return existing;
        }

        return period;
    }
}
