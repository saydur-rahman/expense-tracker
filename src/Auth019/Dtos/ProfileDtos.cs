using System.ComponentModel.DataAnnotations;

namespace Auth019.Dtos;

public class ProfileDto
{
    public Guid Id { get; set; }

    /// <summary>Read-only. Changing it would move the account itself, so it isn't editable here.</summary>
    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
    public string? MobileNumber { get; set; }
    public string? Country { get; set; }
    public string? CountryName { get; set; }

    /// <summary>ISO 4217, derived from the country. Null when no country is set.</summary>
    public string? CurrencyCode { get; set; }
}

public class UpdateProfileRequest
{
    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your mobile number.")]
    [Phone(ErrorMessage = "Enter a valid mobile number.")]
    [MaxLength(30)]
    public string MobileNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Select your country.")]
    public string Country { get; set; } = string.Empty;
}

public class CountryOptionDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
}
