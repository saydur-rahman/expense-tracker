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

    // ---- Weekly cycle ----------------------------------------------------

    [Fact]
    public void Week_MidWeekDate_ResolvesToContainingWeek()
    {
        // Wednesday 2 Sep 2026, weeks starting Monday.
        var (start, end) = MonthCycleMath.ResolveWeekContaining(new DateOnly(2026, 9, 2), DayOfWeek.Monday);

        Assert.Equal(new DateOnly(2026, 8, 31), start);
        Assert.Equal(new DateOnly(2026, 9, 6), end);
        Assert.Equal(DayOfWeek.Monday, start.DayOfWeek);
    }

    [Fact]
    public void Week_DateOnStartDay_StartsThatDay()
    {
        var (start, end) = MonthCycleMath.ResolveWeekContaining(new DateOnly(2026, 8, 31), DayOfWeek.Monday);

        Assert.Equal(new DateOnly(2026, 8, 31), start);
        Assert.Equal(new DateOnly(2026, 9, 6), end);
    }

    [Fact]
    public void Week_SundayStart_CutsDifferentlyFromMonday()
    {
        var date = new DateOnly(2026, 9, 2);

        var (mondayStart, _) = MonthCycleMath.ResolveWeekContaining(date, DayOfWeek.Monday);
        var (sundayStart, _) = MonthCycleMath.ResolveWeekContaining(date, DayOfWeek.Sunday);

        Assert.Equal(new DateOnly(2026, 8, 31), mondayStart);
        Assert.Equal(new DateOnly(2026, 8, 30), sundayStart);
    }

    [Theory]
    [InlineData(DayOfWeek.Sunday)]
    [InlineData(DayOfWeek.Monday)]
    [InlineData(DayOfWeek.Tuesday)]
    [InlineData(DayOfWeek.Wednesday)]
    [InlineData(DayOfWeek.Thursday)]
    [InlineData(DayOfWeek.Friday)]
    [InlineData(DayOfWeek.Saturday)]
    public void Week_AnyStartDay_IsSevenDaysAndContainsTheDate(DayOfWeek weekStartsOn)
    {
        var date = new DateOnly(2026, 9, 2);
        var (start, end) = MonthCycleMath.ResolveWeekContaining(date, weekStartsOn);

        Assert.Equal(weekStartsOn, start.DayOfWeek);
        Assert.Equal(6, end.DayNumber - start.DayNumber);
        Assert.InRange(date, start, end);
    }

    [Fact]
    public void Week_AcrossYearBoundary_Resolves()
    {
        // Thursday 31 Dec 2026 sits in the week starting Monday 28 Dec.
        var (start, end) = MonthCycleMath.ResolveWeekContaining(new DateOnly(2026, 12, 31), DayOfWeek.Monday);

        Assert.Equal(new DateOnly(2026, 12, 28), start);
        Assert.Equal(new DateOnly(2027, 1, 3), end);
    }

    [Fact]
    public void ShiftWeek_MovesWholeWeeks()
    {
        var start = new DateOnly(2026, 8, 31);

        var (next, nextEnd) = MonthCycleMath.ShiftWeek(start, 1);
        var (prev, _) = MonthCycleMath.ShiftWeek(start, -1);

        Assert.Equal(new DateOnly(2026, 9, 7), next);
        Assert.Equal(new DateOnly(2026, 9, 13), nextEnd);
        Assert.Equal(new DateOnly(2026, 8, 24), prev);
    }

    [Fact]
    public void BuildWeekLabel_WeekStartingOnTheFirst_IsNotAMonthName()
    {
        // BuildLabel shortens anything starting on the 1st to "Sep 2026". For a week that
        // would name a span five times too long, which is why weeks label separately.
        var start = new DateOnly(2026, 3, 1);
        var end = new DateOnly(2026, 3, 7);

        Assert.Equal("Mar 2026", MonthCycleMath.BuildLabel(start, end));

        var weekLabel = MonthCycleMath.BuildWeekLabel(start, end);
        Assert.NotEqual("Mar 2026", weekLabel);
        Assert.Contains("7 Mar", weekLabel);
    }

    [Fact]
    public void BuildWeekLabel_WithinOneMonth_ShowsBothDays()
    {
        var label = MonthCycleMath.BuildWeekLabel(new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13));

        Assert.Contains("7", label);
        Assert.Contains("13 Sep 2026", label);
    }

    [Fact]
    public void BuildWeekLabel_AcrossMonths_NamesBothMonths()
    {
        var label = MonthCycleMath.BuildWeekLabel(new DateOnly(2026, 8, 31), new DateOnly(2026, 9, 6));

        Assert.Contains("31 Aug", label);
        Assert.Contains("6 Sep 2026", label);
    }

    [Fact]
    public void BuildWeekLabel_AcrossYears_NamesBothYears()
    {
        var label = MonthCycleMath.BuildWeekLabel(new DateOnly(2026, 12, 28), new DateOnly(2027, 1, 3));

        Assert.Contains("28 Dec 2026", label);
        Assert.Contains("3 Jan 2027", label);
    }
}
