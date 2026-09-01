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

    // No period totals here on purpose. The budgeting screen shows income, budget, spent
    // and left through the same reports summary the dashboard uses, so the two screens
    // cannot print different figures — and the heads-first rule stays in the two places
    // that already own it rather than gaining a third.
    public List<CategoryBudgetDto> Categories { get; set; } = new();
}

public class SetBudgetRequest
{
    [Range(0, 999999999)]
    public decimal Amount { get; set; }
}
