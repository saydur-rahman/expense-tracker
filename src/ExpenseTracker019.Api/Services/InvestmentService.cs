using ExpenseTracker019.Api.Data;
using ExpenseTracker019.Api.Dtos.Investments;
using ExpenseTracker019.Api.Dtos.Loans;
using ExpenseTracker019.Api.Exceptions;
using ExpenseTracker019.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker019.Api.Services;

/// <summary>
/// Investments: what you put in, and what has come back.
/// </summary>
/// <remarks>
/// The mirror of <see cref="LoanService"/>, with two sides instead of one. Both are
/// derived — contributions are expenses on the contribution heads, returns are income on
/// the return heads — so an investment stores no amount at all and nothing here writes to
/// either ledger.
///
/// The two sides link to heads of two different <see cref="CategoryKind"/>s, which is how
/// this stays inside rule 8 rather than mixing the ledgers.
/// </remarks>
public class InvestmentService : IInvestmentService
{
    private const int MaxPageSize = 100;

    private readonly AppDbContext _db;
    private readonly IMonthCycleService _monthCycle;

    public InvestmentService(AppDbContext db, IMonthCycleService monthCycle)
    {
        _db = db;
        _monthCycle = monthCycle;
    }

    public async Task<List<InvestmentDto>> ListAsync(Guid userId)
    {
        var investments = await InvestmentsWithHeads(userId)
            .OrderBy(i => i.Kind).ThenBy(i => i.Name)
            .ToListAsync();

        var result = new List<InvestmentDto>(investments.Count);
        foreach (var investment in investments)
        {
            result.Add(ToDto(
                investment,
                await SumExpensesAsync(ContributionsFor(userId, investment)),
                await SumIncomesAsync(ReturnsFor(userId, investment))));
        }

        return result;
    }

    public async Task<InvestmentPortfolioDto> GetPortfolioAsync(Guid userId, Guid periodId)
    {
        var period = await _monthCycle.GetPeriodByIdAsync(userId, periodId);
        var investments = await InvestmentsWithHeads(userId).ToListAsync();

        var portfolio = new InvestmentPortfolioDto
        {
            PeriodLabel = period.Label,
            StartDate = period.StartDate,
            EndDate = period.EndDate,
        };

        // Both kinds always, in order, so the screen renders the same shape whether or not
        // you happen to use one of them.
        foreach (var kind in new[] { InvestmentKind.Investment, InvestmentKind.Lend })
        {
            var group = new InvestmentGroupTotalsDto { Kind = kind };

            foreach (var investment in investments.Where(i => i.Kind == kind))
            {
                var contributions = ContributionsFor(userId, investment);
                var returns = ReturnsFor(userId, investment);

                var put = await SumExpensesAsync(contributions);
                var back = await SumIncomesAsync(returns);

                group.Count++;
                group.Out += put;
                group.Back += back;
                // Per entry, so one that has paid off cannot mask another still out.
                group.Outstanding += LoanMath.Outstanding(put, back);
                group.Surplus += LoanMath.Overpaid(put, back);
                if (put > 0m && LoanMath.IsSettled(put, back)) group.RecoupedCount++;

                group.OutInPeriod += await SumExpensesAsync(contributions
                    .Where(e => e.ExpenseDate >= period.StartDate && e.ExpenseDate <= period.EndDate));
                group.BackInPeriod += await SumIncomesAsync(returns
                    .Where(i => i.IncomeDate >= period.StartDate && i.IncomeDate <= period.EndDate));
            }

            group.PercentBack = LoanMath.PercentSettled(group.Out, group.Back);
            portfolio.Groups.Add(group);
        }

        return portfolio;
    }

    public async Task<InvestmentDetailDto> GetAsync(Guid userId, Guid investmentId)
    {
        var investment = await GetOwnedAsync(userId, investmentId);

        var contributions = ContributionsFor(userId, investment);
        var returns = ReturnsFor(userId, investment);

        var merged = await MergeAsync(contributions, returns, take: 20);

        return new InvestmentDetailDto
        {
            Investment = ToDto(
                investment,
                await SumExpensesAsync(contributions),
                await SumIncomesAsync(returns)),
            TransactionCount = await contributions.CountAsync() + await returns.CountAsync(),
            RecentTransactions = merged,
        };
    }

