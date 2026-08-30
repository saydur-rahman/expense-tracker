using System.ComponentModel.DataAnnotations;
using Auth019.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Auth019.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public LoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public bool HasGoogleLogin { get; private set; }

    public class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public async Task OnGetAsync()
    {
        HasGoogleLogin = await HasGoogleProviderAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        HasGoogleLogin = await HasGoogleProviderAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _userManager.FindByEmailAsync(Input.Email);
        if (user is null)
        {
            ErrorMessage = "Invalid email or password.";
            return Page();
        }

        if (!user.IsActive)
        {
            ErrorMessage = "This account has been deactivated.";
            return Page();
        }

        var result = await _signInManager.PasswordSignInAsync(
            user, Input.Password, isPersistent: true, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            ErrorMessage = result.IsLockedOut
                ? "This account is temporarily locked. Try again later."
                : "Invalid email or password.";
            return Page();
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        return RedirectToSafeReturnUrl();
    }

    public async Task<IActionResult> OnPostExternalLoginAsync(string provider)
    {
        if (!await HasGoogleProviderAsync())
        {
            return BadRequest("External login is not configured.");
        }

        var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { ReturnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);

        // Deliberately no `prompt` parameter. Someone already signed in to Google
        // should go straight through on the account they are using; Google shows its
        // own chooser anyway when more than one account is signed in.
        //
        // Don't add `prompt=select_account` to make logout look convincing — that was
        // tried, and it fixes the wrong thing. Logout is honest because it lands on
        // the public /signed-out page and revokes the user's tokens; the silent
        // re-authentication people mistook for a broken logout was the SPA bouncing
        // itself into a fresh sign-in from a protected landing page.
        return Challenge(properties, provider);
    }

    private async Task<bool> HasGoogleProviderAsync()
        => (await _signInManager.GetExternalAuthenticationSchemesAsync())
            .Any(s => s.Name == "Google");

    private IActionResult RedirectToSafeReturnUrl()
        => Url.IsLocalUrl(ReturnUrl) ? Redirect(ReturnUrl!) : Redirect("~/");
}
