using ExpenseTracker.Api.Dtos.Budgets;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/budget-periods/{periodId:guid}")]
[Authorize]
public class BudgetsController : ControllerBase
{
    private readonly IBudgetService _budgetService;
    private readonly ICurrentUser _currentUser;

    public BudgetsController(IBudgetService budgetService, ICurrentUser currentUser)
    {
        _budgetService = budgetService;
        _currentUser = currentUser;
    }

    [HttpGet("budgets")]
    public async Task<ActionResult<PeriodBudgetsDto>> Get(Guid periodId)
        => Ok(await _budgetService.GetPeriodBudgetsAsync(_currentUser.Id, periodId));

    [HttpPut("categories/{categoryId:guid}/budget")]
    public async Task<ActionResult<PeriodBudgetsDto>> SetCategoryBudget(Guid periodId, Guid categoryId, SetBudgetRequest request)
        => Ok(await _budgetService.SetCategoryBudgetAsync(_currentUser.Id, periodId, categoryId, request.Amount));

    [HttpDelete("categories/{categoryId:guid}/budget")]
    public async Task<ActionResult<PeriodBudgetsDto>> ClearCategoryBudget(Guid periodId, Guid categoryId)
        => Ok(await _budgetService.ClearCategoryBudgetAsync(_currentUser.Id, periodId, categoryId));

    [HttpPut("heads/{headId:guid}/budget")]
    public async Task<ActionResult<PeriodBudgetsDto>> SetHeadBudget(Guid periodId, Guid headId, SetBudgetRequest request)
        => Ok(await _budgetService.SetHeadBudgetAsync(_currentUser.Id, periodId, headId, request.Amount));

    [HttpDelete("heads/{headId:guid}/budget")]
    public async Task<ActionResult<PeriodBudgetsDto>> ClearHeadBudget(Guid periodId, Guid headId)
        => Ok(await _budgetService.ClearHeadBudgetAsync(_currentUser.Id, periodId, headId));
}