    public async Task<InvestmentTransactionListDto> ListTransactionsAsync(
        Guid userId, Guid investmentId, LoanTransactionQuery query)
    {
        var investment = await GetOwnedAsync(userId, investmentId);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var contributions = ContributionsFor(userId, investment);
        var returns = ReturnsFor(userId, investment);

        if (query.From is { } from)
        {
            contributions = contributions.Where(e => e.ExpenseDate >= from);
            returns = returns.Where(i => i.IncomeDate >= from);
        }

        if (query.To is { } to)
        {
            contributions = contributions.Where(e => e.ExpenseDate <= to);
            returns = returns.Where(i => i.IncomeDate <= to);
        }

        // The merged page can only be drawn from the top (skip + take) of each side, so
        // fetching that many from each and slicing the merge is exact — and bounded,
        // unlike materialising both sides in full.
        var window = await MergeAsync(contributions, returns, take: page * pageSize);

        return new InvestmentTransactionListDto
        {
            TotalCount = await contributions.CountAsync() + await returns.CountAsync(),
            TotalInvested = await SumExpensesAsync(contributions),
            TotalReturned = await SumIncomesAsync(returns),
            Page = page,
            PageSize = pageSize,
            Items = window.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
        };
    }

    public async Task<IReadOnlyList<PeriodTotalDto>> ListByPeriodAsync(
        Guid userId, Guid investmentId, int count)
    {
        var investment = await GetOwnedAsync(userId, investmentId);

        // Computed windows, never resolved ones — drawing a chart must not create rows.
        var windows = await _monthCycle.ListRecentWindowsAsync(userId, Math.Clamp(count, 1, 60));

        var contributions = await ContributionsFor(userId, investment)
            .Select(e => new { Date = e.ExpenseDate, e.Amount }).ToListAsync();
        var returns = await ReturnsFor(userId, investment)
            .Select(i => new { Date = i.IncomeDate, i.Amount }).ToListAsync();

        return windows
            .OrderBy(w => w.StartDate)
            .Select(w => new PeriodTotalDto
            {
                Label = w.Label,
                StartDate = w.StartDate,
                EndDate = w.EndDate,
                Amount = contributions
                    .Where(c => c.Date >= w.StartDate && c.Date <= w.EndDate).Sum(c => c.Amount),
                SecondaryAmount = returns
                    .Where(r => r.Date >= w.StartDate && r.Date <= w.EndDate).Sum(r => r.Amount),
            })
            .ToList();
    }

    public async Task<InvestmentVsIncomeDto> GetVsIncomeAsync(Guid userId, Guid periodId)
    {
        var period = await _monthCycle.GetPeriodByIdAsync(userId, periodId);

        var contributionHeadIds = await _db.InvestmentHeads
            .Where(ih => ih.Investment.UserId == userId
                         && ih.Investment.Kind == InvestmentKind.Investment
                         && ih.Direction == InvestmentDirection.Contribution)
            .Select(ih => ih.HeadId)
            .ToListAsync();

        // Filters ignored throughout: money that went through a head you later archived
        // still went out, and dropping it would understate the share invested.
        var invested = await _db.Expenses
            .IgnoreQueryFilters()
            .Where(e => e.UserId == userId
                        && contributionHeadIds.Contains(e.HeadId)
                        && e.ExpenseDate >= period.StartDate
                        && e.ExpenseDate <= period.EndDate)
            .SumAsync(e => (decimal?)e.Amount) ?? 0m;

        var income = await _db.Incomes
            .IgnoreQueryFilters()
            .Where(i => i.UserId == userId
                        && i.IncomeDate >= period.StartDate
                        && i.IncomeDate <= period.EndDate)
            .SumAsync(i => (decimal?)i.Amount) ?? 0m;

        return new InvestmentVsIncomeDto
        {
            PeriodLabel = period.Label,
            StartDate = period.StartDate,
            EndDate = period.EndDate,
            Invested = invested,
            Income = income,
            Remainder = income - invested,
            PercentOfIncome = income <= 0m
                ? 0m
                : Math.Clamp(Math.Round(invested / income * 100m, 2), 0m, 100m),
        };
    }

    public async Task<InvestmentDto> CreateAsync(Guid userId, SaveInvestmentRequest request)
    {
        Validate(request);

        var investment = new Investment
        {
            UserId = userId,
            Name = request.Name.Trim(),
            Kind = request.Kind,
            Counterparty = Clean(request.Counterparty),
            Remark = Clean(request.Remark),
            StartedOn = request.StartedOn,
        };

        _db.Investments.Add(investment);
        await ApplyHeadsAsync(userId, investment, request);
        await _db.SaveChangesAsync();

        return (await GetAsync(userId, investment.Id)).Investment;
    }

