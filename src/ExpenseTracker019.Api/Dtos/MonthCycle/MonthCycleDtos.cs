using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker019.Api.Dtos.MonthCycle;

public class MonthCycleDto
{
    public int StartDay { get; set; }
    public bool IsConfigured { get; set; }
}

public class UpdateMonthCycleRequest
{
    [Range(1, 31)]
    public int StartDay { get; set; }
}

public class BudgetPeriodDto
{
    public Guid Id { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Label { get; set; } = string.Empty;
}
