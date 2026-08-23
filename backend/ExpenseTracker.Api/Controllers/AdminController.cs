using ExpenseTracker.Api.Dtos.Admin;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = AppRoles.Admin)]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly ICurrentUser _currentUser;

    public AdminController(IAdminService adminService, ICurrentUser currentUser)
    {
        _adminService = adminService;
        _currentUser = currentUser;
    }

    [HttpGet("users")]
    public async Task<ActionResult<AdminUserListDto>> ListUsers([FromQuery] AdminUserQuery query)
        => Ok(await _adminService.ListUsersAsync(query));

    [HttpGet("users/{id:guid}")]
    public async Task<ActionResult<AdminUserDto>> GetUser(Guid id)
        => Ok(await _adminService.GetUserAsync(id));

    [HttpPost("users/{id:guid}/deactivate")]
    public async Task<ActionResult<AdminUserDto>> Deactivate(Guid id)
        => Ok(await _adminService.SetActiveAsync(_currentUser.Id, id, isActive: false));

    [HttpPost("users/{id:guid}/reactivate")]
    public async Task<ActionResult<AdminUserDto>> Reactivate(Guid id)
        => Ok(await _adminService.SetActiveAsync(_currentUser.Id, id, isActive: true));

    [HttpPost("users/{id:guid}/impersonate")]
    public async Task<ActionResult<ImpersonationResponse>> Impersonate(Guid id)
        => Ok(await _adminService.ImpersonateAsync(_currentUser.Id, id));
}
