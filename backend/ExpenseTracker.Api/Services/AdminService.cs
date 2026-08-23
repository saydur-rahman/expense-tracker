using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos.Admin;
using ExpenseTracker.Api.Exceptions;
using ExpenseTracker.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services;

public class AdminService : IAdminService
{
    private const int MaxPageSize = 100;

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;

    public AdminService(AppDbContext db, UserManager<ApplicationUser> userManager, ITokenService tokenService)
    {
        _db = db;
        _userManager = userManager;
        _tokenService = tokenService;
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

        if (!isActive)
        {
            // Revoke outstanding refresh tokens so a deactivated user can't keep
            // minting access tokens from a session they already had open.
            var activeTokens = await _db.RefreshTokens
                .Where(rt => rt.UserId == userId && rt.RevokedAtUtc == null)
                .ToListAsync();
            foreach (var token in activeTokens)
            {
                token.RevokedAtUtc = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();

        return await ToDtoAsync(user);
    }

    public async Task<ImpersonationResponse> ImpersonateAsync(Guid adminUserId, Guid targetUserId)
    {
        if (adminUserId == targetUserId)
        {
            throw new ValidationAppException("You are already signed in as this account.");
        }

        var target = await GetUserOrThrowAsync(targetUserId);

        if (!target.IsActive)
        {
            throw new ValidationAppException("This account is deactivated. Reactivate it before viewing as this user.");
        }

        // Impersonating another admin would let one admin borrow another's authority.
        if (await _userManager.IsInRoleAsync(target, AppRoles.Admin))
        {
            throw new ForbiddenAppException("Admin accounts cannot be impersonated.");
        }

        var token = _tokenService.GenerateImpersonationToken(target, adminUserId);

        return new ImpersonationResponse
        {
            AccessToken = token,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15),
            Target = await ToDtoAsync(target),
        };
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
