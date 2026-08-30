using ExpenseTracker019.Api.Dtos.Incomes;

namespace ExpenseTracker019.Api.Services;

public class IncomeQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? HeadId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public interface IIncomeService
{
    Task<IncomeListDto> ListAsync(Guid userId, IncomeQuery query);
    Task<IncomeDto> CreateAsync(Guid userId, SaveIncomeRequest request);
    Task<IncomeDto> UpdateAsync(Guid userId, Guid incomeId, SaveIncomeRequest request);
    Task DeleteAsync(Guid userId, Guid incomeId);
}
