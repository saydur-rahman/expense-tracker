using ExpenseTracker019.Api.Data;
using ExpenseTracker019.Api.Dtos.Incomes;
using ExpenseTracker019.Api.Exceptions;
using ExpenseTracker019.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker019.Api.Services;

/// <summary>
/// The income ledger — a mirror of <see cref="ExpenseService"/> with no budget rules,
/// since budgets cap spending and there is nothing to cap on money coming in.
/// </summary>
public class IncomeService : IIncomeService
{
    private const int MaxPageSize = 100;

    private readonly AppDbContext _db;

    public IncomeService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IncomeListDto> ListAsync(Guid userId, IncomeQuery query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        // Archived heads/categories must still show in history, so the soft-delete
        // filters are bypassed here and the join is done explicitly.
        var incomes = _db.Incomes
            .IgnoreQueryFilters()
            .Where(i => i.UserId == userId);

        if (query.From is not null) incomes = incomes.Where(i => i.IncomeDate >= query.From);
        if (query.To is not null) incomes = incomes.Where(i => i.IncomeDate <= query.To);
        if (query.HeadId is not null) incomes = incomes.Where(i => i.HeadId == query.HeadId);
        if (query.CategoryId is not null) incomes = incomes.Where(i => i.Head.CategoryId == query.CategoryId);

        var totalCount = await incomes.CountAsync();
        var totalAmount = await incomes.SumAsync(i => (decimal?)i.Amount) ?? 0m;

        var items = await incomes
            .OrderByDescending(i => i.IncomeDate).ThenByDescending(i => i.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new IncomeDto
            {
                Id = i.Id,
                HeadId = i.HeadId,
                HeadName = i.Head.Name,
                CategoryId = i.Head.CategoryId,
                CategoryName = i.Head.Category.Name,
                Amount = i.Amount,
                IncomeDate = i.IncomeDate,
                Note = i.Note,
            })
            .ToListAsync();

        return new IncomeListDto
        {
            Items = items,
            TotalCount = totalCount,
            TotalAmount = totalAmount,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<IncomeDto> CreateAsync(Guid userId, SaveIncomeRequest request)
    {
        Validate(request);
        var head = await GetActiveOwnedIncomeHeadAsync(userId, request.HeadId);

        var income = new Income
        {
            UserId = userId,
            HeadId = head.Id,
            Amount = request.Amount,
            IncomeDate = request.IncomeDate,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
        };

        _db.Incomes.Add(income);
        await _db.SaveChangesAsync();

        return await GetDtoAsync(userId, income.Id);
    }

    public async Task<IncomeDto> UpdateAsync(Guid userId, Guid incomeId, SaveIncomeRequest request)
    {
        Validate(request);

        var income = await _db.Incomes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == incomeId && i.UserId == userId)
            ?? throw new NotFoundAppException("Income not found.");

        var head = await GetActiveOwnedIncomeHeadAsync(userId, request.HeadId);

        income.HeadId = head.Id;
        income.Amount = request.Amount;
        income.IncomeDate = request.IncomeDate;
        income.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        income.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return await GetDtoAsync(userId, income.Id);
    }

    public async Task DeleteAsync(Guid userId, Guid incomeId)
    {
        var income = await _db.Incomes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == incomeId && i.UserId == userId)
            ?? throw new NotFoundAppException("Income not found.");

        _db.Incomes.Remove(income);
        await _db.SaveChangesAsync();
    }

    private static void Validate(SaveIncomeRequest request)
    {
        if (request.Amount <= 0)
        {
            throw new ValidationAppException("Amount must be greater than zero.");
        }
    }

    /// <summary>
    /// New/edited income must target a live head on the <em>income</em> side. Pointing
    /// income at a spending head would corrupt every total on the dashboard, so the
    /// kind is checked here rather than trusted from the client.
    /// </summary>
    private async Task<Head> GetActiveOwnedIncomeHeadAsync(Guid userId, Guid headId)
    {
        var head = await _db.Heads
            .Include(h => h.Category)
            .FirstOrDefaultAsync(h => h.Id == headId && h.Category.UserId == userId)
            ?? throw new NotFoundAppException("Head not found.");

        if (head.Category.Kind != CategoryKind.Income)
        {
            throw new ValidationAppException(
                "That head belongs to a spending category. Pick one from an income category.");
        }

        return head;
    }

    private async Task<IncomeDto> GetDtoAsync(Guid userId, Guid incomeId)
        => await _db.Incomes
               .IgnoreQueryFilters()
               .Where(i => i.Id == incomeId && i.UserId == userId)
               .Select(i => new IncomeDto
               {
                   Id = i.Id,
                   HeadId = i.HeadId,
                   HeadName = i.Head.Name,
                   CategoryId = i.Head.CategoryId,
                   CategoryName = i.Head.Category.Name,
                   Amount = i.Amount,
                   IncomeDate = i.IncomeDate,
                   Note = i.Note,
               })
               .FirstAsync();
}
