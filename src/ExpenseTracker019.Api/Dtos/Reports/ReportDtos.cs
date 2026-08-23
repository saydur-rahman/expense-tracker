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
    public List<CategorySummaryDto> Categories { get; set; } = new();
}
