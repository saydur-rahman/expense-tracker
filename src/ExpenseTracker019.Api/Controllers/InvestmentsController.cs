using ExpenseTracker019.Api.Dtos.Investments;
using ExpenseTracker019.Api.Dtos.Loans;
using ExpenseTracker019.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker019.Api.Controllers;

[ApiController]
[Route("api/investments")]
[Authorize]
public class InvestmentsController : ControllerBase
{
    private readonly IInvestmentService _investmentService;
    private readonly ICurrentUser _currentUser;

    public InvestmentsController(IInvestmentService investmentService, ICurrentUser currentUser)
    {
        _investmentService = investmentService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<List<InvestmentDto>>> List()
        => Ok(await _investmentService.ListAsync(_currentUser.Id));

    /// <summary>Invested against earned for one cycle — the split on the Investments screen.</summary>
    [HttpGet("vs-income")]
    public async Task<ActionResult<InvestmentVsIncomeDto>> VsIncome([FromQuery] Guid periodId)
        => Ok(await _investmentService.GetVsIncomeAsync(_currentUser.Id, periodId));

    /// <summary>Investments and lendings added up, plus what moved in that cycle.</summary>
    [HttpGet("portfolio")]
    public async Task<ActionResult<InvestmentPortfolioDto>> Portfolio([FromQuery] Guid periodId)
        => Ok(await _investmentService.GetPortfolioAsync(_currentUser.Id, periodId));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvestmentDetailDto>> Get(Guid id)
        => Ok(await _investmentService.GetAsync(_currentUser.Id, id));

    [HttpGet("{id:guid}/transactions")]
    public async Task<ActionResult<InvestmentTransactionListDto>> Transactions(
        Guid id, [FromQuery] LoanTransactionQuery query)
        => Ok(await _investmentService.ListTransactionsAsync(_currentUser.Id, id, query));

    [HttpGet("{id:guid}/by-period")]
    public async Task<ActionResult<IReadOnlyList<PeriodTotalDto>>> ByPeriod(Guid id, int count = 12)
        => Ok(await _investmentService.ListByPeriodAsync(_currentUser.Id, id, count));

    [HttpPost]
    public async Task<ActionResult<InvestmentDto>> Create(SaveInvestmentRequest request)
        => Ok(await _investmentService.CreateAsync(_currentUser.Id, request));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<InvestmentDto>> Update(Guid id, SaveInvestmentRequest request)
        => Ok(await _investmentService.UpdateAsync(_currentUser.Id, id, request));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _investmentService.DeleteAsync(_currentUser.Id, id);
        return NoContent();
    }
}
