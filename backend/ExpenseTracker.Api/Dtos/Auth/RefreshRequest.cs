using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos.Auth;

public class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
