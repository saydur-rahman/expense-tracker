using ExpenseTracker.Api.Dtos.Reports;

namespace ExpenseTracker.Api.Services;

public interface IReportService
{
    Task<PeriodSummaryDto> GetPeriodSummaryAsync(Guid userId, Guid periodId);
}
