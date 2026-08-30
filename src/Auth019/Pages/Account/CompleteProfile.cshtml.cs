using System.ComponentModel.DataAnnotations;
using Auth019.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Auth019.Pages.Account;

/// <summary>
/// Collects the details a sign-up form would have asked for but an external provider
/// never supplies. Google hands over a name and an email and nothing else, so an
/// account created that way arrives with no mobile number and — more visibly — no
/// country, which is what the app derives its currency from.
/// </summary>
/// <remarks>
/// Reached from the authorize endpoint rather than the external-login callback, so it
/// cannot be skipped by already holding a session cookie: any account still missing a
/// country is sent here before a token is ever issued.
/// </remarks>
[Authorize]
public class CompleteProfileModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public CompleteProfileModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public string Email { get; private set; } = string.Empty;

    public IReadOnlyList<Country> CountryOptions => Countries.All;

    public class InputModel
    {
        [Required, MaxLength(100)]
        [Display(Name = "Name")]
        public string DisplayName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter your mobile number.")]
        [Phone(ErrorMessage = "Enter a valid mobile number.")]
        [MaxLength(30)]
        [Display(Name = "Mobile number")]
        public string MobileNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Select your country.")]
        [Display(Name = "Country")]
        public string Country { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToPage("./Login");
        }

        // Nothing outstanding — don't make someone re-confirm details they already gave.
        if (!string.IsNullOrWhiteSpace(user.Country))
        {
            return Continue();
        }

        Email = user.Email ?? string.Empty;
        Input.DisplayName = user.DisplayName;
        Input.MobileNumber = user.PhoneNumber ?? string.Empty;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToPage("./Login");
        }

        Email = user.Email ?? string.Empty;

        // Guard against a hand-crafted post: the <select> only ever offers known codes.
        if (!Countries.IsKnownCode(Input.Country))
        {
            ModelState.AddModelError("Input.Country", "Select your country.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        user.DisplayName = Input.DisplayName.Trim();
        user.PhoneNumber = Input.MobileNumber.Trim();
        user.Country = Input.Country.ToUpperInvariant();

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            ErrorMessage = string.Join(" ", result.Errors.Select(e => e.Description));
            return Page();
        }

        return Continue();
    }

    /// <summary>Resumes whatever was interrupted — normally the authorize request.</summary>
    private IActionResult Continue() =>
        Url.IsLocalUrl(ReturnUrl) ? Redirect(ReturnUrl!) : Redirect("~/");
}