    public async Task<InvestmentDto> UpdateAsync(
        Guid userId, Guid investmentId, SaveInvestmentRequest request)
    {
        Validate(request);

        var investment = await GetOwnedAsync(userId, investmentId);

        investment.Name = request.Name.Trim();
        investment.Kind = request.Kind;
        investment.Counterparty = Clean(request.Counterparty);
        investment.Remark = Clean(request.Remark);
        investment.StartedOn = request.StartedOn;
        investment.UpdatedAtUtc = DateTime.UtcNow;

        await ApplyHeadsAsync(userId, investment, request);
        await _db.SaveChangesAsync();

        return (await GetAsync(userId, investment.Id)).Investment;
    }

    public async Task DeleteAsync(Guid userId, Guid investmentId)
    {
        var investment = await GetOwnedAsync(userId, investmentId);

        // The ledger rows stay: they were ordinary spending and earning before this
        // investment grouped them, and they remain so afterwards.
        _db.Investments.Remove(investment);
        await _db.SaveChangesAsync();
    }

    // ---- internals -------------------------------------------------------------

    private IQueryable<Investment> InvestmentsWithHeads(Guid userId) => _db.Investments
        .Where(i => i.UserId == userId)
        .Include(i => i.Heads)
            .ThenInclude(ih => ih.Head)
                .ThenInclude(h => h.Category)
        .IgnoreQueryFilters();

    private async Task<Investment> GetOwnedAsync(Guid userId, Guid investmentId)
        => await InvestmentsWithHeads(userId).FirstOrDefaultAsync(i => i.Id == investmentId)
           ?? throw new NotFoundAppException("Investment not found.");

    private IQueryable<Expense> ContributionsFor(Guid userId, Investment investment)
    {
        var headIds = investment.Heads
            .Where(h => h.Direction == InvestmentDirection.Contribution)
            .Select(h => h.HeadId)
            .ToList();

        return _db.Expenses
            .IgnoreQueryFilters()
            .Include(e => e.Head).ThenInclude(h => h.Category)
            .Where(e => e.UserId == userId
                        && headIds.Contains(e.HeadId)
                        && e.ExpenseDate >= investment.StartedOn);
    }

    private IQueryable<Income> ReturnsFor(Guid userId, Investment investment)
    {
        var headIds = investment.Heads
            .Where(h => h.Direction == InvestmentDirection.Return)
            .Select(h => h.HeadId)
            .ToList();

        return _db.Incomes
            .IgnoreQueryFilters()
            .Include(i => i.Head).ThenInclude(h => h.Category)
            .Where(i => i.UserId == userId
                        && headIds.Contains(i.HeadId)
                        && i.IncomeDate >= investment.StartedOn);
    }

    /// <summary>The newest <paramref name="take"/> rows across both sides, in one list.</summary>
    private static async Task<List<InvestmentTransactionDto>> MergeAsync(
        IQueryable<Expense> contributions, IQueryable<Income> returns, int take)
    {
        var outgoing = await contributions
            .OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.CreatedAtUtc)
            .Take(take)
            .Select(e => new InvestmentTransactionDto
            {
                Id = e.Id,
                HeadId = e.HeadId,
                HeadName = e.Head.Name,
                CategoryName = e.Head.Category.Name,
                Amount = e.Amount,
                Date = e.ExpenseDate,
                Note = e.Note,
                Direction = InvestmentDirection.Contribution,
            })
            .ToListAsync();

        var incoming = await returns
            .OrderByDescending(i => i.IncomeDate).ThenByDescending(i => i.CreatedAtUtc)
            .Take(take)
            .Select(i => new InvestmentTransactionDto
            {
                Id = i.Id,
                HeadId = i.HeadId,
                HeadName = i.Head.Name,
                CategoryName = i.Head.Category.Name,
                Amount = i.Amount,
                Date = i.IncomeDate,
                Note = i.Note,
                Direction = InvestmentDirection.Return,
            })
            .ToListAsync();

