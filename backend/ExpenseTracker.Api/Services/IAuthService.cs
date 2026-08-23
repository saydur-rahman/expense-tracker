using ExpenseTracker.Api.Dtos.Auth;

namespace ExpenseTracker.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest request);
    Task<AuthResponse> RefreshAsync(RefreshRequest request);
}
