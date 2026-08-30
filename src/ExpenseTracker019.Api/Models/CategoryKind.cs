namespace ExpenseTracker019.Api.Models;

/// <summary>
/// Which side of the ledger a category — and every head under it — belongs to.
/// </summary>
/// <remarks>
/// Income reuses the category/head structure but never carries a budget: budgets
/// exist to cap spending, and there is nothing to cap on money coming in. The two
/// trees are kept apart, so "Salary" and "Groceries" never appear in the same list.
/// </remarks>
public enum CategoryKind
{
    Expense = 0,
    Income = 1,
}
