using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos.Expenses;

public class ExpenseDto
{
    public Guid Id { get; set; }
    public Guid HeadId { get; set; }
    public string HeadName { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public string? Note { get; set; }
}

public class SaveExpenseRequest
{
    [Required]
    public Guid HeadId { get; set; }

    [Range(0.01, 999999999)]
    public decimal Amount { get; set; }

    [Required]
    public DateOnly ExpenseDate { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}

public class ExpenseListDto
{
    public List<ExpenseDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public decimal TotalAmount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
