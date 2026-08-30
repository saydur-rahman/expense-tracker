namespace ExpenseTracker019.Api.Models;

public class Category
{
    public Guid Id { get; set; }

    /// <summary>
    /// The owning user's id, taken from the <c>sub</c> claim of an Auth019-issued token.
    /// There is no foreign key: user records live in Auth019's separate database.
    /// </summary>
    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Expense or income. Heads inherit it from their category, so nothing below
    /// this row needs its own copy.
    /// </summary>
    public CategoryKind Kind { get; set; } = CategoryKind.Expense;

    public bool IsArchived { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Head> Heads { get; set; } = new List<Head>();
    public ICollection<CategoryBudget> CategoryBudgets { get; set; } = new List<CategoryBudget>();
}
