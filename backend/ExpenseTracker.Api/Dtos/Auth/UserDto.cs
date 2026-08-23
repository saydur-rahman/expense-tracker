namespace ExpenseTracker.Api.Dtos.Auth;

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public bool IsImpersonating { get; set; }
    public Guid? ImpersonatedBy { get; set; }
}
