using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker019.Api.Dtos.Budgets;

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

    /// <summary>
    /// The budget actually in force: the head total once any head is budgeted, otherwise
    /// <see cref="Target"/>. Heads are authoritative — a category is what its parts add up to.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// The optional figure typed on the category itself. Purely a target to aim at: it never
    /// caps the heads and never overrides their total.
    /// </summary>
    public decimal? Target { get; set; }

    /// <summary>Sum of this category's head budgets for the period.</summary>
    public decimal AllocatedToHeads { get; set; }

    /// <summary>
    /// Head total minus target — positive is over the target, negative is under it. Null
    /// unless both a target and at least one head budget exist, since otherwise there is
    /// nothing to compare.
    /// </summary>
    public decimal? Difference { get; set; }

    public List<HeadBudgetDto> Heads { get; set; } = new();
}

public class PeriodBudgetsDto
{
    public Guid PeriodId { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    /// <summary>
    /// Income logged in this period. Carried here so the budgeting screen can show what
    /// there is to divide up without a second round trip to the reports endpoint.
    /// </summary>
    public decimal TotalIncome { get; set; }

    /// <summary>
    /// Every category's budget in force, added together — so it follows the same
    /// heads-first rule as <see cref="CategoryBudgetDto.Amount"/> rather than re-deriving it.
    /// </summary>
    public decimal TotalBudgeted { get; set; }

    public List<CategoryBudgetDto> Categories { get; set; } = new();
}

public class SetBudgetRequest
{
    [Range(0, 999999999)]
    public decimal Amount { get; set; }
}
