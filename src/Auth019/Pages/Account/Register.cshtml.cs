using System.ComponentModel.DataAnnotations;
using Auth019.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Auth019.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public RegisterModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public IReadOnlyList<Country> CountryOptions => Countries.All;

    public class InputModel
    {
        [Required, MaxLength(100)]
        [Display(Name = "Name")]
        public string DisplayName { get; set; } = string.Empty;

        [Required, EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter your mobile number.")]
        [Phone(ErrorMessage = "Enter a valid mobile number.")]
        [MaxLength(30)]
        [Display(Name = "Mobile number")]
        public string MobileNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Select your country.")]
        [Display(Name = "Country")]
        public string Country { get; set; } = string.Empty;

        [Required, MinLength(8)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Retype your password.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
        [Display(Name = "Retype password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Guard against a hand-crafted post: the <select> only ever offers known codes.
        if (!Countries.IsKnownCode(Input.Country))
        {
            ModelState.AddModelError("Input.Country", "Select your country.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (await _userManager.FindByEmailAsync(Input.Email) is not null)
        {
            ErrorMessage = "An account with this email already exists.";
            return Page();
        }

        var user = new ApplicationUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            DisplayName = Input.DisplayName,
            PhoneNumber = Input.MobileNumber,
            Country = Input.Country.ToUpperInvariant(),
            LastLoginAtUtc = DateTime.UtcNow,
        };

        var result = await _userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            ErrorMessage = string.Join(" ", result.Errors.Select(e => e.Description));
            return Page();
        }

        await _userManager.AddToRoleAsync(user, AppRoles.User);
        await _signInManager.SignInAsync(user, isPersistent: true);

        return Url.IsLocalUrl(ReturnUrl) ? Redirect(ReturnUrl!) : Redirect("~/");
    }
}
