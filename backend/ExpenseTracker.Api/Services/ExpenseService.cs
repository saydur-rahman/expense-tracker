using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos.Expenses;
using ExpenseTracker.Api.Exceptions;
using ExpenseTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services;

public class ExpenseService : IExpenseService
{
    private const int MaxPageSize = 100;

    private readonly AppDbContext _db;

    public ExpenseService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ExpenseListDto> ListAsync(Guid userId, ExpenseQuery query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        // Archived heads/categories must still show in history, so the soft-delete
        // filters are bypassed here and the join is done explicitly.
        var expenses = _db.Expenses
            .IgnoreQueryFilters()
            .Where(e => e.UserId == userId);

        if (query.From is not null) expenses = expenses.Where(e => e.ExpenseDate >= query.From);
        if (query.To is not null) expenses = expenses.Where(e => e.ExpenseDate <= query.To);
        if (query.HeadId is not null) expenses = expenses.Where(e => e.HeadId == query.HeadId);
        if (query.CategoryId is not null) expenses = expenses.Where(e => e.Head.CategoryId == query.CategoryId);

        var totalCount = await expenses.CountAsync();
        var totalAmount = await expenses.SumAsync(e => (decimal?)e.Amount) ?? 0m;

        var items = await expenses
            .OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new ExpenseDto
            {
                Id = e.Id,
                HeadId = e.HeadId,
                HeadName = e.Head.Name,
                CategoryId = e.Head.CategoryId,
                CategoryName = e.Head.Category.Name,
                Amount = e.Amount,
                ExpenseDate = e.ExpenseDate,
                Note = e.Note,
            })
            .ToListAsync();

        return new ExpenseListDto
        {
            Items = items,
            TotalCount = totalCount,
            TotalAmount = totalAmount,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<ExpenseDto> CreateAsync(Guid userId, SaveExpenseRequest request)
    {
        Validate(request);
        var head = await GetActiveOwnedHeadAsync(userId, request.HeadId);

        var expense = new Expense
        {
            UserId = userId,
            HeadId = head.Id,
            Amount = request.Amount,
            ExpenseDate = request.ExpenseDate,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
        };

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();

        return await GetDtoAsync(userId, expense.Id);
    }

    public async Task<ExpenseDto> UpdateAsync(Guid userId, Guid expenseId, SaveExpenseRequest request)
    {
        Validate(request);

        var expense = await _db.Expenses
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == expenseId && e.UserId == userId)
            ?? throw new NotFoundAppException("Expense not found.");

        var head = await GetActiveOwnedHeadAsync(userId, request.HeadId);

        expense.HeadId = head.Id;
        expense.Amount = request.Amount;
        expense.ExpenseDate = request.ExpenseDate;
        expense.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        expense.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return await GetDtoAsync(userId, expense.Id);
    }

    public async Task DeleteAsync(Guid userId, Guid expenseId)
    {
        var expense = await _db.Expenses
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == expenseId && e.UserId == userId)
            ?? throw new NotFoundAppException("Expense not found.");

        _db.Expenses.Remove(expense);
        await _db.SaveChangesAsync();
    }

    private static void Validate(SaveExpenseRequest request)
    {
        if (request.Amount <= 0)
        {
            throw new ValidationAppException("Amount must be greater than zero.");
        }
    }

    /// <summary>
    /// New/edited expenses must target a live head — archived heads keep their history
    /// but can't take new spending.
    /// </summary>
    private async Task<Head> GetActiveOwnedHeadAsync(Guid userId, Guid headId)
        => await _db.Heads.FirstOrDefaultAsync(h => h.Id == headId && h.Category.UserId == userId)
           ?? throw new NotFoundAppException("Head not found.");

    private async Task<ExpenseDto> GetDtoAsync(Guid userId, Guid expenseId)
        => await _db.Expenses
               .IgnoreQueryFilters()
               .Where(e => e.Id == expenseId && e.UserId == userId)
               .Select(e => new ExpenseDto
               {
                   Id = e.Id,
                   HeadId = e.HeadId,
                   HeadName = e.Head.Name,
                   CategoryId = e.Head.CategoryId,
                   CategoryName = e.Head.Category.Name,
                   Amount = e.Amount,
                   ExpenseDate = e.ExpenseDate,
                   Note = e.Note,
               })
               .FirstAsync();
}
