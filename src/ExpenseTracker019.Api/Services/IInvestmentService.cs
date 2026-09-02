using ExpenseTracker019.Api.Dtos.Investments;
using ExpenseTracker019.Api.Dtos.Loans;

namespace ExpenseTracker019.Api.Services;

public interface IInvestmentService
{
    Task<List<InvestmentDto>> ListAsync(Guid userId);
    Task<InvestmentDetailDto> GetAsync(Guid userId, Guid investmentId);
    Task<InvestmentTransactionListDto> ListTransactionsAsync(
        Guid userId, Guid investmentId, LoanTransactionQuery query);
    Task<IReadOnlyList<PeriodTotalDto>> ListByPeriodAsync(Guid userId, Guid investmentId, int count);

    /// <summary>What went into investments in a cycle, against everything earned in it.</summary>
    Task<InvestmentVsIncomeDto> GetVsIncomeAsync(Guid userId, Guid periodId);

    Task<InvestmentDto> CreateAsync(Guid userId, SaveInvestmentRequest request);
    Task<InvestmentDto> UpdateAsync(Guid userId, Guid investmentId, SaveInvestmentRequest request);
    Task DeleteAsync(Guid userId, Guid investmentId);
}