        return outgoing.Concat(incoming)
            .OrderByDescending(t => t.Date)
            .Take(take)
            .ToList();
    }

    private static async Task<decimal> SumExpensesAsync(IQueryable<Expense> query)
        => await query.SumAsync(e => (decimal?)e.Amount) ?? 0m;

    private static async Task<decimal> SumIncomesAsync(IQueryable<Income> query)
        => await query.SumAsync(i => (decimal?)i.Amount) ?? 0m;

    private static InvestmentDto ToDto(Investment investment, decimal invested, decimal returned)
        => new()
        {
            Id = investment.Id,
            Name = investment.Name,
            Kind = investment.Kind,
            Counterparty = investment.Counterparty,
            Remark = investment.Remark,
            StartedOn = investment.StartedOn,
            Invested = invested,
            Returned = returned,
            // The same arithmetic as a loan: what you put in is the thing being paid back.
            Outstanding = LoanMath.Outstanding(invested, returned),
            PercentReturned = LoanMath.PercentSettled(invested, returned),
            Gain = LoanMath.Overpaid(invested, returned),
            IsRecouped = invested > 0m && LoanMath.IsSettled(invested, returned),
            ContributionHeads = LinkedHeads(investment, InvestmentDirection.Contribution),
            ReturnHeads = LinkedHeads(investment, InvestmentDirection.Return),
        };

    private static List<LinkedHeadDto> LinkedHeads(Investment investment, InvestmentDirection direction)
        => investment.Heads
            .Where(h => h.Direction == direction)
            .OrderBy(h => h.Head.Category.Name).ThenBy(h => h.Head.Name)
            .Select(h => new LinkedHeadDto
            {
                HeadId = h.HeadId,
                HeadName = h.Head.Name,
                CategoryId = h.Head.CategoryId,
                CategoryName = h.Head.Category.Name,
                IsArchived = h.Head.IsArchived || h.Head.Category.IsArchived,
            })
            .ToList();

    private async Task ApplyHeadsAsync(
        Guid userId, Investment investment, SaveInvestmentRequest request)
    {
        var wanted = new List<(Guid HeadId, InvestmentDirection Direction)>();

        foreach (var id in request.ContributionHeadIds.Distinct())
        {
            await ValidateHeadAsync(userId, investment.Id, id, InvestmentDirection.Contribution);
            wanted.Add((id, InvestmentDirection.Contribution));
        }

        foreach (var id in request.ReturnHeadIds.Distinct())
        {
            if (wanted.Any(w => w.HeadId == id))
            {
                throw new ValidationAppException(
                    "A head cannot be both where the money goes in and where it comes back.");
            }

            await ValidateHeadAsync(userId, investment.Id, id, InvestmentDirection.Return);
            wanted.Add((id, InvestmentDirection.Return));
        }

        var existing = await _db.InvestmentHeads
            .Where(ih => ih.InvestmentId == investment.Id)
            .ToListAsync();

        _db.InvestmentHeads.RemoveRange(
            existing.Where(e => !wanted.Any(w => w.HeadId == e.HeadId && w.Direction == e.Direction)));

        _db.InvestmentHeads.AddRange(wanted
            .Where(w => !existing.Any(e => e.HeadId == w.HeadId && e.Direction == w.Direction))
            .Select(w => new InvestmentHead
            {
                InvestmentId = investment.Id,
                HeadId = w.HeadId,
                Direction = w.Direction,
            }));
    }

    private async Task ValidateHeadAsync(
        Guid userId, Guid investmentId, Guid headId, InvestmentDirection direction)
    {
        // Not IgnoreQueryFilters: an archived head can stay linked but must not be newly
        // chosen, exactly as it can hold history but take no new rows.
        var head = await _db.Heads
            .Include(h => h.Category)
            .FirstOrDefaultAsync(h => h.Id == headId && h.Category.UserId == userId)
            ?? throw new NotFoundAppException("Head not found.");

        var required = direction == InvestmentDirection.Contribution
            ? CategoryKind.Expense
            : CategoryKind.Income;

        if (head.Category.Kind != required)
        {
            throw new ValidationAppException(direction == InvestmentDirection.Contribution
                ? $"“{head.Name}” is an income head. Money you put in is spending, so pick a head from a spending category."
                : $"“{head.Name}” is a spending head. Money coming back is income, so pick a head from an income category.");
        }

        var claimedBy = await _db.InvestmentHeads
            .Include(ih => ih.Investment)
            .FirstOrDefaultAsync(ih => ih.HeadId == headId && ih.InvestmentId != investmentId);

        if (claimedBy is not null)
        {
            throw new ConflictAppException(
                $"“{head.Name}” already belongs to “{claimedBy.Investment.Name}”. Every row on a head counts towards its investment, so a head can only belong to one.");
        }
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Validate(SaveInvestmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationAppException(request.Kind == InvestmentKind.Lend
                ? "Give it a name so you can tell it apart."
                : "Give the investment a name so you can tell it apart.");
        }
    }
}
