using ExpenseTracker019.Api.Dtos.Incomes;
using ExpenseTracker019.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker019.Api.Controllers;

[ApiController]
[Route("api/incomes")]
[Authorize]
public class IncomesController : ControllerBase
{
    private readonly IIncomeService _incomeService;
    private readonly ICurrentUser _currentUser;

    public IncomesController(IIncomeService incomeService, ICurrentUser currentUser)
    {
        _incomeService = incomeService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IncomeListDto>> List([FromQuery] IncomeQuery query)
        => Ok(await _incomeService.ListAsync(_currentUser.Id, query));

    [HttpPost]
    public async Task<ActionResult<IncomeDto>> Create(SaveIncomeRequest request)
        => Ok(await _incomeService.CreateAsync(_currentUser.Id, request));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<IncomeDto>> Update(Guid id, SaveIncomeRequest request)
        => Ok(await _incomeService.UpdateAsync(_currentUser.Id, id, request));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _incomeService.DeleteAsync(_currentUser.Id, id);
        return NoContent();
    }
}
