using ExpenseTracker019.Api.Dtos.Categories;
using ExpenseTracker019.Api.Models;

namespace ExpenseTracker019.Api.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> ListAsync(Guid userId, bool includeArchived, CategoryKind kind);
    Task<CategoryDto> CreateAsync(Guid userId, string name, CategoryKind kind);
    Task<CategoryDto> RenameAsync(Guid userId, Guid categoryId, string name);
    Task ArchiveAsync(Guid userId, Guid categoryId);

    Task<HeadDto> CreateHeadAsync(Guid userId, Guid categoryId, string name);
    Task<HeadDto> RenameHeadAsync(Guid userId, Guid headId, string name);
    Task ArchiveHeadAsync(Guid userId, Guid headId);
}
