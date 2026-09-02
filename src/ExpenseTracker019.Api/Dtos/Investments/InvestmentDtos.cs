using System.ComponentModel.DataAnnotations;
using ExpenseTracker019.Api.Dtos.Loans;
using ExpenseTracker019.Api.Models;

namespace ExpenseTracker019.Api.Dtos.Investments;

public class InvestmentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Investment or a loan to someone else. Only wording and grouping follow it.</summary>
    public InvestmentKind Kind { get; set; }

    /// <summary>Who you lent it to. Only set on a <see cref="InvestmentKind.Lend"/>.</summary>
    public string? Counterparty { get; set; }

    public string? Remark { get; set; }
    public DateOnly StartedOn { get; set; }

    /// <summary>Sum of expenses on the contribution heads, from <see cref="StartedOn"/> onward.</summary>
    public decimal Invested { get; set; }

    /// <summary>Sum of income on the return heads over the same window.</summary>
    public decimal Returned { get; set; }

    /// <summary>Invested minus returned, floored at zero — capital still out there.</summary>
    public decimal Outstanding { get; set; }

    /// <summary>0–100: how much of what you put in has come back.</summary>
    public decimal PercentReturned { get; set; }

    /// <summary>Returns beyond what was put in. This is the profit, once there is any.</summary>
    public decimal Gain { get; set; }

    public bool IsRecouped { get; set; }

    public List<LinkedHeadDto> ContributionHeads { get; set; } = new();
    public List<LinkedHeadDto> ReturnHeads { get; set; } = new();
}

/// <summary>One expense or income counted against an investment.</summary>
public class InvestmentTransactionDto
{
    public Guid Id { get; set; }
    public Guid HeadId { get; set; }
    public string HeadName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string? Note { get; set; }
    public InvestmentDirection Direction { get; set; }
}

public class InvestmentTransactionListDto
{
    public List<InvestmentTransactionDto> Items { get; set; } = new();
    public int TotalCount { get; set; }

    /// <summary>Contributions in the filtered window.</summary>
    public decimal TotalInvested { get; set; }

    /// <summary>Returns in the filtered window.</summary>
    public decimal TotalReturned { get; set; }

    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class InvestmentDetailDto
{
    public InvestmentDto Investment { get; set; } = new();
    public List<InvestmentTransactionDto> RecentTransactions { get; set; } = new();
    public int TransactionCount { get; set; }
}

/// <summary>
/// What went into investments against what was earned, for one cycle — the
/// investment-vs-income split on the Investments screen.
/// </summary>
public class InvestmentVsIncomeDto
{
    public string PeriodLabel { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    /// <summary>
    /// Put into <see cref="InvestmentKind.Investment"/> entries during this cycle. Money
    /// lent out is deliberately excluded: this answers "what share of my income did I
    /// invest", and a loan to a friend is not that.
    /// </summary>
    public decimal Invested { get; set; }

    /// <summary>All income in this cycle, the same figure the dashboard shows.</summary>
    public decimal Income { get; set; }

    /// <summary>Income not invested. Negative if you invested more than you earned.</summary>
    public decimal Remainder { get; set; }

    /// <summary>Share of income invested, 0–100. Zero when there was no income.</summary>
    public decimal PercentOfIncome { get; set; }
}

public class SaveInvestmentRequest
{
    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public InvestmentKind Kind { get; set; }

    [MaxLength(120)]
    public string? Counterparty { get; set; }

    [MaxLength(1000)]
    public string? Remark { get; set; }

    [Required]
    public DateOnly StartedOn { get; set; }

    /// <summary>Expense heads you invest through. Replaces the existing set wholesale.</summary>
    public List<Guid> ContributionHeadIds { get; set; } = new();

    /// <summary>Income heads the returns arrive on. Replaces the existing set wholesale.</summary>
    public List<Guid> ReturnHeadIds { get; set; } = new();
}
