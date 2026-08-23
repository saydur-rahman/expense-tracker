using ExpenseTracker.Api.Dtos.Expenses;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/expenses")]
[Authorize]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ICurrentUser _currentUser;

    public ExpensesController(IExpenseService expenseService, ICurrentUser currentUser)
    {
        _expenseService = expenseService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<ExpenseListDto>> List([FromQuery] ExpenseQuery query)
        => Ok(await _expenseService.ListAsync(_currentUser.Id, query));

    [HttpPost]
    public async Task<ActionResult<ExpenseDto>> Create(SaveExpenseRequest request)
        => Ok(await _expenseService.CreateAsync(_currentUser.Id, request));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ExpenseDto>> Update(Guid id, SaveExpenseRequest request)
        => Ok(await _expenseService.UpdateAsync(_currentUser.Id, id, request));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _expenseService.DeleteAsync(_currentUser.Id, id);
        return NoContent();
    }
}
