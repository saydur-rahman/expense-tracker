using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker019.Api.Dtos.Incomes;

public class IncomeDto
{
    public Guid Id { get; set; }
    public Guid HeadId { get; set; }
    public string HeadName { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly IncomeDate { get; set; }
    public string? Note { get; set; }
}

public class SaveIncomeRequest
{
    [Required]
    public Guid HeadId { get; set; }

    [Range(0.01, 999999999)]
    public decimal Amount { get; set; }

    [Required]
    public DateOnly IncomeDate { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}

public class IncomeListDto
{
    public List<IncomeDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public decimal TotalAmount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
