using ExpenseTracker019.Api.Dtos.Loans;
using ExpenseTracker019.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker019.Api.Controllers;

[ApiController]
[Route("api/loans")]
[Authorize]
public class LoansController : ControllerBase
{
    private readonly ILoanService _loanService;
    private readonly ICurrentUser _currentUser;

    public LoansController(ILoanService loanService, ICurrentUser currentUser)
    {
        _loanService = loanService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<List<LoanDto>>> List()
        => Ok(await _loanService.ListAsync(_currentUser.Id));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LoanDetailDto>> Get(Guid id)
        => Ok(await _loanService.GetAsync(_currentUser.Id, id));

    [HttpGet("{id:guid}/transactions")]
    public async Task<ActionResult<LoanTransactionListDto>> Transactions(
        Guid id, [FromQuery] LoanTransactionQuery query)
        => Ok(await _loanService.ListTransactionsAsync(_currentUser.Id, id, query));

    [HttpGet("{id:guid}/by-period")]
    public async Task<ActionResult<IReadOnlyList<PeriodTotalDto>>> ByPeriod(Guid id, int count = 12)
        => Ok(await _loanService.ListByPeriodAsync(_currentUser.Id, id, count));

    [HttpPost]
    public async Task<ActionResult<LoanDto>> Create(SaveLoanRequest request)
        => Ok(await _loanService.CreateAsync(_currentUser.Id, request));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<LoanDto>> Update(Guid id, SaveLoanRequest request)
        => Ok(await _loanService.UpdateAsync(_currentUser.Id, id, request));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _loanService.DeleteAsync(_currentUser.Id, id);
        return NoContent();
    }
}
