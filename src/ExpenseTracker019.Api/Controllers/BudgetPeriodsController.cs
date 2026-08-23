using ExpenseTracker019.Api.Dtos.MonthCycle;
using ExpenseTracker019.Api.Models;
using ExpenseTracker019.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker019.Api.Controllers;

[ApiController]
[Route("api/budget-periods")]
[Authorize]
public class BudgetPeriodsController : ControllerBase
{
    private readonly IMonthCycleService _monthCycleService;
    private readonly ICurrentUser _currentUser;

    public BudgetPeriodsController(IMonthCycleService monthCycleService, ICurrentUser currentUser)
    {
        _monthCycleService = monthCycleService;
        _currentUser = currentUser;
    }

    [HttpGet("current")]
    public async Task<ActionResult<BudgetPeriodDto>> GetCurrent()
        => Ok(ToDto(await _monthCycleService.ResolveRelativePeriodAsync(_currentUser.Id, 0)));

    [HttpGet("relative/{offset:int}")]
    public async Task<ActionResult<BudgetPeriodDto>> GetRelative(int offset)
        => Ok(ToDto(await _monthCycleService.ResolveRelativePeriodAsync(_currentUser.Id, offset)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BudgetPeriodDto>> GetById(Guid id)
        => Ok(ToDto(await _monthCycleService.GetPeriodByIdAsync(_currentUser.Id, id)));

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BudgetPeriodDto>>> List()
    {
        var periods = await _monthCycleService.ListPeriodsAsync(_currentUser.Id);
        return Ok(periods.Select(ToDto));
    }

    private static BudgetPeriodDto ToDto(BudgetPeriod period) => new()
    {
        Id = period.Id,
        StartDate = period.StartDate,
        EndDate = period.EndDate,
        Label = period.Label,
    };
}
