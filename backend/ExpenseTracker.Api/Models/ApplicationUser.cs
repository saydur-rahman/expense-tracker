using Microsoft.AspNetCore.Identity;

namespace ExpenseTracker.Api.Models;

public class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;
    public DateTime? DeactivatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }

    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
