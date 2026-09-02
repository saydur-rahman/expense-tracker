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
    public ICollection<Income> Incomes { get; set; } = new List<Income>();

    // A head may be claimed by at most one loan and one investment, but the link is
    // modelled as a collection so EF can enforce that with a unique index rather than
    // a nullable FK that would have to be nulled on every delete.
    public ICollection<LoanHead> LoanHeads { get; set; } = new List<LoanHead>();
    public ICollection<InvestmentHead> InvestmentHeads { get; set; } = new List<InvestmentHead>();
}
