using ExpenseTracker019.Api.Data;
using ExpenseTracker019.Api.Dtos.Loans;
using ExpenseTracker019.Api.Exceptions;
using ExpenseTracker019.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker019.Api.Services;

/// <summary>
/// Loans, and the expenses that repay them.
/// </summary>
/// <remarks>
/// Nothing here writes to the expense ledger and nothing stores a balance. A repayment is
/// simply an expense on a linked head dated on or after the loan was taken, and every
/// figure is a SUM over those rows at read time — so editing or deleting an old expense
/// moves the loan with it, with nothing to keep in sync.
/// </remarks>
public class LoanService : ILoanService
{
    private const int MaxPageSize = 100;

    private readonly AppDbContext _db;
    private readonly IMonthCycleService _monthCycle;

    public LoanService(AppDbContext db, IMonthCycleService monthCycle)
    {
        _db = db;
        _monthCycle = monthCycle;
    }

    public async Task<List<LoanDto>> ListAsync(Guid userId)
    {
        var loans = await LoansWithHeads(userId)
            .OrderBy(l => l.Name)
            .ToListAsync();

        var repaidByLoan = await RepaidByLoanAsync(userId);

        return loans.Select(loan => ToDto(loan, repaidByLoan.GetValueOrDefault(loan.Id))).ToList();
    }

    public async Task<LoanPortfolioDto> GetPortfolioAsync(Guid userId, Guid periodId)
    {
        var period = await _monthCycle.GetPeriodByIdAsync(userId, periodId);

        var loans = await _db.Loans.Where(l => l.UserId == userId)
            .Select(l => new { l.Id, l.AmountTaken })
            .ToListAsync();

        var repaid = await RepaidByLoanAsync(userId);
        var paidInPeriod = await RepaidByLoanAsync(userId, period.StartDate, period.EndDate);

        var borrowed = loans.Sum(l => l.AmountTaken);
        var repaidTotal = loans.Sum(l => repaid.GetValueOrDefault(l.Id));

        return new LoanPortfolioDto
        {
            PeriodLabel = period.Label,
            StartDate = period.StartDate,
            EndDate = period.EndDate,
            Count = loans.Count,
            SettledCount = loans.Count(l =>
                LoanMath.IsSettled(l.AmountTaken, repaid.GetValueOrDefault(l.Id))),
            Borrowed = borrowed,
            Repaid = repaidTotal,
            // Summed per loan rather than borrowed - repaid, so one overpaid loan cannot
            // quietly cancel out what is still owed on another.
            Outstanding = loans.Sum(l =>
                LoanMath.Outstanding(l.AmountTaken, repaid.GetValueOrDefault(l.Id))),
            PaidInPeriod = loans.Sum(l => paidInPeriod.GetValueOrDefault(l.Id)),
            PercentSettled = LoanMath.PercentSettled(borrowed, repaidTotal),
        };
    }

    public async Task<LoanDetailDto> GetAsync(Guid userId, Guid loanId)
    {
        var loan = await GetOwnedAsync(userId, loanId);
        var payments = PaymentsFor(userId, loan);

        return new LoanDetailDto
        {
            Loan = ToDto(loan, await SumAsync(payments)),
            TransactionCount = await payments.CountAsync(),
            RecentTransactions = await OrderAndProject(payments).Take(20).ToListAsync(),
        };
    }

    public async Task<LoanTransactionListDto> ListTransactionsAsync(
        Guid userId, Guid loanId, LoanTransactionQuery query)
    {
        var loan = await GetOwnedAsync(userId, loanId);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var payments = PaymentsFor(userId, loan);

        if (query.From is { } from)
        {
            payments = payments.Where(e => e.ExpenseDate >= from);
        }

        if (query.To is { } to)
        {
            payments = payments.Where(e => e.ExpenseDate <= to);
        }

        return new LoanTransactionListDto
        {
            TotalCount = await payments.CountAsync(),
            TotalAmount = await SumAsync(payments),
            Page = page,
            PageSize = pageSize,
            Items = await OrderAndProject(payments)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(),
        };
    }

