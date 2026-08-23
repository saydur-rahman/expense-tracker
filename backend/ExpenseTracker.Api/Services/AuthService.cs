using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos.Auth;
using ExpenseTracker.Api.Exceptions;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Options;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExpenseTracker.Api.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly GoogleAuthOptions _googleOptions;
    private readonly JwtOptions _jwtOptions;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        ITokenService tokenService,
        IOptions<GoogleAuthOptions> googleOptions,
        IOptions<JwtOptions> jwtOptions)
    {
        _userManager = userManager;
        _db = db;
        _tokenService = tokenService;
        _googleOptions = googleOptions.Value;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            throw new ConflictAppException("An account with this email already exists.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            throw new ValidationAppException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        await _userManager.AddToRoleAsync(user, AppRoles.User);

        return await IssueTokensAsync(user, stampLogin: true);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            throw new UnauthorizedAppException("Invalid email or password.");
        }

        EnsureActive(user);

        return await IssueTokensAsync(user, stampLogin: true);
    }

    public async Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest request)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _googleOptions.ClientId },
            });
        }
        catch (InvalidJwtException)
        {
            throw new UnauthorizedAppException("Invalid Google sign-in token.");
        }

        var user = await _userManager.FindByLoginAsync("Google", payload.Subject);

        if (user is null && payload.EmailVerified)
        {
            user = await _userManager.FindByEmailAsync(payload.Email);
        }

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = payload.Email,
                Email = payload.Email,
                EmailConfirmed = payload.EmailVerified,
                DisplayName = string.IsNullOrWhiteSpace(payload.Name) ? payload.Email : payload.Name,
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                throw new ValidationAppException(string.Join(" ", createResult.Errors.Select(e => e.Description)));
            }

            await _userManager.AddToRoleAsync(user, AppRoles.User);
        }

        EnsureActive(user);

        var hasGoogleLogin = (await _userManager.GetLoginsAsync(user)).Any(l => l.LoginProvider == "Google");
        if (!hasGoogleLogin)
        {
            await _userManager.AddLoginAsync(user, new UserLoginInfo("Google", payload.Subject, "Google"));
        }

        return await IssueTokensAsync(user, stampLogin: true);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request)
    {
        var tokenHash = _tokenService.HashToken(request.RefreshToken);
        var stored = await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);

        if (stored is null || !stored.IsActive)
        {
            throw new UnauthorizedAppException("Invalid or expired refresh token.");
        }

        // Checked here too: a deactivated user must not be able to mint new access
        // tokens from a refresh token they were already holding.
        EnsureActive(stored.User);

        stored.RevokedAtUtc = DateTime.UtcNow;

        return await IssueTokensAsync(stored.User, stampLogin: false);
    }

    private static void EnsureActive(ApplicationUser user)
    {
        if (!user.IsActive)
        {
            throw new ForbiddenAppException("This account has been deactivated.");
        }
    }

    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user, bool stampLogin)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var (rawRefreshToken, refreshTokenHash, expiresAtUtc) = _tokenService.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAtUtc = expiresAtUtc,
        });

        if (stampLogin)
        {
            user.LastLoginAtUtc = DateTime.UtcNow;
            _db.Users.Update(user);
        }

        await _db.SaveChangesAsync();

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes),
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                DisplayName = user.DisplayName,
                Roles = roles.ToList(),
            },
        };
    }
}
