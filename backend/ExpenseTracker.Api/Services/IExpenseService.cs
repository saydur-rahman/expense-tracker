using ExpenseTracker.Api.Dtos.Expenses;

namespace ExpenseTracker.Api.Services;

public class ExpenseQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? HeadId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public interface IExpenseService
{
    Task<ExpenseListDto> ListAsync(Guid userId, ExpenseQuery query);
    Task<ExpenseDto> CreateAsync(Guid userId, SaveExpenseRequest request);
    Task<ExpenseDto> UpdateAsync(Guid userId, Guid expenseId, SaveExpenseRequest request);
    Task DeleteAsync(Guid userId, Guid expenseId);
}
