using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos.Budgets;

public class HeadBudgetDto
{
    public Guid HeadId { get; set; }
    public string HeadName { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
}

public class CategoryBudgetDto
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal? Amount { get; set; }

    /// <summary>Sum of this category's head budgets for the period.</summary>
    public decimal AllocatedToHeads { get; set; }

    /// <summary>How much of the category budget is still unallocated to heads.</summary>
    public decimal? Unallocated { get; set; }

    public List<HeadBudgetDto> Heads { get; set; } = new();
}

public class PeriodBudgetsDto
{
    public Guid PeriodId { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public List<CategoryBudgetDto> Categories { get; set; } = new();
}

public class SetBudgetRequest
{
    [Range(0, 999999999)]
    public decimal Amount { get; set; }
}
