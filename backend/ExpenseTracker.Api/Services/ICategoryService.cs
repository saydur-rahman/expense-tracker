using ExpenseTracker.Api.Dtos.Categories;

namespace ExpenseTracker.Api.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> ListAsync(Guid userId, bool includeArchived);
    Task<CategoryDto> CreateAsync(Guid userId, string name);
    Task<CategoryDto> RenameAsync(Guid userId, Guid categoryId, string name);
    Task ArchiveAsync(Guid userId, Guid categoryId);

    Task<HeadDto> CreateHeadAsync(Guid userId, Guid categoryId, string name);
    Task<HeadDto> RenameHeadAsync(Guid userId, Guid headId, string name);
    Task ArchiveHeadAsync(Guid userId, Guid headId);
}
