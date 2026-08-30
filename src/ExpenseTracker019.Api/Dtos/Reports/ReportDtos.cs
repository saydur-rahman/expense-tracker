namespace ExpenseTracker019.Api.Dtos.Reports;

public class HeadSummaryDto
{
    public Guid HeadId { get; set; }
    public string HeadName { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public decimal? Budget { get; set; }
    public decimal Spent { get; set; }
    public decimal? Remaining { get; set; }
    public bool IsOverBudget { get; set; }
}

/// <summary>
/// One category's figures for a period. Used for both ledgers: on an income category
/// <see cref="Spent"/> carries the amount received and the budget fields stay null,
/// so the dashboard can render either tab with one component.
/// </summary>
public class CategorySummaryDto
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public decimal? Budget { get; set; }
    public decimal Spent { get; set; }
    public decimal? Remaining { get; set; }
    public bool IsOverBudget { get; set; }
    public List<HeadSummaryDto> Heads { get; set; } = new();
}

public class PeriodSummaryDto
{
    public Guid PeriodId { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal TotalBudget { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal TotalRemaining { get; set; }

    public decimal TotalIncome { get; set; }

    /// <summary>Income minus spending for the period. Negative means you spent more than you earned.</summary>
    public decimal TotalSaved { get; set; }

    /// <summary>The spending breakdown — the dashboard's "Expense" tab.</summary>
    public List<CategorySummaryDto> Categories { get; set; } = new();

    /// <summary>The income breakdown — the dashboard's "Income" tab.</summary>
    public List<CategorySummaryDto> IncomeCategories { get; set; } = new();
}
