using System.Security.Claims;
using ExpenseTracker.Api.Dtos.Auth;
using ExpenseTracker.Api.Exceptions;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthController(IAuthService authService, UserManager<ApplicationUser> userManager)
    {
        _authService = authService;
        _userManager = userManager;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
        => Ok(await _authService.RegisterAsync(request));

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
        => Ok(await _authService.LoginAsync(request));

    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> Google(GoogleLoginRequest request)
        => Ok(await _authService.GoogleLoginAsync(request));

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request)
        => Ok(await _authService.RefreshAsync(request));

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me()
    {
        var userId = User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        var user = userId is not null ? await _userManager.FindByIdAsync(userId) : null;
        if (user is null)
        {
            throw new NotFoundAppException("User not found.");
        }

        var impersonatedBy = User.FindFirstValue(AppClaims.ImpersonatedBy);
        var isImpersonating = User.HasClaim(AppClaims.ImpersonationReadOnly, "true");

        return Ok(new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName,
            // Roles reflect what this session can actually do, not what the account holds:
            // an impersonation token carries no roles, so it must not report any.
            Roles = isImpersonating ? new List<string>() : (await _userManager.GetRolesAsync(user)).ToList(),
            IsImpersonating = isImpersonating,
            ImpersonatedBy = Guid.TryParse(impersonatedBy, out var adminId) ? adminId : null,
        });
    }
}