    public async Task<IReadOnlyList<PeriodTotalDto>> ListByPeriodAsync(Guid userId, Guid loanId, int count)
    {
        var loan = await GetOwnedAsync(userId, loanId);

        // Computed windows, never resolved ones: resolving would create BudgetPeriod rows
        // and run carry-forward, and merely drawing a chart must not write.
        var windows = await _monthCycle.ListRecentWindowsAsync(userId, Math.Clamp(count, 1, 60));
        var payments = await PaymentsFor(userId, loan)
            .Select(e => new { e.ExpenseDate, e.Amount })
            .ToListAsync();

        return windows
            .OrderBy(w => w.StartDate)
            .Select(w => new PeriodTotalDto
            {
                Label = w.Label,
                StartDate = w.StartDate,
                EndDate = w.EndDate,
                Amount = payments
                    .Where(p => p.ExpenseDate >= w.StartDate && p.ExpenseDate <= w.EndDate)
                    .Sum(p => p.Amount),
            })
            .ToList();
    }

    public async Task<LoanDto> CreateAsync(Guid userId, SaveLoanRequest request)
    {
        Validate(request);

        var loan = new Loan
        {
            UserId = userId,
            Name = request.Name.Trim(),
            Lender = Clean(request.Lender),
            AmountTaken = request.AmountTaken,
            TakenOn = request.TakenOn,
            Remark = Clean(request.Remark),
        };

        _db.Loans.Add(loan);
        await ApplyHeadsAsync(userId, loan, request.HeadIds);
        await _db.SaveChangesAsync();

        return (await GetAsync(userId, loan.Id)).Loan;
    }

    public async Task<LoanDto> UpdateAsync(Guid userId, Guid loanId, SaveLoanRequest request)
    {
        Validate(request);

        var loan = await GetOwnedAsync(userId, loanId);

        loan.Name = request.Name.Trim();
        loan.Lender = Clean(request.Lender);
        loan.AmountTaken = request.AmountTaken;
        loan.TakenOn = request.TakenOn;
        loan.Remark = Clean(request.Remark);
        loan.UpdatedAtUtc = DateTime.UtcNow;

        await ApplyHeadsAsync(userId, loan, request.HeadIds);
        await _db.SaveChangesAsync();

        return (await GetAsync(userId, loan.Id)).Loan;
    }

    public async Task DeleteAsync(Guid userId, Guid loanId)
    {
        var loan = await GetOwnedAsync(userId, loanId);

        // The expenses are untouched — they were ordinary spending before this loan
        // existed and they stay ordinary spending after it is gone.
        _db.Loans.Remove(loan);
        await _db.SaveChangesAsync();
    }

    // ---- internals -------------------------------------------------------------

    private IQueryable<Loan> LoansWithHeads(Guid userId) => _db.Loans
        .Where(l => l.UserId == userId)
        .Include(l => l.Heads)
            // Filters ignored so a head archived after it was linked still names itself
            // in the loan's history, the same reason the report queries ignore them.
            .ThenInclude(lh => lh.Head)
                .ThenInclude(h => h.Category)
        .IgnoreQueryFilters();

    private async Task<Loan> GetOwnedAsync(Guid userId, Guid loanId)
        // Ownership is folded into the same predicate as the id, so someone else's loan
        // is a 404 rather than a 403 that confirms it exists.
        => await LoansWithHeads(userId).FirstOrDefaultAsync(l => l.Id == loanId)
           ?? throw new NotFoundAppException("Loan not found.");

    /// <summary>
    /// Every expense that repays this loan: on one of its heads, and dated on or after the
    /// day it was taken. Spending on that head from before the loan is not a repayment.
    /// </summary>
    private IQueryable<Expense> PaymentsFor(Guid userId, Loan loan)
    {
        var headIds = loan.Heads.Select(h => h.HeadId).ToList();

        return _db.Expenses
            .IgnoreQueryFilters()
            .Include(e => e.Head).ThenInclude(h => h.Category)
            .Where(e => e.UserId == userId
                        && headIds.Contains(e.HeadId)
                        && e.ExpenseDate >= loan.TakenOn);
    }

