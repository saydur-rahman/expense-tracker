namespace ExpenseTracker019.Api.Services;

/// <summary>
/// Pure date math for resolving a user's custom month cycle into concrete period boundaries.
/// Kept free of EF/DB concerns so the edge cases (short-month roll-over, year wrap) are unit-testable.
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
}
