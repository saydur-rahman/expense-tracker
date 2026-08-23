namespace ExpenseTracker.Api.Models;

public class Category
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public bool IsArchived { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Head> Heads { get; set; } = new List<Head>();
    public ICollection<CategoryBudget> CategoryBudgets { get; set; } = new List<CategoryBudget>();
}
