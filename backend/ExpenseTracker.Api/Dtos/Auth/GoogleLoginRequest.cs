using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos.Auth;

public class GoogleLoginRequest
{
    [Required]
    public string IdToken { get; set; } = string.Empty;
}
