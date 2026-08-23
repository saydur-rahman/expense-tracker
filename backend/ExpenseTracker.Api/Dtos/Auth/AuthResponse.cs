namespace ExpenseTracker.Api.Dtos.Auth;

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAtUtc { get; set; }
    public UserDto User { get; set; } = null!;
}
