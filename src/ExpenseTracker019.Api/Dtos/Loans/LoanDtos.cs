using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker019.Api.Dtos.Loans;

/// <summary>A head linked to a loan or an investment, named for display.</summary>
public class LinkedHeadDto
{
    public Guid HeadId { get; set; }
    public string HeadName { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>The head has since been removed. Its past rows still count.</summary>
    public bool IsArchived { get; set; }
}

public class LoanDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Lender { get; set; }
    public decimal AmountTaken { get; set; }
    public DateOnly TakenOn { get; set; }
    public string? Remark { get; set; }

    /// <summary>Sum of expenses on the linked heads, from <see cref="TakenOn"/> onward.</summary>
    public decimal Repaid { get; set; }

    /// <summary>Taken minus repaid, floored at zero.</summary>
    public decimal Outstanding { get; set; }

    /// <summary>0–100.</summary>
    public decimal PercentSettled { get; set; }

    /// <summary>Paid beyond what was taken. Usually a payment logged against the wrong head.</summary>
    public decimal Overpaid { get; set; }

    public bool IsSettled { get; set; }

    public List<LinkedHeadDto> Heads { get; set; } = new();
}

/// <summary>One expense counted as a repayment.</summary>
public class LoanTransactionDto
{
    public Guid Id { get; set; }
    public Guid HeadId { get; set; }
    public string HeadName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string? Note { get; set; }
}

public class LoanTransactionListDto
{
    public List<LoanTransactionDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public decimal TotalAmount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>The loan plus the most recent transactions, so the screen opens in one call.</summary>
public class LoanDetailDto
{
    public LoanDto Loan { get; set; } = new();
    public List<LoanTransactionDto> RecentTransactions { get; set; } = new();
    public int TransactionCount { get; set; }
}

/// <summary>What was paid in each of the last several cycles — the column chart.</summary>
public class PeriodTotalDto
{
    public string Label { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Only set for investments, where the columns have two sides.</summary>
    public decimal SecondaryAmount { get; set; }
}

public class SaveLoanRequest
{
    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? Lender { get; set; }

    [Range(0.01, 999999999)]
    public decimal AmountTaken { get; set; }

    [Required]
    public DateOnly TakenOn { get; set; }

    [MaxLength(1000)]
    public string? Remark { get; set; }

    /// <summary>
    /// Expense heads whose spending repays this loan. Replaces the existing set wholesale,
    /// so a PUT carrying fewer ids unlinks the rest.
    /// </summary>
    public List<Guid> HeadIds { get; set; } = new();
}
