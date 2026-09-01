using ExpenseTracker019.Api.Services;

namespace ExpenseTracker019.Tests;

/// <summary>
/// "Today" for the person making the request. The server runs on UTC, which is the wrong
/// date for most of the world for part of every day — and for New Zealand, for most of the
/// working day.
/// </summary>
public class UserClockTests
{
    private static TimeZoneInfo Auckland => TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");

    [Fact]
    public void New_Zealand_is_already_tomorrow_late_in_the_utc_day()
    {
        // 1 Sep 21:00 UTC is 2 Sep 09:00 in Auckland. This is the actual bug: a user
        // opening the app at 9am was served the previous day's date.
        var utc = new DateTime(2026, 9, 1, 21, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateOnly(2026, 9, 2), UserClock.TodayIn(utc, Auckland));
        Assert.Equal(new DateOnly(2026, 9, 1), UserClock.TodayIn(utc, TimeZoneInfo.Utc));
    }

    [Fact]
    public void The_month_rolls_over_on_the_users_clock_not_the_servers()
    {
        // The worst case: on the 1st of a month, UTC still says the 31st, so the dashboard
        // showed last month's cycle all morning.
        var utc = new DateTime(2026, 8, 31, 20, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateOnly(2026, 9, 1), UserClock.TodayIn(utc, Auckland));
        Assert.Equal(new DateOnly(2026, 8, 31), UserClock.TodayIn(utc, TimeZoneInfo.Utc));
    }

    [Fact]
    public void Daylight_saving_is_handled_by_the_zone_not_a_fixed_offset()
    {
        // Auckland is +12 in July and +13 in January. A stored offset would be wrong for
        // half the year; the zone knows.
        var midwinter = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var midsummer = new DateTime(2026, 1, 1, 11, 30, 0, DateTimeKind.Utc);

        Assert.Equal(new DateOnly(2026, 7, 2), UserClock.TodayIn(midwinter, Auckland));
        Assert.Equal(new DateOnly(2026, 1, 2), UserClock.TodayIn(midsummer, Auckland));
    }

    [Fact]
    public void Zones_behind_utc_go_the_other_way()
    {
        // Early in the UTC day the Americas are still on yesterday — the same fault
        // mirrored, and the reason this is a zone lookup rather than "add 13 hours".
        var utc = new DateTime(2026, 9, 2, 2, 0, 0, DateTimeKind.Utc);
        var newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        Assert.Equal(new DateOnly(2026, 9, 1), UserClock.TodayIn(utc, newYork));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Not/AZone")]
    [InlineData("Pacific/Nowhere")]
    [InlineData("../../etc/passwd")]
    public void An_absent_or_bad_header_falls_back_to_utc(string? header)
    {
        // Never an exception: a missing or spoofed header must leave the app working with
        // the behaviour it had before, not return a 500.
        Assert.Equal(TimeZoneInfo.Utc, UserClock.ResolveZone(header));
    }

    [Theory]
    [InlineData("Pacific/Auckland")]
    [InlineData("  Pacific/Auckland  ")]
    [InlineData("America/New_York")]
    [InlineData("Asia/Dhaka")]
    [InlineData("UTC")]
    public void A_real_iana_zone_resolves(string header)
    {
        // IANA ids work on Windows as well as Linux, so the dev machine and the Linux
        // App Service agree.
        var zone = UserClock.ResolveZone(header);

        Assert.Equal(TimeZoneInfo.FindSystemTimeZoneById(header.Trim()).Id, zone.Id);
    }

    [Fact]
    public void An_unspecified_kind_is_treated_as_utc_rather_than_local()
    {
        // Guards against the build machine's own zone leaking in and making the result
        // depend on where the tests run.
        var unspecified = new DateTime(2026, 9, 1, 21, 0, 0, DateTimeKind.Unspecified);

        Assert.Equal(new DateOnly(2026, 9, 2), UserClock.TodayIn(unspecified, Auckland));
    }
}
