namespace ExpenseTracker019.Api.Services;

/// <summary>
/// What "today" is <em>for the person making the request</em>.
/// </summary>
/// <remarks>
/// The server runs on UTC and has no idea where anyone is, so `DateTime.UtcNow` used as a
/// *date* is wrong for most of the world. In New Zealand (UTC+12/+13) it is yesterday for
/// the whole working day: on the 1st of a month, a user opening the dashboard at 9am was
/// shown the previous month's cycle until 1pm.
///
/// The browser knows its own zone exactly, so it sends it on every request and this reads
/// it back. Anything missing or unrecognised falls back to UTC, which is the old
/// behaviour — never an error, because a bad header must not make the app unusable.
/// </remarks>
public interface IUserClock
{
    /// <summary>The caller's local date.</summary>
    DateOnly Today { get; }

    /// <summary>The caller's zone, or UTC when it wasn't sent or wasn't recognised.</summary>
    TimeZoneInfo TimeZone { get; }
}

public class UserClock : IUserClock
{
    /// <summary>
    /// Carries an IANA zone id, e.g. `Pacific/Auckland`. A header rather than a query
    /// parameter or a stored profile field: it is set once in the API client, so every
    /// request carries it and a new endpoint cannot forget to ask.
    /// </summary>
    public const string HeaderName = "X-Time-Zone";

    private readonly IHttpContextAccessor _accessor;
    private TimeZoneInfo? _resolved;

    public UserClock(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public TimeZoneInfo TimeZone =>
        _resolved ??= ResolveZone(_accessor.HttpContext?.Request.Headers[HeaderName]);

    public DateOnly Today => TodayIn(DateTime.UtcNow, TimeZone);

    /// <summary>
    /// Turns a header value into a zone. Kept pure and public so the fallbacks are
    /// unit-tested rather than trusted — this runs on every request that needs a date.
    /// </summary>
    public static TimeZoneInfo ResolveZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            // .NET accepts IANA ids on Windows as well as Linux, so one id works on both
            // the dev machine and the Linux App Service the app is deployed to.
            return TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // An unknown zone means an old client or a spoofed header. Falling back is
            // strictly better than a 500: the worst case is the behaviour we already had.
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>The local date at an instant, in a zone.</summary>
    public static DateOnly TodayIn(DateTime utcNow, TimeZoneInfo zone)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone));
}
