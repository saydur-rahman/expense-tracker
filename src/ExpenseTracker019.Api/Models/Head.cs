namespace ExpenseTracker019.Api.Models;

public class Head
{
    public Guid Id { get; set; }

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public bool IsArchived { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<HeadBudget> HeadBudgets { get; set; } = new List<HeadBudget>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
