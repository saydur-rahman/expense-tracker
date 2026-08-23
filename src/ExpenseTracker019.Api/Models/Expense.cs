namespace ExpenseTracker019.Api.Models;

public class Expense
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid HeadId { get; set; }
    public Head Head { get; set; } = null!;

    public decimal Amount { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
