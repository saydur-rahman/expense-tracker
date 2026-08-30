namespace ExpenseTracker019.Api.Models;

/// <summary>
/// The rhythm a user budgets in. One user has one rhythm at a time, chosen in settings.
/// </summary>
/// <remarks>
/// A period is only ever a start and an end date — budgets, expenses and reports never ask
/// which kind it is. This exists so period *resolution* knows how to cut the next window,
/// and so a week and a month that happen to start on the same day stay distinct rows.
/// </remarks>
public enum PeriodKind
{
    Month = 0,
    Week = 1,
}
