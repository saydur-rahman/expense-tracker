using ExpenseTracker019.Api.Dtos.Loans;

namespace ExpenseTracker019.Api.Services;

public class LoanTransactionQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public interface ILoanService
{
    Task<List<LoanDto>> ListAsync(Guid userId);

    /// <summary>Every loan added up, plus what was paid in one cycle.</summary>
    Task<LoanPortfolioDto> GetPortfolioAsync(Guid userId, Guid periodId);
    Task<LoanDetailDto> GetAsync(Guid userId, Guid loanId);
    Task<LoanTransactionListDto> ListTransactionsAsync(Guid userId, Guid loanId, LoanTransactionQuery query);
    Task<IReadOnlyList<PeriodTotalDto>> ListByPeriodAsync(Guid userId, Guid loanId, int count);
    Task<LoanDto> CreateAsync(Guid userId, SaveLoanRequest request);
    Task<LoanDto> UpdateAsync(Guid userId, Guid loanId, SaveLoanRequest request);
    Task DeleteAsync(Guid userId, Guid loanId);
}
