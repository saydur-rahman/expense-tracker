using ExpenseTracker019.Api.Services;

namespace ExpenseTracker019.Tests;

/// <summary>
/// The loan and investment arithmetic. Both features run through this one class — an
/// investment's payback is the same sum as a loan's balance, with what you put in
/// standing in for what you borrowed.
/// </summary>
public class LoanMathTests
{
    [Fact]
    public void Nothing_repaid_leaves_the_whole_amount_outstanding()
    {
        Assert.Equal(12000m, LoanMath.Outstanding(12000m, 0m));
        Assert.Equal(0m, LoanMath.PercentSettled(12000m, 0m));
        Assert.False(LoanMath.IsSettled(12000m, 0m));
    }

    [Theory]
    [InlineData(12000, 500, 11500)]
    [InlineData(12000, 6000, 6000)]
    [InlineData(12000, 11999.99, 0.01)]
    public void Outstanding_is_taken_minus_repaid(decimal taken, decimal repaid, decimal expected)
        => Assert.Equal(expected, LoanMath.Outstanding(taken, repaid));

    [Fact]
    public void Repaying_exactly_settles_it()
    {
        Assert.Equal(0m, LoanMath.Outstanding(12000m, 12000m));
        Assert.Equal(100m, LoanMath.PercentSettled(12000m, 12000m));
        Assert.True(LoanMath.IsSettled(12000m, 12000m));
        Assert.Equal(0m, LoanMath.Overpaid(12000m, 12000m));
    }

    [Fact]
    public void Overpaying_floors_the_balance_rather_than_going_into_credit()
    {
        // A negative balance would draw a bar pointing the wrong way, and you are not
        // owed money by a loan you overpaid.
        Assert.Equal(0m, LoanMath.Outstanding(12000m, 13000m));
        Assert.Equal(100m, LoanMath.PercentSettled(12000m, 13000m));
        Assert.True(LoanMath.IsSettled(12000m, 13000m));
    }

    [Fact]
    public void Overpayment_is_reported_rather_than_hidden()
    {
        // It usually means a payment went against the wrong head, and clamping it away
        // silently would hide the mistake.
        Assert.Equal(1000m, LoanMath.Overpaid(12000m, 13000m));
        Assert.Equal(0m, LoanMath.Overpaid(12000m, 11000m));
    }

    [Theory]
    [InlineData(12000, 0, 0)]
    [InlineData(12000, 3000, 25)]
    [InlineData(12000, 6000, 50)]
    [InlineData(12000, 12000, 100)]
    public void Percent_settled_tracks_the_ratio(decimal taken, decimal repaid, decimal expected)
        => Assert.Equal(expected, LoanMath.PercentSettled(taken, repaid));

    [Fact]
    public void Percent_settled_rounds_to_two_places()
    {
        // 1/3 of a loan is 33.33%, not 33.333333333333333333333333333.
        Assert.Equal(33.33m, LoanMath.PercentSettled(3000m, 1000m));
        Assert.Equal(66.67m, LoanMath.PercentSettled(3000m, 2000m));
    }

    [Fact]
    public void A_loan_of_nothing_is_settled_rather_than_a_divide_by_zero()
    {
        Assert.Equal(100m, LoanMath.PercentSettled(0m, 0m));
        Assert.Equal(0m, LoanMath.Outstanding(0m, 0m));
        Assert.True(LoanMath.IsSettled(0m, 0m));
    }

    [Fact]
    public void A_negative_amount_taken_is_treated_as_nothing_owed()
    {
        // Not reachable through the API, which rejects it — this is the guard that keeps
        // a bad row from producing a nonsense percentage.
        Assert.Equal(100m, LoanMath.PercentSettled(-500m, 0m));
        Assert.Equal(0m, LoanMath.Outstanding(-500m, 0m));
    }

    [Fact]
    public void Percent_never_leaves_the_zero_to_hundred_band()
    {
        Assert.Equal(100m, LoanMath.PercentSettled(100m, 100000m));
        Assert.Equal(0m, LoanMath.PercentSettled(100m, 0m));
    }

    [Fact]
    public void Cents_survive_the_arithmetic()
    {
        // decimal, not double: 0.1 + 0.2 must be 0.3 here.
        Assert.Equal(0.30m, LoanMath.Outstanding(0.60m, 0.30m));
        Assert.Equal(50m, LoanMath.PercentSettled(0.60m, 0.30m));
    }

    [Fact]
    public void An_investment_reads_the_same_way_as_a_loan()
    {
        // Invested stands in for taken, returned for repaid: 20,000 back out of 50,000
        // is 40% recouped with 30,000 still out there.
        Assert.Equal(30000m, LoanMath.Outstanding(50000m, 20000m));
        Assert.Equal(40m, LoanMath.PercentSettled(50000m, 20000m));
        Assert.False(LoanMath.IsSettled(50000m, 20000m));

        // And returns beyond the capital are the gain.
        Assert.Equal(5000m, LoanMath.Overpaid(50000m, 55000m));
    }
}
