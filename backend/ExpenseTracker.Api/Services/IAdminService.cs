using ExpenseTracker.Api.Dtos.Admin;

namespace ExpenseTracker.Api.Services;

public class AdminUserQuery
{
    public string? Search { get; set; }
    public bool IncludeInactive { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public interface IAdminService
{
    Task<AdminUserListDto> ListUsersAsync(AdminUserQuery query);
    Task<AdminUserDto> GetUserAsync(Guid userId);
    Task<AdminUserDto> SetActiveAsync(Guid adminUserId, Guid userId, bool isActive);
    Task<ImpersonationResponse> ImpersonateAsync(Guid adminUserId, Guid targetUserId);
}
