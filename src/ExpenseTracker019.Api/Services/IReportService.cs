using ExpenseTracker019.Api.Dtos.Reports;

namespace ExpenseTracker019.Api.Services;

public interface IReportService
{
    Task<PeriodSummaryDto> GetPeriodSummaryAsync(Guid userId, Guid periodId);
}