    /// <summary>
    /// One pass over every loan's payments, so a list of loans is not N queries. Bounded by
    /// <paramref name="from"/>/<paramref name="to"/> to answer "and what about this cycle?"
    /// with the same query rather than a second shape of it.
    /// </summary>
    private async Task<Dictionary<Guid, decimal>> RepaidByLoanAsync(
        Guid userId, DateOnly? from = null, DateOnly? to = null)
    {
        var links = await _db.LoanHeads
            .IgnoreQueryFilters()
            .Where(lh => lh.Loan.UserId == userId)
            .Select(lh => new { lh.LoanId, lh.HeadId, lh.Loan.TakenOn })
            .ToListAsync();

        if (links.Count == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        var headIds = links.Select(l => l.HeadId).Distinct().ToList();

        var expenses = await _db.Expenses
            .IgnoreQueryFilters()
            .Where(e => e.UserId == userId && headIds.Contains(e.HeadId))
            .Select(e => new { e.HeadId, e.ExpenseDate, e.Amount })
            .ToListAsync();

        return links
            .GroupBy(l => l.LoanId)
            .ToDictionary(
                g => g.Key,
                g => expenses
                    .Where(e => (from is null || e.ExpenseDate >= from)
                                && (to is null || e.ExpenseDate <= to)
                                // Never before the loan itself, whatever window is asked for.
                                && g.Any(l => l.HeadId == e.HeadId && e.ExpenseDate >= l.TakenOn))
                    .Sum(e => e.Amount));
    }

    private static async Task<decimal> SumAsync(IQueryable<Expense> payments)
        => await payments.SumAsync(e => (decimal?)e.Amount) ?? 0m;

    private static IQueryable<LoanTransactionDto> OrderAndProject(IQueryable<Expense> payments)
        => payments
            .OrderByDescending(e => e.ExpenseDate)
            .ThenByDescending(e => e.CreatedAtUtc)
            .Select(e => new LoanTransactionDto
            {
                Id = e.Id,
                HeadId = e.HeadId,
                HeadName = e.Head.Name,
                CategoryName = e.Head.Category.Name,
                Amount = e.Amount,
                Date = e.ExpenseDate,
                Note = e.Note,
            });

    private static LoanDto ToDto(Loan loan, decimal repaid) => new()
    {
        Id = loan.Id,
        Name = loan.Name,
        Lender = loan.Lender,
        AmountTaken = loan.AmountTaken,
        TakenOn = loan.TakenOn,
        Remark = loan.Remark,
        Repaid = repaid,
        Outstanding = LoanMath.Outstanding(loan.AmountTaken, repaid),
        PercentSettled = LoanMath.PercentSettled(loan.AmountTaken, repaid),
        Overpaid = LoanMath.Overpaid(loan.AmountTaken, repaid),
        IsSettled = LoanMath.IsSettled(loan.AmountTaken, repaid),
        Heads = loan.Heads
            .OrderBy(h => h.Head.Category.Name).ThenBy(h => h.Head.Name)
            .Select(h => new LinkedHeadDto
            {
                HeadId = h.HeadId,
                HeadName = h.Head.Name,
                CategoryId = h.Head.CategoryId,
                CategoryName = h.Head.Category.Name,
                IsArchived = h.Head.IsArchived || h.Head.Category.IsArchived,
            })
            .ToList(),
    };

    /// <summary>Replaces the linked heads wholesale, validating each one first.</summary>
    private async Task ApplyHeadsAsync(Guid userId, Loan loan, List<Guid> headIds)
    {
        var wanted = headIds.Distinct().ToList();

        foreach (var headId in wanted)
        {
            // Not IgnoreQueryFilters: an archived head can stay linked but must not be
            // newly chosen, exactly as it can hold history but take no new expenses.
            var head = await _db.Heads
                .Include(h => h.Category)
                .FirstOrDefaultAsync(h => h.Id == headId && h.Category.UserId == userId)
                ?? throw new NotFoundAppException("Head not found.");

            if (head.Category.Kind != CategoryKind.Expense)
            {
                throw new ValidationAppException(
                    $"“{head.Name}” is an income head. A loan is repaid out of spending, so pick a head from a spending category.");
            }

            var claimedBy = await _db.LoanHeads
                .Include(lh => lh.Loan)
                .FirstOrDefaultAsync(lh => lh.HeadId == headId && lh.LoanId != loan.Id);

            if (claimedBy is not null)
            {
                throw new ConflictAppException(
                    $"“{head.Name}” already repays “{claimedBy.Loan.Name}”. Every expense on a head counts towards its loan, so a head can only belong to one.");
            }
        }

        var existing = await _db.LoanHeads.Where(lh => lh.LoanId == loan.Id).ToListAsync();

        _db.LoanHeads.RemoveRange(existing.Where(lh => !wanted.Contains(lh.HeadId)));
        _db.LoanHeads.AddRange(wanted
            .Where(id => existing.All(lh => lh.HeadId != id))
            .Select(id => new LoanHead { LoanId = loan.Id, HeadId = id }));
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Validate(SaveLoanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationAppException("Give the loan a name so you can tell it apart.");
        }

        if (request.AmountTaken <= 0m)
        {
            throw new ValidationAppException("How much did you borrow? It has to be more than zero.");
        }
    }
}
