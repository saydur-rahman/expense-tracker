using Auth019.Dtos;
using Auth019.Exceptions;
using Auth019.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Auth019.Controllers;

/// <summary>
/// User administration. Lives in Auth019 because Auth019 owns user data —
/// the expense API has no user table to administer.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
           Policy = AuthPolicies.AuthAdmin)]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet("users")]
    public async Task<ActionResult<AdminUserListDto>> ListUsers([FromQuery] AdminUserQuery query)
        => Ok(await _adminService.ListUsersAsync(query));

    [HttpGet("users/{id:guid}")]
    public async Task<ActionResult<AdminUserDto>> GetUser(Guid id)
        => Ok(await _adminService.GetUserAsync(id));

    [HttpPost("users/{id:guid}/deactivate")]
    public async Task<ActionResult<AdminUserDto>> Deactivate(Guid id)
        => Ok(await _adminService.SetActiveAsync(CurrentUserId, id, isActive: false));

    [HttpPost("users/{id:guid}/reactivate")]
    public async Task<ActionResult<AdminUserDto>> Reactivate(Guid id)
        => Ok(await _adminService.SetActiveAsync(CurrentUserId, id, isActive: true));

    private Guid CurrentUserId => Guid.TryParse(User.GetClaim(Claims.Subject), out var id)
        ? id
        : throw new ForbiddenAppException("Not authenticated.");
}
