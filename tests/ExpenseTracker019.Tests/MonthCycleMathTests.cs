using ExpenseTracker019.Api.Services;

namespace ExpenseTracker019.Tests;

public class MonthCycleMathTests
{
    [Fact]
    public void CalendarMonthCycle_CoversWholeMonth()
    {
        var (start, end) = MonthCycleMath.ResolvePeriodContaining(new DateOnly(2026, 8, 15), startDay: 1);

        Assert.Equal(new DateOnly(2026, 8, 1), start);
        Assert.Equal(new DateOnly(2026, 8, 31), end);
    }

    [Fact]
    public void SalaryCycle_DateAfterStartDay_StartsThisMonth()
    {
        var (start, end) = MonthCycleMath.ResolvePeriodContaining(new DateOnly(2026, 8, 26), startDay: 25);

        Assert.Equal(new DateOnly(2026, 8, 25), start);
        Assert.Equal(new DateOnly(2026, 9, 24), end);
    }

    [Fact]
    public void SalaryCycle_DateBeforeStartDay_StartsPreviousMonth()
    {
        var (start, end) = MonthCycleMath.ResolvePeriodContaining(new DateOnly(2026, 8, 3), startDay: 25);

        Assert.Equal(new DateOnly(2026, 7, 25), start);
        Assert.Equal(new DateOnly(2026, 8, 24), end);
    }

    [Fact]
    public void StartDay31_ClampsToShortMonth()
    {
        // February 2026 has 28 days, so a 31st cycle starts on the 28th.
        var (start, end) = MonthCycleMath.ResolvePeriodContaining(new DateOnly(2026, 3, 1), startDay: 31);

        Assert.Equal(new DateOnly(2026, 2, 28), start);
        Assert.Equal(new DateOnly(2026, 3, 30), end);
    }

    [Fact]
    public void StartDay31_LeapYearFebruary()
    {
        var (start, _) = MonthCycleMath.ResolvePeriodContaining(new DateOnly(2028, 2, 29), startDay: 31);

        Assert.Equal(new DateOnly(2028, 2, 29), start);
    }

    [Fact]
    public void PeriodsAreContiguous_NoGapsOrOverlaps()
    {
        var date = new DateOnly(2026, 1, 10);
        var (_, previousEnd) = MonthCycleMath.ResolvePeriodContaining(date, startDay: 25);

        for (var i = 0; i < 24; i++)
        {
            var next = previousEnd.AddDays(1);
            var (start, end) = MonthCycleMath.ResolvePeriodContaining(next, startDay: 25);

            Assert.Equal(next, start);
            Assert.True(end >= start);
            previousEnd = end;
        }
    }

    [Fact]
    public void YearBoundary_WrapsCorrectly()
    {
        var (start, end) = MonthCycleMath.ResolvePeriodContaining(new DateOnly(2027, 1, 5), startDay: 25);

        Assert.Equal(new DateOnly(2026, 12, 25), start);
        Assert.Equal(new DateOnly(2027, 1, 24), end);
    }

    [Fact]
    public void ShiftPeriod_MovesForwardAndBack()
    {
        var (start, _) = MonthCycleMath.ResolvePeriodContaining(new DateOnly(2026, 8, 26), startDay: 25);

        var (nextStart, _) = MonthCycleMath.ShiftPeriod(start, startDay: 25, offset: 1);
        Assert.Equal(new DateOnly(2026, 9, 25), nextStart);

        var (prevStart, _) = MonthCycleMath.ShiftPeriod(start, startDay: 25, offset: -1);
        Assert.Equal(new DateOnly(2026, 7, 25), prevStart);
    }

    [Fact]
    public void ShiftPeriod_FromClampedStart_ReturnsToFullDay()
    {
        // Starting from a February-clamped 28th, moving forward should return to the 31st in March.
        var (febStart, _) = MonthCycleMath.ResolvePeriodContaining(new DateOnly(2026, 2, 28), startDay: 31);
        var (marStart, _) = MonthCycleMath.ShiftPeriod(febStart, startDay: 31, offset: 1);

        Assert.Equal(new DateOnly(2026, 3, 31), marStart);
    }

    [Fact]
    public void BuildLabel_CalendarMonth_UsesMonthName()
    {
        var label = MonthCycleMath.BuildLabel(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));
        Assert.Equal("Aug 2026", label);
    }

    [Fact]
    public void BuildLabel_CustomCycle_ShowsRange()
    {
        var label = MonthCycleMath.BuildLabel(new DateOnly(2026, 7, 25), new DateOnly(2026, 8, 24));
        Assert.Contains("25 Jul", label);
        Assert.Contains("24 Aug", label);
    }
}
