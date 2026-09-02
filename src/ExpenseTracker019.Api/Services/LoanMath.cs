namespace ExpenseTracker019.Api.Services;

/// <summary>
/// The arithmetic behind a loan's balance and an investment's payback.
/// </summary>
/// <remarks>
/// Kept as a pure static class, next to <see cref="MonthCycleMath"/> and unit-tested for
/// the same reason: it is the part that can be wrong without anything failing to build.
/// The services around it only fetch rows and hand the totals here.
/// </remarks>
public static class LoanMath
{
    /// <summary>
    /// What is still owed. Floors at zero — overpaying a loan does not put you in credit,
    /// and a negative balance would draw a bar pointing the wrong way.
    /// </summary>
    public static decimal Outstanding(decimal taken, decimal repaid)
        => Math.Max(0m, taken - repaid);

    /// <summary>
    /// How much of the loan is cleared, 0–100. A loan of nothing is fully settled rather
    /// than a division by zero.
    /// </summary>
    public static decimal PercentSettled(decimal taken, decimal repaid)
    {
        if (taken <= 0m)
        {
            return 100m;
        }

        var pct = repaid / taken * 100m;
        return Math.Clamp(Math.Round(pct, 2), 0m, 100m);
    }

    /// <summary>
    /// Paid past what was taken. Shown rather than hidden: it usually means a payment was
    /// logged against the wrong head, and silently clamping it away hides the mistake.
    /// </summary>
    public static decimal Overpaid(decimal taken, decimal repaid)
        => Math.Max(0m, repaid - taken);

    /// <summary>True once nothing is left to pay.</summary>
    public static bool IsSettled(decimal taken, decimal repaid)
        => Outstanding(taken, repaid) <= 0m;
}
