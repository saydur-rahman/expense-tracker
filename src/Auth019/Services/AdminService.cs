using Auth019.Data;
using Auth019.Dtos;
using Auth019.Exceptions;
using Auth019.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Core;
using OpenIddict.EntityFrameworkCore.Models;

namespace Auth019.Services;

public class AdminService : IAdminService
{
    private const int MaxPageSize = 100;

    private readonly AuthDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOpenIddictTokenManager _tokenManager;

    public AdminService(
        AuthDbContext db,
        UserManager<ApplicationUser> userManager,
        IOpenIddictTokenManager tokenManager)
    {
        _db = db;
        _userManager = userManager;
        _tokenManager = tokenManager;
    }

    public async Task<AdminUserListDto> ListUsersAsync(AdminUserQuery query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var users = _db.Users.AsQueryable();

        if (!query.IncludeInactive)
        {
            users = users.Where(u => u.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            users = users.Where(u =>
                (u.Email != null && EF.Functions.Like(u.Email, $"%{term}%")) ||
                EF.Functions.Like(u.DisplayName, $"%{term}%"));
        }

        var totalCount = await users.CountAsync();

        var pageOfUsers = await users
            .OrderByDescending(u => u.LastLoginAtUtc ?? u.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = new List<AdminUserDto>(pageOfUsers.Count);
        foreach (var user in pageOfUsers)
        {
            items.Add(await ToDtoAsync(user));
        }

        return new AdminUserListDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<AdminUserDto> GetUserAsync(Guid userId)
        => await ToDtoAsync(await GetUserOrThrowAsync(userId));

    public async Task<AdminUserDto> SetActiveAsync(Guid adminUserId, Guid userId, bool isActive)
    {
        if (adminUserId == userId)
        {
            throw new ValidationAppException("You cannot deactivate your own account.");
        }

        var user = await GetUserOrThrowAsync(userId);

        user.IsActive = isActive;
        user.DeactivatedAtUtc = isActive ? null : DateTime.UtcNow;
        await _db.SaveChangesAsync();

        if (!isActive)
        {
            // Revoke issued tokens so a deactivated user can't keep using a session
            // they already had open. The authorize/token endpoints re-check IsActive
            // as well, so this closes the window rather than being the only defence.
            await foreach (var token in _tokenManager.FindBySubjectAsync(userId.ToString()))
            {
                await _tokenManager.TryRevokeAsync(token);
            }
        }

        return await ToDtoAsync(user);
    }

    private async Task<ApplicationUser> GetUserOrThrowAsync(Guid userId)
        => await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
           ?? throw new NotFoundAppException("User not found.");

    private async Task<AdminUserDto> ToDtoAsync(ApplicationUser user) => new()
    {
        Id = user.Id,
        Email = user.Email ?? string.Empty,
        DisplayName = user.DisplayName,
        Roles = (await _userManager.GetRolesAsync(user)).ToList(),
        IsActive = user.IsActive,
        LastLoginAtUtc = user.LastLoginAtUtc,
        DeactivatedAtUtc = user.DeactivatedAtUtc,
        CreatedAtUtc = user.CreatedAtUtc,
    };
}
