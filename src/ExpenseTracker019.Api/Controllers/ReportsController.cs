using ExpenseTracker019.Api.Dtos.Reports;
using ExpenseTracker019.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker019.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly IMonthCycleService _monthCycleService;
    private readonly ICurrentUser _currentUser;

    public ReportsController(
        IReportService reportService,
        IMonthCycleService monthCycleService,
        ICurrentUser currentUser)
    {
        _reportService = reportService;
        _monthCycleService = monthCycleService;
        _currentUser = currentUser;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<PeriodSummaryDto>> Summary([FromQuery] Guid periodId)
        => Ok(await _reportService.GetPeriodSummaryAsync(_currentUser.Id, periodId));

    [HttpGet("summary/current")]
    public async Task<ActionResult<PeriodSummaryDto>> CurrentSummary()
    {
        var period = await _monthCycleService.ResolveRelativePeriodAsync(_currentUser.Id, 0);
        return Ok(await _reportService.GetPeriodSummaryAsync(_currentUser.Id, period.Id));
    }
}
