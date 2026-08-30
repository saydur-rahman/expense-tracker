using ExpenseTracker019.Api.Data;
using ExpenseTracker019.Api.Dtos.Categories;
using ExpenseTracker019.Api.Exceptions;
using ExpenseTracker019.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker019.Api.Services;

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;

    public CategoryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CategoryDto>> ListAsync(Guid userId, bool includeArchived, CategoryKind kind)
    {
        var query = _db.Categories.Include(c => c.Heads).Where(c => c.UserId == userId && c.Kind == kind);

        if (includeArchived)
        {
            query = _db.Categories
                .IgnoreQueryFilters()
                .Include(c => c.Heads)
                .Where(c => c.UserId == userId && c.Kind == kind);
        }

        var categories = await query
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .ToListAsync();

        return categories.Select(c => ToDto(c, includeArchived)).ToList();
    }

    public async Task<CategoryDto> CreateAsync(Guid userId, string name, CategoryKind kind)
    {
        name = Normalize(name);
        await EnsureCategoryNameAvailableAsync(userId, kind, name, excludingId: null);

        // Ordering runs per ledger, so adding an income category doesn't push a
        // number onto the end of the spending list.
        var maxOrder = await _db.Categories
            .Where(c => c.UserId == userId && c.Kind == kind)
            .Select(c => (int?)c.DisplayOrder)
            .MaxAsync() ?? 0;

        var category = new Category
        {
            UserId = userId,
            Name = name,
            Kind = kind,
            DisplayOrder = maxOrder + 1,
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return ToDto(category, includeArchived: false);
    }

    public async Task<CategoryDto> RenameAsync(Guid userId, Guid categoryId, string name)
    {
        name = Normalize(name);
        var category = await GetOwnedCategoryAsync(userId, categoryId);
        await EnsureCategoryNameAvailableAsync(userId, category.Kind, name, excludingId: categoryId);

        category.Name = name;
        await _db.SaveChangesAsync();

        return ToDto(category, includeArchived: false);
    }

    public async Task ArchiveAsync(Guid userId, Guid categoryId)
    {
        var category = await _db.Categories
            .Include(c => c.Heads)
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId)
            ?? throw new NotFoundAppException("Category not found.");

        var now = DateTime.UtcNow;
        category.IsArchived = true;
        category.ArchivedAtUtc = now;

        // Archiving a category hides its heads too, so the pair stays consistent
        // in every list view; the rows (and their expenses/budgets) are untouched.
        foreach (var head in category.Heads.Where(h => !h.IsArchived))
        {
            head.IsArchived = true;
            head.ArchivedAtUtc = now;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<HeadDto> CreateHeadAsync(Guid userId, Guid categoryId, string name)
    {
        name = Normalize(name);
        var category = await GetOwnedCategoryAsync(userId, categoryId);
        await EnsureHeadNameAvailableAsync(categoryId, name, excludingId: null);

        var maxOrder = await _db.Heads
            .Where(h => h.CategoryId == categoryId)
            .Select(h => (int?)h.DisplayOrder)
            .MaxAsync() ?? 0;

        var head = new Head
        {
            CategoryId = category.Id,
            Name = name,
            DisplayOrder = maxOrder + 1,
        };

        _db.Heads.Add(head);
        await _db.SaveChangesAsync();

        return ToDto(head);
    }

    public async Task<HeadDto> RenameHeadAsync(Guid userId, Guid headId, string name)
    {
        name = Normalize(name);
        var head = await GetOwnedHeadAsync(userId, headId);
        await EnsureHeadNameAvailableAsync(head.CategoryId, name, excludingId: headId);

        head.Name = name;
        await _db.SaveChangesAsync();

        return ToDto(head);
    }

    public async Task ArchiveHeadAsync(Guid userId, Guid headId)
    {
        var head = await GetOwnedHeadAsync(userId, headId);
        head.IsArchived = true;
        head.ArchivedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private async Task<Category> GetOwnedCategoryAsync(Guid userId, Guid categoryId)
        => await _db.Categories.FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId)
           ?? throw new NotFoundAppException("Category not found.");

    private async Task<Head> GetOwnedHeadAsync(Guid userId, Guid headId)
        => await _db.Heads
               .Include(h => h.Category)
               .FirstOrDefaultAsync(h => h.Id == headId && h.Category.UserId == userId)
           ?? throw new NotFoundAppException("Head not found.");

    // Names only have to be unique within their own ledger — "Other" can sit in both.
    private async Task EnsureCategoryNameAvailableAsync(Guid userId, CategoryKind kind, string name, Guid? excludingId)
    {
        var taken = await _db.Categories
            .AnyAsync(c => c.UserId == userId && c.Kind == kind && c.Name == name && c.Id != excludingId);
        if (taken)
        {
            throw new ConflictAppException($"A category named \"{name}\" already exists.");
        }
    }

    private async Task EnsureHeadNameAvailableAsync(Guid categoryId, string name, Guid? excludingId)
    {
        var taken = await _db.Heads
            .AnyAsync(h => h.CategoryId == categoryId && h.Name == name && h.Id != excludingId);
        if (taken)
        {
            throw new ConflictAppException($"A head named \"{name}\" already exists in this category.");
        }
    }

    private static string Normalize(string name)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationAppException("Name is required.");
        }
        return name;
    }

    private static CategoryDto ToDto(Category category, bool includeArchived) => new()
    {
        Id = category.Id,
        Name = category.Name,
        Kind = category.Kind,
        IsArchived = category.IsArchived,
        Heads = category.Heads
            .Where(h => includeArchived || !h.IsArchived)
            .OrderBy(h => h.DisplayOrder).ThenBy(h => h.Name)
            .Select(ToDto)
            .ToList(),
    };

    private static HeadDto ToDto(Head head) => new()
    {
        Id = head.Id,
        CategoryId = head.CategoryId,
        Name = head.Name,
        IsArchived = head.IsArchived,
    };
}
