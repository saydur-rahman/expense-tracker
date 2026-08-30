namespace ExpenseTracker019.Api.Services;

/// <summary>
/// Pure date math for resolving a user's budgeting cycle — monthly or weekly — into concrete
/// period boundaries. Kept free of EF/DB concerns so the edge cases (short-month roll-over,
/// year wrap, week alignment) are unit-testable.
/// </summary>
public static class MonthCycleMath
{
    /// <summary>
    /// The cycle start date within a given year/month, clamped to the last day of that month
    /// so a StartDay of 31 still resolves in February.
    /// </summary>
    public static DateOnly ResolveStart(int year, int month, int startDay)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        return new DateOnly(year, month, Math.Min(startDay, daysInMonth));
    }

    /// <summary>
    /// The period (start, end inclusive) that contains <paramref name="date"/> for a user
    /// whose cycle begins on <paramref name="startDay"/>.
    /// </summary>
    public static (DateOnly Start, DateOnly End) ResolvePeriodContaining(DateOnly date, int startDay)
    {
        var thisMonthStart = ResolveStart(date.Year, date.Month, startDay);

        DateOnly start;
        if (date >= thisMonthStart)
        {
            start = thisMonthStart;
        }
        else
        {
            var prev = new DateOnly(date.Year, date.Month, 1).AddMonths(-1);
            start = ResolveStart(prev.Year, prev.Month, startDay);
        }

        var next = new DateOnly(start.Year, start.Month, 1).AddMonths(1);
        var end = ResolveStart(next.Year, next.Month, startDay).AddDays(-1);

        return (start, end);
    }

    /// <summary>
    /// Shifts a period by <paramref name="offset"/> whole cycles (negative = earlier).
    /// </summary>
    public static (DateOnly Start, DateOnly End) ShiftPeriod(DateOnly start, int startDay, int offset)
    {
        var anchor = new DateOnly(start.Year, start.Month, 1).AddMonths(offset);
        var shiftedStart = ResolveStart(anchor.Year, anchor.Month, startDay);
        return ResolvePeriodContaining(shiftedStart, startDay);
    }

    /// <summary>
    /// The week (start, end inclusive) containing <paramref name="date"/> for a user whose
    /// week begins on <paramref name="weekStartsOn"/>. Always exactly seven days — unlike the
    /// month cycle there is nothing to clamp.
    /// </summary>
    public static (DateOnly Start, DateOnly End) ResolveWeekContaining(DateOnly date, DayOfWeek weekStartsOn)
    {
        // How far back the chosen start-of-week is from this date, wrapped into 0..6.
        var daysSinceStart = ((int)date.DayOfWeek - (int)weekStartsOn + 7) % 7;
        var start = date.AddDays(-daysSinceStart);
        return (start, start.AddDays(6));
    }

    /// <summary>Shifts a week by <paramref name="offset"/> whole weeks (negative = earlier).</summary>
    public static (DateOnly Start, DateOnly End) ShiftWeek(DateOnly start, int offset)
    {
        var shifted = start.AddDays(7 * offset);
        return (shifted, shifted.AddDays(6));
    }

    public static string BuildLabel(DateOnly start, DateOnly end)
    {
        if (start.Day == 1)
        {
            return start.ToString("MMM yyyy");
        }

        return start.Year == end.Year
            ? $"{start:d MMM} – {end:d MMM yyyy}"
            : $"{start:d MMM yyyy} – {end:d MMM yyyy}";
    }

    /// <summary>
    /// A week's label. Deliberately separate from <see cref="BuildLabel"/>: that one shortens a
    /// period starting on the 1st to just "Sep 2026", which for a week would name the wrong span.
    /// </summary>
    public static string BuildWeekLabel(DateOnly start, DateOnly end)
    {
        if (start.Year != end.Year)
        {
            return $"{start:d MMM yyyy} – {end:d MMM yyyy}";
        }

        return start.Month != end.Month
            ? $"{start:d MMM} – {end:d MMM yyyy}"
            : $"{start.Day}–{end:d MMM yyyy}";
    }
}
