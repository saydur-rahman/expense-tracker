using ExpenseTracker019.Api.Dtos.MonthCycle;
using ExpenseTracker019.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker019.Api.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly IMonthCycleService _monthCycleService;
    private readonly ICurrentUser _currentUser;

    public SettingsController(IMonthCycleService monthCycleService, ICurrentUser currentUser)
    {
        _monthCycleService = monthCycleService;
        _currentUser = currentUser;
    }

    [HttpGet("month-cycle")]
    public async Task<ActionResult<MonthCycleDto>> GetMonthCycle()
        => Ok(await _monthCycleService.GetAsync(_currentUser.Id));

    [HttpPut("month-cycle")]
    public async Task<ActionResult<MonthCycleDto>> UpdateMonthCycle(UpdateMonthCycleRequest request)
        => Ok(await _monthCycleService.UpdateAsync(
            _currentUser.Id, request.PeriodKind, request.StartDay, request.WeekStartsOn));
}
